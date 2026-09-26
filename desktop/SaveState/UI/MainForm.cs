using SaveState.UI.Controls;
using SaveState.UI.Pages;

namespace SaveState.UI;

/// <summary>Main window: sidebar navigation (Apps, Files, Backup) plus the current page.</summary>
internal sealed class MainForm : Form
{
    private readonly AppState _state;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    private readonly List<(RoundedButton Button, Control Page)> _pages = [];
    private readonly StatusCard _status = new() { Clickable = true, Margin = new Padding(0, Theme.S5, 0, 0) };

    // Picks up changes made elsewhere (files deleted on the website, expiry) while the app is open.
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 60_000 };

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
        _firstPage = apps;

        _status.Status = BackupStatus.From(state);
        _status.Click += (_, _) => Show(backup);
        state.BackupChanged += (_, _) => { if (IsHandleCreated) BeginInvoke(() => _status.Status = BackupStatus.From(_state)); };
        _refreshTimer.Tick += (_, _) => { if (WindowState != FormWindowState.Minimized) _ = _state.RefreshBackupAsync(); };
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Coming back to the app (e.g. after deleting files on the website) re-checks the backup.
        if (!Program.Headless) _ = _state.RefreshBackupAsync();
    }

    private readonly Control _firstPage;

    /// <summary>
    /// Pages start loading (async) only once the window is on screen and its message loop is
    /// running, so their awaits resume on the UI thread.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_content.Controls.Count == 0) Show(_firstPage);
        if (!Program.Headless) _refreshTimer.Start();
    }

    private Control BuildSidebar(params (string Label, string Glyph, Control Page)[] pages)
    {
        var sidebar = new SidebarPanel { Dock = DockStyle.Left, Width = 232, Padding = new Padding(Theme.S4, Theme.S5, Theme.S4, Theme.S4) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var brand = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Surface, Margin = new Padding(Theme.S3, 0, 0, Theme.S6) };
        var iconSize = LogicalToDeviceUnits(28);
        if (AppIcon.Load() is { } icon)
        {
            brand.Controls.Add(new PictureBox
            {
                Image = new Icon(icon, 256, 256).ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(iconSize, iconSize),
                Margin = new Padding(0, 0, Theme.S2, 0),
            });
        }
        var wordmark = Ui.Text("SaveState", Theme.Wordmark);
        wordmark.Margin = new Padding(0, (iconSize - TextRenderer.MeasureText("S", Theme.Wordmark).Height) / 2, 0, 0);
        brand.Controls.Add(wordmark);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(brand);

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

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_status);

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface });

        var account = Ui.Text(_state.Api.Session?.Email ?? "", Theme.BodySmall, Theme.InkMuted, maxWidth: 190);
        account.AutoEllipsis = true;
        account.Margin = new Padding(Theme.S3, 0, 0, Theme.S2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(account);

        var signOut = new RoundedButton { Text = "Log out", Variant = ButtonVariant.Nav, Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 38, Margin = new Padding(0), Padding = new Padding(Theme.S3, 0, Theme.S3, 0) };
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
        Screenshots.Trace($"main: swapping to {page.GetType().Name}");
        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(page);
        _content.ResumeLayout(true);
        Screenshots.Trace("main: layout done");
        foreach (var (button, p) in _pages) button.Selected = ReferenceEquals(p, page);
        if (page is IPage shown) shown.OnShown();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshTimer.Dispose();
        base.Dispose(disposing);
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
