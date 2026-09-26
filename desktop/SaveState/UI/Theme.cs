using System.Drawing.Drawing2D;

namespace SaveState.UI;

/// <summary>
/// SaveState design tokens for WinForms, mirroring web/src/app/globals.css ("Organic" direction):
/// sage-tinted off-white surfaces, deep moss as the single accent, soft rounded corners.
/// </summary>
internal static class Theme
{
    // Colors (light theme, same values as the website).
    public static readonly Color Bg = Hex("#edefe7");
    public static readonly Color Surface = Hex("#f7f8f3");
    public static readonly Color SurfaceSunk = Hex("#e2e6db");
    public static readonly Color Ink = Hex("#1e2620");
    public static readonly Color InkMuted = Hex("#4c5750");
    public static readonly Color InkSubtle = Hex("#5b665f");
    public static readonly Color Line = Hex("#d3d9cc");
    public static readonly Color LineStrong = Hex("#7f8a81");
    public static readonly Color Accent = Hex("#3d6a4c");
    public static readonly Color AccentHover = Hex("#335a40");
    public static readonly Color AccentSoft = Hex("#d8e5d6");
    public static readonly Color OnAccent = Hex("#f7f8f3");
    public static readonly Color Danger = Hex("#a0402e");
    public static readonly Color DangerSoft = Hex("#f1ddd5");
    public static readonly Color Warn = Hex("#7d5a0c");
    public static readonly Color WarnSoft = Hex("#f1e6c9");

    // Radii (logical pixels; scale with Scale()).
    public const int RadiusControl = 10;
    public const int RadiusCard = 16;

    // Spacing scale (logical pixels).
    public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32, S7 = 48;

    // Typography. Segoe UI ships with every Windows install. Headings use its Semibold cut when present
    // (a real font file, not synthesized bold). Web uses Nunito; bundling it into GDI+ isn't worth the fragility.
    private const string TextFamily = "Segoe UI";
    private static readonly string DisplayFamily = FirstInstalled("Segoe UI Semibold", "Segoe UI");
    private static readonly FontStyle DisplayStyle = DisplayFamily == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular;
    private static readonly string MonoFamily = FirstInstalled("Cascadia Mono", "Consolas");

    /// <summary>Icon font: Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10 (same code points).</summary>
    public static readonly string GlyphFamily = FirstInstalled("Segoe Fluent Icons", "Segoe MDL2 Assets");

    public static readonly Font Body = new(TextFamily, 10f);
    public static readonly Font BodySmall = new(TextFamily, 9f);
    public static readonly Font BodyStrong = new(DisplayFamily, 10f, DisplayStyle);
    public static readonly Font Label = new(DisplayFamily, 9f, DisplayStyle);
    public static readonly Font Button = new(DisplayFamily, 10f, DisplayStyle);
    public static readonly Font Mono = new(MonoFamily, 9f);
    public static readonly Font H1 = new(DisplayFamily, 20f, DisplayStyle);
    public static readonly Font H2 = new(DisplayFamily, 14f, DisplayStyle);
    public static readonly Font Wordmark = new(DisplayFamily, 13f, DisplayStyle);

    public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    /// <summary>A rounded-rectangle path. Radius is in device pixels.</summary>
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 1)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void HighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }

    private static string FirstInstalled(params string[] families)
    {
        using var installed = new System.Drawing.Text.InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return families.FirstOrDefault(names.Contains) ?? families[^1];
    }
}
