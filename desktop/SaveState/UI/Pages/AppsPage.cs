using SaveState.Core.Api;
using SaveState.Core.Apps;
using SaveState.Services;
using SaveState.UI.Controls;

namespace SaveState.UI.Pages;

/// <summary>
/// Installed-app checklist: search, tick the apps to remember, add a note per app, save the list.
/// Previously saved apps come back ticked with their notes.
/// </summary>
internal sealed class AppsPage : UserControl, IPage
{
    private readonly AppState _state;
    private readonly InputBox _search = new() { Width = 320, PlaceholderText = "Search apps, publishers or notes", AccessibleName = "Search apps" };
    private readonly CheckBox _selectedOnly = new() { Text = "Selected only", AutoSize = true, Font = Theme.Body, ForeColor = Theme.InkMuted, Margin = new Padding(Theme.S4, 10, 0, 0) };
    private readonly Label _count = Ui.Muted("");
    private readonly RoundedButton _save = new() { Text = "Save app list", Enabled = false };
    private readonly Label _banner = Ui.Banner();
    private readonly ThemedGrid _grid = new() { Dock = DockStyle.Fill };
    private readonly Label _placeholder = Ui.Muted("Looking for installed apps…");
    private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 160 };
    private bool _loaded;
    private bool _loading;

    private const int ColSelect = 0, ColName = 1, ColPublisher = 2, ColVersion = 3, ColNote = 4;

    public AppsPage(AppState state)
    {
        _state = state;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Bg;

        var header = Ui.Heading("Apps");
        var intro = Ui.Muted("Tick the apps you want to remember for after the reinstall. Add a note for anything you'll want to know, like a version or a setting.", maxWidth: 720);
        intro.Margin = new Padding(0, Theme.S2, 0, Theme.S5);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, AutoSize = true, Margin = new Padding(0, 0, 0, Theme.S4), BackColor = Theme.Bg };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _search.Margin = new Padding(0);
        _count.Margin = new Padding(0, 11, Theme.S4, 0);
        _save.Margin = new Padding(0);
        _save.AutoSize = true;
        toolbar.Controls.Add(_search, 0, 0);
        toolbar.Controls.Add(_selectedOnly, 1, 0);
        toolbar.Controls.Add(new Panel { Width = 1, Height = 1, BackColor = Theme.Bg }, 2, 0);
        toolbar.Controls.Add(_count, 3, 0);
        toolbar.Controls.Add(_save, 4, 0);

        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(Theme.S3, Theme.S2, Theme.S3, Theme.S2) };
        _placeholder.Dock = DockStyle.Fill;
        _placeholder.AutoSize = false;
        _placeholder.TextAlign = ContentAlignment.MiddleCenter;
        _placeholder.BackColor = Theme.Surface;
        card.Controls.Add(_grid);
        card.Controls.Add(_placeholder);
        _placeholder.BringToFront();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Bg };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var c in new Control[] { header, intro, toolbar, _banner })
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(c);
        }
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(card);
        Controls.Add(root);

        BuildColumns();
        ResumeLayout(true);

        _search.TextChanged += (_, _) => { _searchDebounce.Stop(); _searchDebounce.Start(); };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); RenderRows(); };
        _selectedOnly.CheckedChanged += (_, _) => RenderRows();
        _save.Click += async (_, _) => await SaveAsync();

        // Commit checkbox clicks immediately instead of when the cell loses focus.
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += OnCellValueChanged;
        _grid.KeyDown += (_, e) =>
        {
            // Space toggles the tick on the current row even when the note cell is focused.
            if (e.KeyCode == Keys.Space && _grid.CurrentRow is { } row && _grid.CurrentCell?.ColumnIndex != ColNote)
            {
                row.Cells[ColSelect].Value = !(bool)(row.Cells[ColSelect].Value ?? false);
                e.Handled = true;
            }
        };
    }

    public void OnShown()
    {
        if (!_loading && (!_loaded || !_state.AppsLoaded)) _ = LoadAsync();
        else if (_loaded) RenderRows(); // pick up changes made elsewhere
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Save", Width = 64, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Resizable = DataGridViewTriState.False });
        _grid.CellPainting += PaintCheckbox;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", ReadOnly = true, FillWeight = 32 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Publisher", ReadOnly = true, FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Version", ReadOnly = true, FillWeight = 12, MinimumWidth = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Note (optional)", FillWeight = 36, MaxInputLength = 500 });
        _grid.Columns[ColNote].DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Theme.InkMuted, NullValue = "" };
        _grid.Columns[ColName].DefaultCellStyle = new DataGridViewCellStyle { Font = Theme.BodyStrong };
    }

    /// <summary>Draws the "Save" tick as a soft moss checkbox instead of the system-blue one.</summary>
    private void PaintCheckbox(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColSelect || e.Graphics is null) return;
        e.PaintBackground(e.CellBounds, true);
        var g = e.Graphics;
        Theme.HighQuality(g);
        var size = _grid.LogicalToDeviceUnits(18);
        var box = new RectangleF(e.CellBounds.X + (e.CellBounds.Width - size) / 2f, e.CellBounds.Y + (e.CellBounds.Height - size) / 2f, size, size);
        var ticked = e.Value is true;
        using (var path = Theme.RoundedRect(box, _grid.LogicalToDeviceUnits(5)))
        {
            if (ticked)
            {
                using var fill = new SolidBrush(Theme.Accent);
                g.FillPath(fill, path);
            }
            else
            {
                using var fill = new SolidBrush(Theme.Surface);
                using var border = new Pen(Theme.LineStrong, _grid.DeviceDpi / 96f * 1.5f);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }
        if (ticked)
        {
            using var pen = new Pen(Theme.OnAccent, _grid.DeviceDpi / 96f * 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            g.DrawLines(pen, [
                new PointF(box.X + box.Width * 0.26f, box.Y + box.Height * 0.52f),
                new PointF(box.X + box.Width * 0.44f, box.Y + box.Height * 0.70f),
                new PointF(box.X + box.Width * 0.76f, box.Y + box.Height * 0.32f),
            ]);
        }
        e.Handled = true;
    }

    private async Task LoadAsync()
    {
        _loading = true;
        Ui.HideBanner(_banner);
        _placeholder.Text = "Looking for installed apps…";
        _placeholder.Visible = true;
        _save.Enabled = false;
        try
        {
            var warning = await _state.EnsureAppsLoadedAsync();
            if (warning is not null)
                Ui.ShowBanner(_banner, $"{warning} Your previously saved apps and notes will appear once you're back online.", Ui.Tone.Warning);
            _loaded = true;
            RenderRows();
        }
        catch (ApiException e) when (e.Kind == ApiErrorKind.SessionExpired)
        {
            Program.RequestRelogin(FindForm());
        }
        catch (Exception e)
        {
            Log.Error("Loading apps failed", e);
            _placeholder.Text = "Couldn't read the list of installed apps. Try restarting SaveState.";
        }
        finally
        {
            _loading = false;
            UpdateCount();
        }
    }

    private void RenderRows()
    {
        var query = _search.Text;
        var visible = _state.Apps
            .Where(a => a.Matches(query))
            .Where(a => !_selectedOnly.Checked || a.IsSelected)
            .ToList();

        _grid.CellValueChanged -= OnCellValueChanged;
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var app in visible)
        {
            var version = app.IsInstalled ? app.Version ?? "" : "not installed";
            var index = _grid.Rows.Add(app.IsSelected, app.Name, app.Publisher ?? "", version, app.Note);
            var row = _grid.Rows[index];
            row.Tag = app;
            if (!app.IsInstalled) row.Cells[ColVersion].Style.ForeColor = Theme.InkSubtle;
        }
        _grid.ResumeLayout();
        _grid.ClearSelection(); // don't start with a highlighted row that looks "chosen"
        _grid.CellValueChanged += OnCellValueChanged;

        _placeholder.Visible = visible.Count == 0;
        if (visible.Count == 0)
            _placeholder.Text = _state.Apps.Count == 0 ? "No installed apps found." : "No apps match your search.";
        UpdateCount();
    }

    private void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Tag is not AppListItem app) return;
        var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (e.ColumnIndex == ColSelect) app.IsSelected = cell.Value is true;
        else if (e.ColumnIndex == ColNote)
        {
            app.Note = (cell.Value as string ?? "").Trim();
            // Writing a note implies you want to keep the app.
            if (app.Note.Length > 0 && !app.IsSelected)
            {
                app.IsSelected = true;
                _grid.Rows[e.RowIndex].Cells[ColSelect].Value = true;
            }
        }
        UpdateCount();
    }

    private void UpdateCount()
    {
        var selected = _state.Apps.Count(a => a.IsSelected);
        _count.Text = $"{selected} of {_state.Apps.Count} selected";
        _save.Enabled = _loaded && !_loading;
    }

    private async Task SaveAsync()
    {
        _grid.EndEdit();
        _save.Enabled = false;
        _save.Text = "Saving…";
        Ui.HideBanner(_banner);
        try
        {
            var row = await _state.Api.SaveAppsAsync(_state.SelectedApps);
            _state.SetBackup(row);
            Ui.ShowBanner(_banner, $"Saved {row.Apps.Count} apps to your account. You can see them on the website any time.", Ui.Tone.Success);
        }
        catch (ApiException e)
        {
            Ui.ShowBanner(_banner, e.Message, Ui.Tone.Error);
            if (e.Kind == ApiErrorKind.SessionExpired) Program.RequestRelogin(FindForm());
        }
        finally
        {
            _save.Text = "Save app list";
            _save.Enabled = true;
        }
    }
}
