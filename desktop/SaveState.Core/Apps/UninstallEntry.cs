namespace SaveState.Core.Apps;

/// <summary>Raw values read from one registry Uninstall subkey (see InstalledAppScanner in the UI project).</summary>
public sealed record UninstallEntry
{
    public string? DisplayName { get; init; }
    public string? Publisher { get; init; }
    public string? DisplayVersion { get; init; }
    /// <summary>1 means "hidden system component" (drivers, runtimes, installer internals).</summary>
    public int? SystemComponent { get; init; }
    /// <summary>Set on updates/patches that belong to another product.</summary>
    public string? ParentKeyName { get; init; }
    /// <summary>"Update", "Hotfix", "Security Update", "Service Pack" for patches.</summary>
    public string? ReleaseType { get; init; }
}

/// <summary>An app as shown in the checklist.</summary>
public sealed record InstalledApp(string Name, string? Publisher, string? Version);
