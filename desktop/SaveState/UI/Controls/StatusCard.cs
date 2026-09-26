namespace SaveState.UI.Controls;

/// <summary>
/// A tinted rounded block with an icon, a bold title and a few lines of detail, e.g.
/// "Backed up / 26 Sep 2026, 18:40 / 4 apps, 12 files, 3 MB". Measures its own height from its
/// width like <see cref="Banner"/>. Place it with Anchor = Left | Right.
/// </summary>
internal sealed class StatusCard : Control
{
    private BackupStatus _status = new(Ui.Tone.Info, "", "", "");
    private readonly Font _titleFont;
    private readonly Font _detailFont;
    private bool _hover;

    public StatusCard(Font? titleFont = null, Font? detailFont = null)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        _titleFont = titleFont ?? Theme.BodyStrong;
        _detailFont = detailFont ?? Theme.BodySmall;
        Anchor = AnchorStyles.Left | AnchorStyles.Right;
        Padding = new Padding(Theme.S3);
        AccessibleRole = AccessibleRole.StaticText;
        TabStop = false;
    }

    /// <summary>Makes the card act like a link (hover tint, hand cursor, Enter/Space activate).</summary>
    public bool Clickable
    {
        get => _clickable;
        set
        {
            _clickable = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            TabStop = value;
            AccessibleRole = value ? AccessibleRole.Link : AccessibleRole.StaticText;
        }
    }

    private bool _clickable;

    public BackupStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            AccessibleName = $"{value.Title}. {value.Detail.Replace('\n', ' ')}";
            FitHeight();
            Invalidate();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        return new Size(width, HeightFor(width));
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        FitHeight(); // only changes Height, so this can't loop
    }

    private int GlyphBox => LogicalToDeviceUnits(22);
    private int Gap => LogicalToDeviceUnits(10);

    private int TextWidth(int width) => Math.Max(40, width - Padding.Horizontal - GlyphBox - Gap);

    private int HeightFor(int width)
    {
        var textWidth = TextWidth(width);
        var title = TextRenderer.MeasureText(_status.Title.Length == 0 ? " " : _status.Title, _titleFont, new Size(textWidth, int.MaxValue), Flags);
        var height = title.Height;
        if (_status.Detail.Length > 0)
            height += LogicalToDeviceUnits(2) + TextRenderer.MeasureText(_status.Detail, _detailFont, new Size(textWidth, int.MaxValue), Flags).Height;
        return Math.Max(height, GlyphBox) + Padding.Vertical;
    }

    private void FitHeight()
    {
        var height = HeightFor(Width);
        if (Height != height) Height = height;
    }

    private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

    private (Color Fill, Color Icon) Colors => _status.Tone switch
    {
        Ui.Tone.Success => (Theme.AccentSoft, Theme.Accent),
        Ui.Tone.Warning => (Theme.WarnSoft, Theme.Warn),
        Ui.Tone.Error => (Theme.DangerSoft, Theme.Danger),
        _ => (Theme.SurfaceSunk, Theme.InkMuted),
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        Theme.HighQuality(g);
        var (fill, icon) = Colors;
        if (_hover && _clickable) fill = ControlPaint.Dark(fill, 0.03f);
        using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), LogicalToDeviceUnits(Theme.RadiusControl)))
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);

        var x = Padding.Left;
        var y = Padding.Top;
        if (_status.Glyph.Length > 0)
        {
            using var glyphFont = new Font(Theme.GlyphFamily, 12f);
            TextRenderer.DrawText(g, _status.Glyph, glyphFont, new Rectangle(x, y, GlyphBox, GlyphBox), icon,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        var textX = x + GlyphBox + Gap;
        var textWidth = TextWidth(Width);
        var titleSize = TextRenderer.MeasureText(_status.Title, _titleFont, new Size(textWidth, int.MaxValue), Flags);
        // Center a one-line title on the icon; longer content just starts at the top.
        var titleY = y + Math.Max(0, (GlyphBox - titleSize.Height) / 2);
        TextRenderer.DrawText(g, _status.Title, _titleFont, new Rectangle(textX, titleY, textWidth, titleSize.Height), Theme.Ink, Flags);
        if (_status.Detail.Length > 0)
        {
            var detailY = titleY + titleSize.Height + LogicalToDeviceUnits(2);
            TextRenderer.DrawText(g, _status.Detail, _detailFont, new Rectangle(textX, detailY, textWidth, Height - detailY), Theme.InkMuted, Flags);
        }

        if (Focused && _clickable) ControlPaint.DrawFocusRectangle(g, new Rectangle(2, 2, Width - 5, Height - 5));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_clickable && e.KeyCode is Keys.Enter or Keys.Space) OnClick(EventArgs.Empty);
    }
}
