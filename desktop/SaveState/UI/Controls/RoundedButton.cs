using System.ComponentModel;

namespace SaveState.UI.Controls;

public enum ButtonVariant { Primary, Secondary, Ghost, Danger, Nav }

/// <summary>
/// A Button that paints itself as a soft rounded pill, with hover/pressed/focus/disabled states.
/// Still a real Button, so keyboard, AcceptButton and accessibility behave normally.
/// </summary>
internal sealed class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;
    private bool _selected;
    private ButtonVariant _variant = ButtonVariant.Primary;

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = Theme.Button;
        Cursor = Cursors.Hand;
        Height = 40;
        Padding = new Padding(Theme.S4, 0, Theme.S4, 0);
        AutoSizeMode = AutoSizeMode.GrowOnly;
        BackColor = Color.Transparent;
    }

    [DefaultValue(ButtonVariant.Primary)]
    public ButtonVariant Variant { get => _variant; set { _variant = value; Invalidate(); } }

    /// <summary>For sidebar navigation: marks the current page.</summary>
    [DefaultValue(false)]
    public bool Selected { get => _selected; set { _selected = value; AccessibleDescription = value ? "Current page" : null; Invalidate(); } }

    /// <summary>Optional small glyph drawn before the text (e.g. from Segoe Fluent Icons).</summary>
    public string? Glyph { get; set; }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        var glyph = Glyph is null ? 0 : LogicalToDeviceUnits(22);
        return new Size(text.Width + glyph + Padding.Horizontal + LogicalToDeviceUnits(8), Math.Max(Height, text.Height + LogicalToDeviceUnits(16)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ParentBackColor());
        Theme.HighQuality(g);

        var (fill, text, border) = Colors();
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (_pressed) rect.Offset(0, 1);
        using var path = Theme.RoundedRect(rect, LogicalToDeviceUnits(Theme.RadiusControl));

        if (fill.A > 0) { using var b = new SolidBrush(fill); g.FillPath(b, path); }
        if (border.A > 0) { using var p = new Pen(border, 1f); g.DrawPath(p, path); }

        var content = Rectangle.Round(rect);
        content = new Rectangle(content.X + Padding.Left, content.Y, content.Width - Padding.Horizontal, content.Height);
        var align = Variant == ButtonVariant.Nav ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;

        if (Glyph is not null)
        {
            using var iconFont = new Font(Theme.GlyphFamily, Font.SizeInPoints + 1, GraphicsUnit.Point);
            var glyphWidth = LogicalToDeviceUnits(22);
            var textWidth = TextRenderer.MeasureText(Text, Font).Width;
            var startX = Variant == ButtonVariant.Nav ? content.X : content.X + (content.Width - glyphWidth - textWidth) / 2;
            TextRenderer.DrawText(g, Glyph, iconFont, new Rectangle(startX, content.Y, glyphWidth, content.Height), text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            content = new Rectangle(startX + glyphWidth, content.Y, content.Right - startX - glyphWidth, content.Height);
            align = TextFormatFlags.Left;
        }

        TextRenderer.DrawText(g, Text, Font, content, text,
            align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Theme.Accent, LogicalToDeviceUnits(2)) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset };
            using var fpath = Theme.RoundedRect(new RectangleF(1, 1, Width - 3, Height - 3), LogicalToDeviceUnits(Theme.RadiusControl));
            g.DrawPath(focus, fpath);
        }
    }

    private (Color Fill, Color Text, Color Border) Colors()
    {
        if (!Enabled)
        {
            return Variant == ButtonVariant.Primary || Variant == ButtonVariant.Danger
                ? (Blend(Theme.Accent, ParentBackColor(), 0.45f), Theme.OnAccent, Color.Empty)
                : (Color.Empty, Theme.InkSubtle, Variant == ButtonVariant.Secondary ? Theme.Line : Color.Empty);
        }

        return Variant switch
        {
            ButtonVariant.Primary => (_hover ? Theme.AccentHover : Theme.Accent, Theme.OnAccent, Color.Empty),
            ButtonVariant.Danger => (_hover ? Blend(Theme.Danger, Color.Black, 0.12f) : Theme.Danger, Theme.OnAccent, Color.Empty),
            ButtonVariant.Secondary => (Theme.Surface, Theme.Ink, _hover ? Theme.InkMuted : Theme.LineStrong),
            ButtonVariant.Ghost => (_hover ? Theme.SurfaceSunk : Color.Empty, _hover ? Theme.Ink : Theme.InkMuted, Color.Empty),
            ButtonVariant.Nav => _selected
                ? (Theme.AccentSoft, Theme.Ink, Color.Empty)
                : (_hover ? Theme.SurfaceSunk : Color.Empty, _hover ? Theme.Ink : Theme.InkMuted, Color.Empty),
            _ => (Theme.Accent, Theme.OnAccent, Color.Empty),
        };
    }

    private Color ParentBackColor()
    {
        for (Control? c = Parent; c is not null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return Theme.Bg;
    }

    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}
