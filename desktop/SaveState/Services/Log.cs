namespace SaveState.Services;

/// <summary>Tiny append-only log in %LOCALAPPDATA%\SaveState\log.txt for troubleshooting. Never logs tokens.</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private const long MaxBytes = 512 * 1024;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? e = null) => Write("ERROR", e is null ? message : $"{message}\n{e}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                AppPaths.EnsureRoot();
                var file = new FileInfo(AppPaths.LogFile);
                if (file.Exists && file.Length > MaxBytes) file.Delete();
                File.AppendAllText(AppPaths.LogFile, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
