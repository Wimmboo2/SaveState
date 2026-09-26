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
}
