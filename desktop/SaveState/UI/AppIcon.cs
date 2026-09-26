namespace SaveState.UI;

internal static class AppIcon
{
    private static Icon? _icon;

    /// <summary>The exe's own icon (set via ApplicationIcon in the csproj), reused for every window.</summary>
    public static Icon? Load()
    {
        try { return _icon ??= Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch (Exception) { return null; }
    }
}
