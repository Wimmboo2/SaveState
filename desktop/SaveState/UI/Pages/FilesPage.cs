using SaveState.Core.Backup;
using SaveState.Core.Presets;
using SaveState.Services;
using SaveState.UI.Controls;

namespace SaveState.UI.Pages;

/// <summary>
/// Pick files and folders to include (with one-click presets), see sizes and a running total
/// against the 100 MB limit.
/// </summary>
internal sealed class FilesPage : UserControl, IPage
{
    private readonly AppState _state;
    private readonly IReadOnlyList<PathRoot> _roots = PathTokens.FromEnvironment();
    private readonly ThemedGrid _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = true };
    private readonly Label _placeholder = Ui.Muted("");
    private readonly Banner _banner = Ui.Banner();
    private readonly UsageBar _usage = new() { Caption = "Selected files" };
    private readonly Label _usageNote = Ui.Text("Sizes are before compression. The zip is usually a bit smaller.", Theme.BodySmall, Theme.InkMuted);
    private readonly RoundedButton _remove = new() { Text = "Remove", Variant = ButtonVariant.Ghost, Enabled = false, AutoSize = true };
    private CancellationTokenSource? _measure;

    private const int ColPath = 0, ColKind = 1, ColCount = 2, ColSize = 3;

    public FilesPage(AppState state)
    {
        _state = state;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Bg;

        var header = Ui.Heading("Files");
        var intro = Ui.Muted($"Pick the small files and folders that make your setup yours: game configs, resource packs, mods, settings. Everything together has to fit in {Sizes.Format(state.Api.Config.MaxBackupBytes)}.", maxWidth: 720);
        intro.Margin = new Padding(0, Theme.S2, 0, Theme.S5);

        // One row, no wrapping: a wrapping auto-size FlowLayoutPanel inside an auto-size table row can
        // send WinForms into an endless layout loop.
        var toolbar = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, Theme.S4) };
        var addFiles = new RoundedButton { Text = "Add files", Variant = ButtonVariant.Secondary, AutoSize = true, Glyph = "", Margin = new Padding(0, 0, Theme.S2, Theme.S2) };
        var addFolder = new RoundedButton { Text = "Add folder", Variant = ButtonVariant.Secondary, AutoSize = true, Glyph = "", Margin = new Padding(0, 0, Theme.S4, Theme.S2) };
        toolbar.Controls.Add(addFiles);
        toolbar.Controls.Add(addFolder);
        foreach (var preset in Presets.All)
        {
            var button = new RoundedButton { Text = $"Add {preset.Name}", Variant = ButtonVariant.Secondary, AutoSize = true, Glyph = "", Margin = new Padding(0, 0, Theme.S2, Theme.S2) };
            if (Environment.GetEnvironmentVariable("SAVESTATE_EXP_NOTOOLTIP") != "1")
                new ToolTip().SetToolTip(button, preset.Description);
            button.Click += (_, _) => AddPreset(preset);
            toolbar.Controls.Add(button);
        }
        _remove.Margin = new Padding(Theme.S4, 0, 0, Theme.S2);
        toolbar.Controls.Add(_remove);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Location", FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Files", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });

        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(Theme.S3, Theme.S2, Theme.S3, Theme.S2) };
        _placeholder.Dock = DockStyle.Fill;
        _placeholder.AutoSize = false;
        _placeholder.TextAlign = ContentAlignment.MiddleCenter;
        _placeholder.BackColor = Theme.Surface;
        _placeholder.Text = "Nothing picked yet. Add files or folders, or use a preset.";
        card.Controls.Add(_grid);
        card.Controls.Add(_placeholder);
        _placeholder.BringToFront();

        // Table, not docking: an AutoSize label that's also docked fights its parent's layout
        // (it made this page crawl, and occasionally freeze).
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Bg, Padding = new Padding(0, Theme.S5, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _usage.Dock = DockStyle.None;
        _usage.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _usage.Margin = new Padding(0, 0, 0, Theme.S1);
        _usageNote.Margin = new Padding(0);
        footer.Controls.Add(_usage);
        footer.Controls.Add(_usageNote);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Bg };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var c in new Control[] { header, intro, toolbar, _banner })
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(c);
        }
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(card);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.Controls.Add(footer);
        if (Environment.GetEnvironmentVariable("SAVESTATE_EXP_NOFOOTER") == "1") footer.Visible = false;
        if (Environment.GetEnvironmentVariable("SAVESTATE_EXP_NOTOOLBAR") == "1") toolbar.Visible = false;
        if (Environment.GetEnvironmentVariable("SAVESTATE_EXP_NOGRID") == "1") card.Visible = false;
        Controls.Add(root);
        ResumeLayout(true);

        _usage.Max = state.Api.Config.MaxBackupBytes;
        addFiles.Click += (_, _) => PickFiles();
        addFolder.Click += (_, _) => PickFolder();
        _remove.Click += (_, _) => RemoveSelected();
        _grid.SelectionChanged += (_, _) => _remove.Enabled = _grid.SelectedRows.Count > 0 && _state.Sources.Count > 0;
        _grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
        _state.SourcesChanged += (_, _) => { if (IsHandleCreated) _ = MeasureAsync(); };
    }

    public void OnShown() => _ = MeasureAsync();

    private void PickFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add files to your backup",
            Multiselect = true,
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) Add(dialog.FileNames);
    }

    private void PickFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Add a folder to your backup",
            UseDescriptionForTitle = true,
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) Add(dialog.SelectedPaths);
    }

    private void AddPreset(Preset preset)
    {
        var found = Presets.ResolveExisting(preset);
        if (found.Count == 0)
        {
            Ui.ShowBanner(_banner, $"No {preset.Name} files found on this PC (looked in {Path.GetDirectoryName(preset.Paths[0])}).", Ui.Tone.Info);
            return;
        }
        var added = Add(found);
        Ui.ShowBanner(_banner, added == 0
            ? $"Your {preset.Name} files are already in the list."
            : $"Added {added} {preset.Name} {(added == 1 ? "item" : "items")}.", Ui.Tone.Success);
    }

    private int Add(IEnumerable<string> paths)
    {
        var before = _state.Sources.Count;
        _state.SetSources(_state.Sources.Concat(paths));
        return _state.Sources.Count - before;
    }

    private void RemoveSelected()
    {
        var remove = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as string).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (remove.Count == 0) return;
        _state.SetSources(_state.Sources.Where(s => !remove.Contains(s)));
        Ui.HideBanner(_banner);
    }

    /// <summary>Measures every picked path in the background and refreshes the list and total.</summary>
    private async Task MeasureAsync()
    {
        _measure?.Cancel();
        var cts = _measure = new CancellationTokenSource();
        var sources = _state.Sources.ToList();

        Screenshots.Trace("files: render placeholder rows");
        RenderRows(sources, plan: null);
        try
        {
            Screenshots.Trace("files: measuring");
            var plan = await Task.Run(() => BackupBuilder.Plan(sources, cts.Token), cts.Token);
            if (cts.IsCancellationRequested) return;
            if (InvokeRequired)
            {
                // Never touch controls from a background thread (it can hang or crash painting).
                Screenshots.Trace("files: continuation came back OFF the UI thread; marshalling");
                BeginInvoke(() => RenderRows(sources, plan));
                return;
            }
            Screenshots.Trace("files: render measured rows");
            RenderRows(sources, plan);
            Screenshots.Trace("files: rendered");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Log.Error("Measuring files failed", e);
            Ui.ShowBanner(_banner, "Couldn't measure some of the files. " + Ui.Describe(e), Ui.Tone.Warning);
        }
    }

    private void RenderRows(List<string> sources, BackupPlan? plan)
    {
        var rows = new List<DataGridViewRow>();
        foreach (var source in sources)
        {
            var item = plan?.Items.FirstOrDefault(i => string.Equals(i.SourcePath, source, StringComparison.OrdinalIgnoreCase));
            var missing = plan?.Missing.Contains(source, StringComparer.OrdinalIgnoreCase) ?? false;
            string kind, count, size;
            if (plan is null) (kind, count, size) = ("", "", "…");
            else if (missing) (kind, count, size) = ("Missing", "", "");
            else if (item is null) (kind, count, size) = ("Included above", "", ""); // already covered by a picked folder
            else (kind, count, size) = (item.IsFolder ? "Folder" : "File", item.Files.Count.ToString("N0"), Sizes.Format(item.SizeBytes));

            var row = _grid.NewRow(source, PathTokens.Tokenize(source, _roots), kind, count, size);
            if (missing || item is null && plan is not null)
                row.DefaultCellStyle.ForeColor = Theme.InkSubtle;
            rows.Add(row);
        }
        Screenshots.Trace("files: rows built");
        _grid.ReplaceRows(rows);
        Screenshots.Trace("files: rows replaced");

        _placeholder.Visible = sources.Count == 0;
        _usage.Used = plan?.TotalBytes ?? _usage.Used;
        if (sources.Count == 0) _usage.Used = 0;
        Screenshots.Trace("files: usage set");

        if (plan is not null && Environment.GetEnvironmentVariable("SAVESTATE_EXP_FORCEBANNER") == "1")
            Ui.ShowBanner(_banner, "Experiment: a banner on the Files page.", Ui.Tone.Warning);
        else if (plan is { Missing.Count: > 0 } && Environment.GetEnvironmentVariable("SAVESTATE_EXP_NOBANNER") == "1") { }
        else if (plan is { Missing.Count: > 0 })
            Ui.ShowBanner(_banner, $"{plan.Missing.Count} {(plan.Missing.Count == 1 ? "item doesn't" : "items don't")} exist anymore and will be skipped. Select and remove {(plan.Missing.Count == 1 ? "it" : "them")} to tidy up.", Ui.Tone.Warning);
        else if (plan is not null && plan.TotalBytes > _state.Api.Config.MaxBackupBytes)
            Ui.ShowBanner(_banner, $"That's {Sizes.Format(plan.TotalBytes - _state.Api.Config.MaxBackupBytes)} over the limit. Remove something big, or it may not fit after zipping.", Ui.Tone.Warning);
    }
}
