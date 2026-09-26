namespace SaveState.Services;

/// <summary>Where SaveState keeps its own local files: %LOCALAPPDATA%\SaveState.</summary>
internal static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaveState");

    public static string SessionFile => Path.Combine(Root, "session.dat");
    public static string SelectionFile => Path.Combine(Root, "selection.json");
    public static string LogFile => Path.Combine(Root, "log.txt");
    public static string TempDir => Path.Combine(Path.GetTempPath(), "SaveState");

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
