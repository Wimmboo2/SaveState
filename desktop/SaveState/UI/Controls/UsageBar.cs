using SaveState.Core.Backup;

namespace SaveState.UI.Controls;

/// <summary>"42 MB / 100 MB" with a rounded track. Amber past 85%, red when over the limit.</summary>
internal sealed class UsageBar : Control
{
    private long _used;
    private long _max = 1;

    public UsageBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 48;
        Font = Theme.BodySmall;
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    public long Used { get => _used; set { _used = value; UpdateAccessibility(); Invalidate(); } }
    public long Max { get => _max; set { _max = Math.Max(1, value); UpdateAccessibility(); Invalidate(); } }
    public string? Caption { get; set; }

    private void UpdateAccessibility() =>
        AccessibleName = $"{Caption ?? "Backup size"}: {Sizes.Format(_used)} of {Sizes.Format(_max)}";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        Theme.HighQuality(g);

        var ratio = (double)_used / _max;
        var over = ratio > 1;
        var fillColor = over ? Theme.Danger : ratio >= 0.85 ? Theme.Warn : Theme.Accent;

        var left = $"{Sizes.Format(_used)} / {Sizes.Format(_max)}";
        var right = over ? $"{Sizes.Format(_used - _max)} over the limit" : $"{Math.Round(ratio * 100)}% used";
        var textHeight = TextRenderer.MeasureText("Ag", Theme.BodyStrong).Height;
        TextRenderer.DrawText(g, left, Theme.BodyStrong, new Point(0, 0), Theme.Ink);
        var rightSize = TextRenderer.MeasureText(right, Font);
        TextRenderer.DrawText(g, right, Font, new Point(Width - rightSize.Width, (textHeight - rightSize.Height) / 2), over ? Theme.Danger : Theme.InkMuted);

        var barHeight = LogicalToDeviceUnits(10);
        var top = textHeight + LogicalToDeviceUnits(8);
        var track = new RectangleF(0, top, Width - 1, barHeight);
        using (var path = Theme.RoundedRect(track, barHeight / 2f))
        using (var brush = new SolidBrush(Theme.SurfaceSunk))
            g.FillPath(brush, path);

        if (_used > 0)
        {
            var w = (float)Math.Max(barHeight, Math.Min(1, ratio) * track.Width);
            using var path = Theme.RoundedRect(new RectangleF(0, top, w, barHeight), barHeight / 2f);
            using var brush = new SolidBrush(fillColor);
            g.FillPath(brush, path);
        }
    }
}
