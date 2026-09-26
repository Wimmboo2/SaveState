namespace SaveState.UI.Pages;

/// <summary>Backup page. Implemented in the next phase.</summary>
internal sealed class BackupPage : UserControl, IPage
{
    public BackupPage(AppState state)
    {
        _ = state;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Bg;
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, BackColor = Theme.Bg };
        layout.Controls.Add(Ui.Heading("Backup"));
        layout.Controls.Add(Ui.Muted("Coming in the next update."));
        Controls.Add(layout);
    }

    public void OnShown() { }
}
