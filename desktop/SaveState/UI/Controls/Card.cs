namespace SaveState.UI.Controls;

/// <summary>A rounded surface with a hairline border. Children should use <see cref="Theme.Surface"/> as BackColor.</summary>
internal class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        Padding = new Padding(Theme.S5);
    }

    public Color FillColor { get; set; } = Theme.Surface;
    public Color BorderColor { get; set; } = Theme.Line;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        Theme.HighQuality(g);
        using var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(Theme.RadiusCard));
        using var fill = new SolidBrush(FillColor);
        g.FillPath(fill, path);
        if (BorderColor.A > 0)
        {
            using var pen = new Pen(BorderColor);
            g.DrawPath(pen, path);
        }
    }
}
