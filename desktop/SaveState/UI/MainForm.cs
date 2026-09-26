using SaveState.UI.Controls;
using SaveState.UI.Pages;

namespace SaveState.UI;

/// <summary>Main window: sidebar navigation (Apps, Files, Backup) plus the current page.</summary>
internal sealed class MainForm : Form
{
    private readonly AppState _state;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    private readonly List<(RoundedButton Button, Control Page)> _pages = [];

    /// <summary>True when the window closed because the user signed out (Program shows the login again).</summary>
    public bool SignedOut { get; private set; }

    /// <summary>True when the window closed because the login expired (Program asks the user to log in again).</summary>
    public bool SessionExpired { get; private set; }

    public void ExpireSession()
    {
        if (SessionExpired) return;
        SessionExpired = true;
        BeginInvoke(Close);
    }

    public MainForm(AppState state)
    {
        _state = state;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "SaveState";
        Icon = AppIcon.Load();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        Font = Theme.Body;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1140, 740);
        MinimumSize = new Size(940, 620);

        _content.Padding = new Padding(Theme.S6, Theme.S6, Theme.S6, Theme.S5);

        var apps = new AppsPage(state) { Dock = DockStyle.Fill };
        var files = new FilesPage(state) { Dock = DockStyle.Fill };
        var backup = new BackupPage(state) { Dock = DockStyle.Fill };

        var sidebar = BuildSidebar(
            ("Apps", "", apps),
            ("Files", "", files),
            ("Backup", "", backup));

        Controls.Add(_content);
        Controls.Add(sidebar);
        ResumeLayout(true);

        Show(apps);
    }

    private Control BuildSidebar(params (string Label, string Glyph, Control Page)[] pages)
    {
        var sidebar = new SidebarPanel { Dock = DockStyle.Left, Width = 232, Padding = new Padding(Theme.S4, Theme.S5, Theme.S4, Theme.S4) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var wordmark = Ui.Text("SaveState", Theme.Wordmark);
        wordmark.Margin = new Padding(Theme.S3, 0, 0, Theme.S6);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(wordmark);

        foreach (var (label, glyph, page) in pages)
        {
            var button = new RoundedButton
            {
                Text = label,
                Glyph = glyph,
                Variant = ButtonVariant.Nav,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 42,
                Margin = new Padding(0, 0, 0, Theme.S1),
                Padding = new Padding(Theme.S3, 0, Theme.S3, 0),
                Font = Theme.BodyStrong,
            };
            button.Click += (_, _) => Show(page);
            _pages.Add((button, page));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(button);
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface });

        var account = Ui.Text(_state.Api.Session?.Email ?? "", Theme.BodySmall, Theme.InkMuted, maxWidth: 190);
        account.AutoEllipsis = true;
        account.Margin = new Padding(Theme.S3, 0, 0, Theme.S2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(account);

        var signOut = new RoundedButton { Text = "Log out", Variant = ButtonVariant.Ghost, Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 38, Margin = new Padding(0), Padding = new Padding(Theme.S3, 0, Theme.S3, 0) };
        signOut.Click += async (_, _) => await SignOutAsync();
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(signOut);

        sidebar.Controls.Add(layout);
        return sidebar;
    }

    /// <summary>Switches to the page at <paramref name="index"/> (0 Apps, 1 Files, 2 Backup).</summary>
    internal void ShowPage(int index) => Show(_pages[index].Page);

    private void Show(Control page)
    {
        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(page);
        _content.ResumeLayout(true);
        foreach (var (button, p) in _pages) button.Selected = ReferenceEquals(p, page);
        if (page is IPage shown) shown.OnShown();
    }

    private async Task SignOutAsync()
    {
        if (!Ui.Confirm(this, "Log out of SaveState?", "Your backup stays in your account. You can log back in any time.", "Log out")) return;
        await _state.Api.SignOutAsync();
        SignedOut = true;
        Close();
    }

    /// <summary>Surface-colored sidebar with a hairline on its right edge.</summary>
    private sealed class SidebarPanel : Panel
    {
        public SidebarPanel()
        {
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Theme.Line);
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }
    }
}

/// <summary>Pages get a hook when they become visible (e.g. to refresh data).</summary>
internal interface IPage
{
    void OnShown();
}
