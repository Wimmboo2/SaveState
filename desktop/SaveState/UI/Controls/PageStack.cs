namespace SaveState.UI.Controls;

/// <summary>
/// Simple, deterministic page layout: items stack top-down at their natural height and full width,
/// one optional item fills the remaining space, and bottom items are pinned to the bottom.
/// Replaces TableLayoutPanel auto-size rows, which mis-measure wrapping text and custom controls
/// (big gaps, and in the worst case long layout loops).
/// </summary>
internal sealed class PageStack : Panel
{
    private readonly List<Control> _top = [];
    private readonly List<Control> _bottom = [];
    private Control? _fill;
    private bool _laying;

    public PageStack()
    {
        BackColor = Theme.Bg;
        Dock = DockStyle.Fill;
    }

    /// <summary>Adds an item below the previous top item.</summary>
    public PageStack Add(Control control)
    {
        _top.Add(control);
        Controls.Add(control);
        return this;
    }

    /// <summary>The item that takes whatever height is left (e.g. a list).</summary>
    public PageStack Fill(Control control)
    {
        _fill = control;
        Controls.Add(control);
        return this;
    }

    /// <summary>Adds an item pinned to the bottom (first call = lowest).</summary>
    public PageStack AddBottom(Control control)
    {
        _bottom.Add(control);
        Controls.Add(control);
        return this;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (_laying) return;
        _laying = true;
        try
        {
            var width = ClientSize.Width - Padding.Horizontal;
            var y = Padding.Top;
            foreach (var c in _top.Where(c => c.Visible))
                y = Place(c, y, width) + c.Margin.Bottom;

            var bottom = ClientSize.Height - Padding.Bottom;
            foreach (var c in _bottom.Where(c => c.Visible))
            {
                var h = MeasureHeight(c, width - c.Margin.Horizontal);
                bottom -= h + c.Margin.Bottom;
                c.SetBounds(Padding.Left + c.Margin.Left, bottom, width - c.Margin.Horizontal, h);
                bottom -= c.Margin.Top;
            }

            if (_fill is { Visible: true } fill)
            {
                var top = y + fill.Margin.Top;
                fill.SetBounds(Padding.Left + fill.Margin.Left, top, width - fill.Margin.Horizontal,
                    Math.Max(0, bottom - fill.Margin.Bottom - top));
            }
        }
        finally
        {
            _laying = false;
        }
    }

    private int Place(Control c, int y, int width)
    {
        var w = width - c.Margin.Horizontal;
        var h = MeasureHeight(c, w);
        // Toolbars keep their natural width; everything else spans the page.
        var actualWidth = c is FlowLayoutPanel ? Math.Min(w, c.PreferredSize.Width) : w;
        c.SetBounds(Padding.Left + c.Margin.Left, y + c.Margin.Top, actualWidth, h);
        return y + c.Margin.Top + h;
    }

    private static int MeasureHeight(Control c, int width)
    {
        switch (c)
        {
            case Label { AutoSize: true } label:
                // Wrap to the page width, then let the label size itself.
                label.MaximumSize = new Size(Math.Max(1, width), 0);
                return label.PreferredSize.Height;
            case Banner banner:
                return banner.GetPreferredSize(new Size(width, 0)).Height;
            case Card card:
                card.Width = width; // content re-flows and the card fits itself (see Card.FitTo)
                return card.Height;
            case FlowLayoutPanel flow:
                return flow.PreferredSize.Height;
            case TableLayoutPanel table:
                return table.GetPreferredSize(new Size(width, 0)).Height;
            default:
                return c.Height;
        }
    }
}
