using SaveState.Core.Api;

namespace SaveState.UI;

/// <summary>Small factories and dialogs so every screen uses the same type styles and messages.</summary>
internal static class Ui
{
    public static Label Text(string text, Font? font = null, Color? color = null, int maxWidth = 0) => new()
    {
        Text = text,
        Font = font ?? Theme.Body,
        ForeColor = color ?? Theme.Ink,
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        Margin = new Padding(0),
        UseMnemonic = false,
        BackColor = Color.Transparent,
    };

    public static Label Heading(string text) => Text(text, Theme.H1);
    public static Label Subheading(string text) => Text(text, Theme.H2);
    public static Label Muted(string text, int maxWidth = 0) => Text(text, Theme.Body, Theme.InkMuted, maxWidth);
    public static Label FieldLabel(string text) => Text(text, Theme.Label);

    /// <summary>A soft-tinted message strip for inline errors / notices. Hidden until <see cref="ShowBanner"/>.</summary>
    public static Label Banner() => new()
    {
        AutoSize = true,
        Font = Theme.Body,
        Padding = new Padding(Theme.S3, Theme.S2 + 2, Theme.S3, Theme.S2 + 2),
        Margin = new Padding(0, 0, 0, Theme.S4),
        Visible = false,
        UseMnemonic = false,
    };

    public enum Tone { Error, Warning, Info, Success }

    public static void ShowBanner(Label banner, string message, Tone tone)
    {
        (banner.BackColor, banner.ForeColor) = tone switch
        {
            Tone.Error => (Theme.DangerSoft, Theme.Ink),
            Tone.Warning => (Theme.WarnSoft, Theme.Ink),
            Tone.Success => (Theme.AccentSoft, Theme.Ink),
            _ => (Theme.SurfaceSunk, Theme.Ink),
        };
        banner.Text = message;
        banner.MaximumSize = new Size(Math.Max(200, (banner.Parent?.ClientSize.Width ?? 600) - banner.Margin.Horizontal), 0);
        banner.Visible = true;
    }

    public static void HideBanner(Label banner) => banner.Visible = false;

    /// <summary>Friendly text for any exception coming out of an operation.</summary>
    public static string Describe(Exception e) => e switch
    {
        ApiException api => api.Message,
        UnauthorizedAccessException => "Windows blocked access to a file or folder. Check you have permission to read it.",
        IOException io => io.Message,
        _ => "Something unexpected went wrong. Please try again.",
    };

    public static void Error(IWin32Window? owner, string heading, string message) =>
        Show(owner, new TaskDialogPage
        {
            Caption = "SaveState",
            Heading = heading,
            Text = message,
            Icon = TaskDialogIcon.Error,
            Buttons = { TaskDialogButton.OK },
        });

    public static void Info(IWin32Window? owner, string heading, string message) =>
        Show(owner, new TaskDialogPage
        {
            Caption = "SaveState",
            Heading = heading,
            Text = message,
            Icon = TaskDialogIcon.Information,
            Buttons = { TaskDialogButton.OK },
        });

    private static TaskDialogButton Show(IWin32Window? owner, TaskDialogPage page) =>
        owner is null
            ? TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen)
            : TaskDialog.ShowDialog(owner, page, TaskDialogStartupLocation.CenterOwner);

    /// <summary>Asks a yes/no question with explicit verb buttons. Returns true for the confirm verb.</summary>
    public static bool Confirm(IWin32Window? owner, string heading, string message, string confirmText, bool warning = false)
    {
        var confirm = new TaskDialogButton(confirmText);
        var cancel = TaskDialogButton.Cancel;
        var result = Show(owner, new TaskDialogPage
        {
            Caption = "SaveState",
            Heading = heading,
            Text = message,
            Icon = warning ? TaskDialogIcon.Warning : TaskDialogIcon.Information,
            Buttons = { confirm, cancel },
            DefaultButton = cancel,
        });
        return result == confirm;
    }
}
