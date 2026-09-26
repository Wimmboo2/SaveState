namespace SaveState.UI.Pages;

/// <summary>Files page. Implemented in the next phase.</summary>
internal sealed class FilesPage : UserControl, IPage
{
    public FilesPage(AppState state)
    {
        _ = state;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Bg;
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, BackColor = Theme.Bg };
        layout.Controls.Add(Ui.Heading("Files"));
        layout.Controls.Add(Ui.Muted("Coming in the next update."));
        Controls.Add(layout);
    }

    public void OnShown() { }
}
