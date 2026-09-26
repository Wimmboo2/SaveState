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

    public void SetBackup(BackupRow? backup)
    {
        Backup = backup;
        BackupLoaded = true;
        BackupChanged?.Invoke(this, EventArgs.Empty);
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
            var backup = await Api.GetBackupAsync();
            SetBackup(backup);
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

    public void SetSources(IEnumerable<string> sources)
    {
        var list = sources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Sources.Clear();
        Sources.AddRange(list);
        SelectionStore.Save(Sources);
        SourcesChanged?.Invoke(this, EventArgs.Empty);
    }
}
