namespace SaveState.Core.Backup;

/// <summary>A known folder that can be written as an environment variable, e.g. APPDATA → C:\Users\me\AppData\Roaming.</summary>
public sealed record PathRoot(string Token, string BasePath);

/// <summary>
/// Converts real paths into portable ones. The Windows username (and so C:\Users\&lt;name&gt;) often
/// changes after a reinstall, so paths are stored as %APPDATA%\..., %USERPROFILE%\... instead.
/// Works on Windows-style strings regardless of the OS running the code (so it's testable anywhere).
/// </summary>
public static class PathTokens
{
    /// <summary>The current user's known folders, most specific first.</summary>
    public static IReadOnlyList<PathRoot> FromEnvironment()
    {
        var roots = new List<PathRoot>
        {
            new("LOCALAPPDATA", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            new("APPDATA", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            new("USERPROFILE", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            new("PUBLIC", Environment.GetEnvironmentVariable("PUBLIC") ?? ""),
            new("PROGRAMDATA", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
            new("ProgramFiles(x86)", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
            new("ProgramFiles", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
        };
        return Normalize(roots);
    }

    /// <summary>Drops empty roots and orders by length so nested folders (LOCALAPPDATA inside USERPROFILE) win.</summary>
    public static IReadOnlyList<PathRoot> Normalize(IEnumerable<PathRoot> roots) => roots
        .Where(r => !string.IsNullOrWhiteSpace(r.BasePath))
        .Select(r => r with { BasePath = Clean(r.BasePath) })
        .DistinctBy(r => r.BasePath, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(r => r.BasePath.Length)
        .ToList();

    /// <summary>"C:\Users\me\AppData\Roaming\.minecraft\options.txt" → "%APPDATA%\.minecraft\options.txt".</summary>
    public static string Tokenize(string path, IReadOnlyList<PathRoot> roots)
    {
        var clean = Clean(path);
        foreach (var root in roots)
        {
            var rest = RelativeTo(clean, root.BasePath);
            if (rest is not null) return rest.Length == 0 ? $"%{root.Token}%" : $"%{root.Token}%\\{rest}";
        }
        return clean;
    }

    /// <summary>
    /// Where a file lives inside the zip. Known folders map to files/APPDATA/..., other drives to
    /// files/_drives/D/..., network shares to files/_network/server/share/.... Always forward slashes.
    /// </summary>
    public static string ZipPath(string path, IReadOnlyList<PathRoot> roots)
    {
        var clean = Clean(path);
        foreach (var root in roots)
        {
            var rest = RelativeTo(clean, root.BasePath);
            if (rest is not null) return Join("files", root.Token, rest);
        }

        if (clean.StartsWith(@"\\", StringComparison.Ordinal))
            return Join("files", "_network", clean.TrimStart('\\'));
        if (clean.Length >= 2 && clean[1] == ':' && char.IsLetter(clean[0]))
            return Join("files", "_drives", char.ToUpperInvariant(clean[0]).ToString(), clean[2..].TrimStart('\\'));
        return Join("files", "_other", clean.TrimStart('\\'));
    }

    /// <summary>Normalizes separators to backslashes and trims trailing ones.</summary>
    public static string Clean(string path)
    {
        var p = path.Trim().Replace('/', '\\');
        var unc = p.StartsWith(@"\\", StringComparison.Ordinal);
        while (p.Contains(@"\\", StringComparison.Ordinal)) p = p.Replace(@"\\", @"\", StringComparison.Ordinal);
        if (unc) p = @"\" + p;
        return p.Length > 3 ? p.TrimEnd('\\') : p;
    }

    /// <summary>The part of <paramref name="path"/> below <paramref name="basePath"/>, or null if it isn't inside it.</summary>
    private static string? RelativeTo(string path, string basePath)
    {
        if (path.Equals(basePath, StringComparison.OrdinalIgnoreCase)) return "";
        var prefix = basePath.EndsWith('\\') ? basePath : basePath + "\\";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : null;
    }

    private static string Join(params string[] parts) =>
        string.Join('/', parts.Where(p => p.Length > 0).Select(p => p.Replace('\\', '/').Trim('/')));
}
