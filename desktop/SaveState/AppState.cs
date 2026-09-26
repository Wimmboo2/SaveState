using SaveState.Core.Api;
using SaveState.Core.Apps;
using SaveState.Core.Backup;
using SaveState.Core.Models;
using SaveState.Services;

namespace SaveState;

/// <summary>State shared by the pages of the main window.</summary>
internal sealed class AppState
{
    private Task<string?>? _appsLoad;

    public AppState(SupabaseApi api, HttpClient http)
    {
        Api = api;
        Transfer = new BackupTransfer(api, http);
        Sources = SelectionStore.Load();
    }

    public SupabaseApi Api { get; }
    public BackupTransfer Transfer { get; }

    /// <summary>The user's row from the server; null until loaded or if they never backed up.</summary>
    public BackupRow? Backup { get; private set; }
    public bool BackupLoaded { get; private set; }

    /// <summary>The Apps checklist (installed + previously saved apps).</summary>
    public List<AppListItem> Apps { get; private set; } = [];
    public bool AppsLoaded { get; private set; }

    /// <summary>Files and folders picked for backup (full local paths). Remembered between launches.</summary>
    public List<string> Sources { get; }

    public event EventHandler? BackupChanged;
    public event EventHandler? SourcesChanged;

    public IReadOnlyList<SavedApp> SelectedApps =>
        Apps.Where(a => a.IsSelected).Select(a => a.ToSavedApp()).ToList();

    /// <summary>True while a backup or download runs (background refreshes wait until it's done).</summary>
    public bool TransferRunning { get; set; }

    private int _backupVersion;
    private bool _refreshing;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private DateTimeOffset? _fileListRequestedFor;

    public void SetBackup(BackupRow? backup)
    {
        Backup = backup;
        BackupLoaded = true;
        _backupVersion++;
        BackupChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-reads the row from the server so changes made elsewhere show up here: files deleted on
    /// the website, files that expired, or a backup made from another PC. Quietly does nothing
    /// when offline. Throttled unless <paramref name="force"/>.
    /// </summary>
    public async Task RefreshBackupAsync(bool force = false)
    {
        if (_refreshing || TransferRunning || !BackupLoaded) return;
        if (!force && DateTime.UtcNow - _lastRefreshUtc < TimeSpan.FromSeconds(15)) return;
        _refreshing = true;
        _lastRefreshUtc = DateTime.UtcNow;
        var version = _backupVersion;
        try
        {
            var row = await LoadRowAsync();
            // Something newer (an upload that just finished) wins over this read.
            if (version != _backupVersion || TransferRunning) return;
            if (row?.UpdatedAt != Backup?.UpdatedAt || (row is null) != (Backup is null)) SetBackup(row);
        }
        catch (ApiException e)
        {
            Log.Info($"Background refresh skipped: {e.Message}");
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>The user's row, with the file list filled in by the server if it's missing.</summary>
    private async Task<BackupRow?> LoadRowAsync()
    {
        var row = await Api.GetBackupAsync();
        if (row is { HasFiles: true, Files: null } && _fileListRequestedFor != row.UploadedAt)
        {
            _fileListRequestedFor = row.UploadedAt; // ask once per upload
            try { row = await Transfer.FetchFileListAsync() ?? row; }
            catch (ApiException e) { Log.Info($"Couldn't load the file list: {e.Message}"); }
        }
        return row;
    }

    /// <summary>
    /// Scans installed apps and merges them with the saved list (once). Returns a warning to show
    /// if the saved list couldn't be fetched (the checklist still works offline).
    /// </summary>
    public Task<string?> EnsureAppsLoadedAsync() => _appsLoad ??= LoadAppsAsync();

    private async Task<string?> LoadAppsAsync()
    {
        var installedTask = Task.Run(InstalledAppScanner.Scan);
        IReadOnlyList<SavedApp> saved = [];
        string? warning = null;
        try
        {
            var backup = await LoadRowAsync();
            SetBackup(backup);
            _lastRefreshUtc = DateTime.UtcNow;
            saved = backup?.Apps ?? [];
        }
        catch (ApiException e)
        {
            warning = e.Message;
            if (e.Kind == ApiErrorKind.SessionExpired) throw;
        }

        Apps = AppListMerger.Merge(await installedTask, saved);
        // Only trust the list for uploading if we know what was saved before; otherwise an offline
        // start could overwrite the saved list with an empty selection.
        AppsLoaded = warning is null;
        if (!AppsLoaded) _appsLoad = null; // allow a retry later
        return warning;
    }

    /// <summary>Fills the state with sample data (used by the --screenshots mode, no network).</summary>
    internal void LoadDemo(List<AppListItem> apps, BackupRow? backup, IEnumerable<string> sources)
    {
        Apps = apps;
        AppsLoaded = true;
        _appsLoad = Task.FromResult<string?>(null);
        Sources.Clear();
        Sources.AddRange(sources);
        SetBackup(backup);
    }

    public void SetSources(IEnumerable<string> sources)
    {
        var list = sources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Sources.Clear();
        Sources.AddRange(list);
        SelectionStore.Save(Sources);
        SourcesChanged?.Invoke(this, EventArgs.Empty);
    }
}
