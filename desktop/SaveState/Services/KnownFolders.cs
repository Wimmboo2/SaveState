namespace SaveState.Services;

internal static class KnownFolders
{
    /// <summary>The user's Downloads folder (falls back to Documents if it doesn't exist).</summary>
    public static string Downloads
    {
        get
        {
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            return Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
