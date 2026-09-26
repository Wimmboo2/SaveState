using System.Runtime.InteropServices;

namespace SaveState.UI.Controls;

/// <summary>DataGridView styled to match the theme: airy rows, hairline separators, no Win95 chrome.</summary>
internal sealed class ThemedGrid : DataGridView
{
    public ThemedGrid()
    {
        DoubleBuffered = true;
        BackgroundColor = Theme.Surface;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        GridColor = Theme.Line;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        AllowUserToOrderColumns = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        EnableHeadersVisualStyles = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 40;
        RowTemplate.Height = 40;
        Font = Theme.Body;

        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.InkMuted,
            SelectionBackColor = Theme.Surface,
            SelectionForeColor = Theme.InkMuted,
            Font = Theme.Label,
            Padding = new Padding(8, 0, 8, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        DefaultCellStyle = new DataGridViewCellStyle
        {
            // Always set: a replaced default style with no font makes cell painting throw.
            Font = Theme.Body,
            BackColor = Theme.Surface,
            ForeColor = Theme.Ink,
            SelectionBackColor = Theme.AccentSoft,
            SelectionForeColor = Theme.Ink,
            Padding = new Padding(8, 0, 8, 0),
            WrapMode = DataGridViewTriState.False,
        };
    }

    /// <summary>A new, fully configured row for this grid (not added yet).</summary>
    public DataGridViewRow NewRow(object? tag, params object?[] values)
    {
        var row = new DataGridViewRow { Height = RowTemplate.Height };
        row.CreateCells(this, values!);
        row.Tag = tag;
        return row;
    }

    /// <summary>
    /// Swaps in a complete set of rows with painting switched off, so the grid never paints a
    /// half-built row (DataGridView repaints synchronously during Clear/Add, which can throw).
    /// </summary>
    public void ReplaceRows(IEnumerable<DataGridViewRow> rows)
    {
        var batch = rows.ToArray();
        // WM_SETREDRAW(TRUE) also sets WS_VISIBLE, which would bring a hidden grid back on screen
        // behind WinForms' back; a hidden grid doesn't paint anyway, so skip the dance.
        if (!IsHandleCreated || !Visible)
        {
            Rows.Clear();
            Rows.AddRange(batch);
            return;
        }

        SendMessage(Handle, WM_SETREDRAW, 0, 0);
        try
        {
            Rows.Clear();
            Rows.AddRange(batch);
            ClearSelection();
        }
        finally
        {
            SendMessage(Handle, WM_SETREDRAW, 1, 0);
            Invalidate(true);
        }
    }

    private const int WM_SETREDRAW = 0x000B;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, nint wParam, nint lParam);
}
