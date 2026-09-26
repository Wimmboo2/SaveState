using SaveState.Core.Api;
using SaveState.Core.Apps;
using SaveState.Core.Models;

namespace SaveState;

/// <summary>State shared by the pages of the main window.</summary>
internal sealed class AppState(SupabaseApi api)
{
    public SupabaseApi Api { get; } = api;

    /// <summary>The user's row from the server; null until loaded or if they never backed up.</summary>
    public BackupRow? Backup { get; private set; }
    public bool BackupLoaded { get; private set; }

    /// <summary>The Apps checklist (installed + previously saved apps).</summary>
    public List<AppListItem> Apps { get; set; } = [];

    public event EventHandler? BackupChanged;

    public IReadOnlyList<SavedApp> SelectedApps =>
        Apps.Where(a => a.IsSelected).Select(a => a.ToSavedApp()).ToList();

    public void SetBackup(BackupRow? backup)
    {
        Backup = backup;
        BackupLoaded = true;
        BackupChanged?.Invoke(this, EventArgs.Empty);
    }
}
