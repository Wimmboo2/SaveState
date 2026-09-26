namespace SaveState.UI.Controls;

/// <summary>
/// Soft rounded message strip for inline errors / warnings / confirmations.
/// Measures its own height from its width (no AutoSize), because a word-wrapping AutoSize label
/// inside an auto-size table row makes WinForms re-layout for many seconds.
/// Place it with Anchor = Left | Right in an auto-size row.
/// </summary>
internal sealed class Banner : Control
{
    private Ui.Tone _tone = Ui.Tone.Info;
    private string _message = "";

    public Banner()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = Theme.Body;
        Visible = false;
        Anchor = AnchorStyles.Left | AnchorStyles.Right;
        Margin = new Padding(0, 0, 0, Theme.S4);
        Padding = new Padding(Theme.S3 + 2, Theme.S2 + 2, Theme.S3 + 2, Theme.S2 + 2);
        AccessibleRole = AccessibleRole.Alert;
    }

    public void Show(string message, Ui.Tone tone)
    {
        _message = message;
        _tone = tone;
        AccessibleName = message;
        FitHeight();
        Visible = true;
        Invalidate();
    }

    public new void Hide() => Visible = false;

    public string Message => _message;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        FitHeight(); // only changes Height, so this can't loop
    }

    private void FitHeight()
    {
        var textWidth = Math.Max(40, Width - Padding.Horizontal);
        var measured = TextRenderer.MeasureText(_message.Length == 0 ? " " : _message, Font,
            new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        var height = measured.Height + Padding.Vertical;
        if (Height != height) Height = height;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        Theme.HighQuality(g);
        var fill = _tone switch
        {
            Ui.Tone.Error => Theme.DangerSoft,
            Ui.Tone.Warning => Theme.WarnSoft,
            Ui.Tone.Success => Theme.AccentSoft,
            _ => Theme.SurfaceSunk,
        };
        using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), LogicalToDeviceUnits(Theme.RadiusControl)))
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);

        var text = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
        TextRenderer.DrawText(g, _message, Font, text, Theme.Ink, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
