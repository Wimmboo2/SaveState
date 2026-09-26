using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;

namespace SaveState.UI.Controls;

/// <summary>
/// A text input with a rounded border (WinForms TextBox can't do that itself): a borderless
/// TextBox hosted inside a painted frame that turns moss green on focus and red on error.
/// </summary>
internal sealed class InputBox : UserControl
{
    private readonly TextBox _box = new()
    {
        BorderStyle = BorderStyle.None,
        Font = Theme.Body,
        BackColor = Theme.Surface,
        ForeColor = Theme.Ink,
    };
    private bool _invalid;

    public InputBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        Height = 42;
        Padding = new Padding(12, 0, 12, 0);
        Controls.Add(_box);
        _box.GotFocus += (_, _) => Invalidate();
        _box.LostFocus += (_, _) => Invalidate();
        _box.TextChanged += (_, e) => OnTextChanged(e);
        _box.KeyDown += (_, e) => OnKeyDown(e);
        Cursor = Cursors.IBeam;
        Click += (_, _) => _box.Focus();
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [AllowNull]
    public override string Text { get => _box.Text; set => _box.Text = value ?? ""; }

    public string PlaceholderText { get => _box.PlaceholderText; set => _box.PlaceholderText = value; }
    public bool Password { get => _box.UseSystemPasswordChar; set => _box.UseSystemPasswordChar = value; }
    public TextBox Inner => _box;

    /// <summary>Red border for validation errors.</summary>
    public bool Invalid { get => _invalid; set { _invalid = value; Invalidate(); } }

    public new bool Focus() => _box.Focus();

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var pad = LogicalToDeviceUnits(12);
        _box.Width = Width - pad * 2;
        _box.Location = new Point(pad, (Height - _box.Height) / 2);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        Theme.HighQuality(g);
        var focused = _box.Focused;
        var border = _invalid ? Theme.Danger : focused ? Theme.Accent : Theme.LineStrong;
        var width = focused || _invalid ? LogicalToDeviceUnits(2) : 1;
        var inset = width / 2f + 0.5f;
        using var path = Theme.RoundedRect(new RectangleF(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1), LogicalToDeviceUnits(Theme.RadiusControl));
        using var fill = new SolidBrush(Theme.Surface);
        g.FillPath(fill, path);
        using var pen = new Pen(border, width);
        g.DrawPath(pen, path);
    }
}
