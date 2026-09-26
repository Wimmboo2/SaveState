using System.Text.RegularExpressions;

namespace SaveState.Core.Apps;

/// <summary>Turns raw Uninstall registry entries into a clean, de-duplicated list of real apps.</summary>
public static partial class AppFilter
{
    private static readonly HashSet<string> PatchReleaseTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Update", "Hotfix", "Security Update", "Service Pack" };

    // "Security Update for Windows (KB5034441)", "Update for Microsoft Office 2016 (KB4484103)", ...
    [GeneratedRegex(@"\bKB\d{6,8}\b", RegexOptions.IgnoreCase)]
    private static partial Regex KbArticle();

    /// <summary>True for entries a person would recognise as an installed app.</summary>
    public static bool IsUserFacing(UninstallEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DisplayName)) return false;
        if (entry.SystemComponent == 1) return false;
        if (!string.IsNullOrWhiteSpace(entry.ParentKeyName)) return false;
        if (entry.ReleaseType is { } type && PatchReleaseTypes.Contains(type.Trim())) return false;
        if (KbArticle().IsMatch(entry.DisplayName)) return false;
        return true;
    }

    /// <summary>
    /// Filters out junk and collapses duplicates (the same app registered under HKLM 64-bit,
    /// WOW6432Node and/or HKCU). Keeps the entry with the highest version. Sorted by name.
    /// </summary>
    public static List<InstalledApp> Clean(IEnumerable<UninstallEntry> entries)
    {
        return entries
            .Where(IsUserFacing)
            .Select(e => new InstalledApp(
                Collapse(e.DisplayName!),
                NullIfBlank(e.Publisher),
                NullIfBlank(e.DisplayVersion)))
            .GroupBy(a => AppKey(a.Name, a.Publisher))
            .Select(g => g.OrderByDescending(a => ParseVersion(a.Version)).First())
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Identity used to match an installed app with a saved one: name + publisher, case/space-insensitive.</summary>
    public static string AppKey(string name, string? publisher) =>
        $"{Collapse(name).ToLowerInvariant()}|{Collapse(publisher ?? "").ToLowerInvariant()}";

    private static Version ParseVersion(string? value)
    {
        if (value is null) return new Version(0, 0);
        // Keep only the leading dotted-number part: "2.24.17 (beta)" -> "2.24.17".
        var match = Regex.Match(value, @"^\d+(\.\d+){0,3}");
        if (!match.Success) return new Version(0, 0);
        var text = match.Value.Contains('.') ? match.Value : match.Value + ".0";
        return Version.TryParse(text, out var v) ? v : new Version(0, 0);
    }

    private static string Collapse(string value) => Regex.Replace(value.Trim(), @"\s+", " ");
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
