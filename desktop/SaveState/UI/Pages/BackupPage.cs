using System.Diagnostics;
using System.Globalization;
using SaveState.Core.Api;
using SaveState.Core.Backup;
using SaveState.Core.Models;
using SaveState.Services;
using SaveState.UI.Controls;

namespace SaveState.UI.Pages;

/// <summary>
/// Is my stuff backed up? A status card (when, what, how big, when it expires, or that it was
/// deleted on the website), "Back up now" / "Download backup", and a list of every file in the
/// stored backup.
/// </summary>
internal sealed class BackupPage : UserControl, IPage
{
    private readonly AppState _state;
    private readonly Banner _banner = Ui.Banner();

    // Status card
    private readonly StatusCard _status = new(Theme.H2, Theme.Body) { Margin = new Padding(0, 0, 0, Theme.S4) };
    private readonly UsageBar _stored = new() { Dock = DockStyle.Top, Caption = "Stored backup" };
    private readonly RoundedButton _backup = new() { Text = "Back up now", AutoSize = true, Glyph = "\uE898", Height = 44 };
    private readonly RoundedButton _download = new() { Text = "Download backup", Variant = ButtonVariant.Secondary, AutoSize = true, Glyph = "\uE896", Height = 44 };
    private readonly RoundedButton _cancel = new() { Text = "Cancel", Variant = ButtonVariant.Ghost, AutoSize = true, Visible = false, Height = 44 };
    private readonly Label _summary = Ui.Muted("", maxWidth: 720);
    private readonly Label _stage = Ui.Text("", Theme.BodyStrong);
    private readonly ProgressLine _progress = new() { Dock = DockStyle.Top, Visible = false };

    // What's in the backup
    private readonly Label _contentsSummary = Ui.Text("", Theme.BodySmall, Theme.InkMuted);
    private readonly CheckBox _showFiles = new() { Text = "Show every file", AutoSize = true, Font = Theme.Body, ForeColor = Theme.InkMuted, BackColor = Theme.Surface };
    private readonly ThemedGrid _grid = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _placeholder = Ui.Muted("");

    private readonly IReadOnlyList<PathRoot> _roots = PathTokens.FromEnvironment();
    private CancellationTokenSource? _work;

    public BackupPage(AppState state)
    {
        _state = state;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Bg;

        var header = Ui.Heading("Backup");
        var intro = Ui.Muted($"Back up before you reinstall. Your app list is kept for good; files are kept for {state.Api.Config.ExpiryDays} days after each upload. A new backup replaces the old one.", maxWidth: 720);
        intro.Margin = new Padding(0, Theme.S2, 0, Theme.S5);

        // Card 1: is it backed up, plus the actions.
        var current = new Card { Padding = new Padding(Theme.S5), Margin = new Padding(0, 0, 0, Theme.S4) };
        var currentLayout = Stack();
        _stored.Margin = new Padding(0, 0, 0, Theme.S4);
        _stored.Max = state.Api.Config.MaxBackupBytes;
        _stored.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, Theme.S2), WrapContents = false };
        _backup.Margin = new Padding(0, 0, Theme.S2, 0);
        _download.Margin = new Padding(0, 0, Theme.S2, 0);
        buttons.Controls.Add(_backup);
        buttons.Controls.Add(_download);
        buttons.Controls.Add(_cancel);
        _summary.Margin = new Padding(0, Theme.S1, 0, 0);
        _stage.Margin = new Padding(0, Theme.S3, 0, Theme.S2);
        _stage.Visible = false; // only while working
        _progress.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        AddRows(currentLayout, _status, _stored, buttons, _summary, _stage, _progress);
        current.FitTo(currentLayout);

        // Card 2: every file in the stored backup.
        var contents = new Card { Padding = new Padding(Theme.S5, Theme.S4, Theme.S5, Theme.S3) };
        var contentsHeader = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 2, Height = 64, BackColor = Theme.Surface };
        contentsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        contentsHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentsHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var contentsTitle = Ui.Subheading("What's in your backup");
        _contentsSummary.Margin = new Padding(0, Theme.S1, 0, 0);
        _showFiles.Margin = new Padding(Theme.S4, Theme.S1, 0, 0);
        contentsHeader.Controls.Add(contentsTitle, 0, 0);
        contentsHeader.Controls.Add(_contentsSummary, 0, 1);
        contentsHeader.Controls.Add(_showFiles, 1, 0);
        contentsHeader.SetRowSpan(_showFiles, 2);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Location", FillWeight = 62 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Files", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _placeholder.Dock = DockStyle.Fill;
        _placeholder.AutoSize = false;
        _placeholder.TextAlign = ContentAlignment.MiddleCenter;
        _placeholder.BackColor = Theme.Surface;
        _placeholder.Padding = new Padding(Theme.S6, 0, Theme.S6, 0);
        contents.Controls.Add(_grid);
        contents.Controls.Add(_placeholder);
        contents.Controls.Add(contentsHeader);
        _placeholder.BringToFront();
        contentsHeader.SendToBack(); // docked first, so it takes the top and the grid fills the rest

        Controls.Add(new PageStack().Add(header).Add(intro).Add(_banner).Add(current).Fill(contents));
        ResumeLayout(true);

        _backup.Click += async (_, _) => await BackUpAsync();
        _download.Click += async (_, _) => await DownloadAsync();
        _cancel.Click += (_, _) => _work?.Cancel();
        _showFiles.CheckedChanged += (_, _) => RenderContents();
        _state.BackupChanged += (_, _) => { if (IsHandleCreated) BeginInvoke(RenderStatus); };
        _state.SourcesChanged += (_, _) => { if (IsHandleCreated) BeginInvoke(RenderSummary); };
        RenderStatus();
        RenderSummary();
    }

    public async void OnShown()
    {
        RenderSummary();
        try
        {
            // Load apps (and with them the saved row) so the summary and overwrite warning are accurate.
            var warning = await _state.EnsureAppsLoadedAsync();
            if (warning is not null && _work is null) Ui.ShowBanner(_banner, warning, Ui.Tone.Warning);
            RenderSummary();
            if (!Program.Headless) await _state.RefreshBackupAsync(); // fresh status when you open this page
        }
        catch (ApiException e) when (e.Kind == ApiErrorKind.SessionExpired)
        {
            Program.RequestRelogin(FindForm());
        }
        catch (Exception e)
        {
            Log.Error("Loading apps on backup page failed", e);
        }
    }

    private void RenderStatus()
    {
        _status.Status = BackupStatus.From(_state);
        var backup = _state.Backup;
        var hasFiles = backup?.HasFiles == true;
        _stored.Visible = hasFiles;
        _download.Visible = hasFiles;
        if (hasFiles) _stored.Used = backup!.SizeBytes;
        RenderContents();
    }

    private void RenderSummary()
    {
        var apps = _state.AppsLoaded ? _state.Apps.Count(a => a.IsSelected) : (int?)null;
        var items = _state.Sources.Count;
        var appsText = apps is null ? "your apps" : BackupStatus.Count(apps.Value, "app");
        var verb = _state.Backup?.HasFiles == true ? "Backing up again replaces this backup and saves" : "Backing up saves";
        _summary.Text = items == 0
            ? $"{verb} {appsText}. No files picked, so only your app list is saved (add files on the Files page)."
            : $"{verb} {appsText} and {BackupStatus.Count(items, "picked item")} from the Files page, zipped into one file.";
        _backup.Text = items == 0 ? "Save app list" : "Back up now";
    }

    /// <summary>Fills the "What's in your backup" list from the server's file list.</summary>
    private void RenderContents()
    {
        var backup = _state.Backup;
        var contents = backup?.Files;
        string? empty = null;
        if (!_state.BackupLoaded) empty = "Checking your backup…";
        else if (backup is not { HasFiles: true })
            empty = backup switch
            {
                { FilesWereDeleted: true } => $"Your backup files were deleted on the website on {BackupStatus.When(backup.FilesRemovedAt!.Value)}, so nothing is stored right now.\nBack up again to store your files.",
                { FilesExpired: true } => $"Your backup files expired on {BackupStatus.When(backup.FilesRemovedAt!.Value)}, so nothing is stored right now.\nBack up again to store your files.",
                _ => "No files backed up yet. Pick files on the Files page, then back up.",
            };
        else if (contents is null) empty = "Loading the list of files…";
        else if (contents.Unavailable) empty = "This backup doesn't include a file list. Download it and open README.txt to see what's inside.";

        _showFiles.Visible = empty is null && contents!.Items.Any(i => i.IsFolder);
        _placeholder.Visible = empty is not null;
        if (empty is not null)
        {
            _placeholder.Text = empty;
            _contentsSummary.Text = "";
            _grid.ReplaceRows([]);
            return;
        }

        _contentsSummary.Text = $"{BackupStatus.Count(contents!.FileCount, "file")} from {BackupStatus.Count(contents.ItemCount, "picked item")}, {Sizes.Format(contents.TotalBytes)} before zipping";
        var rows = new List<DataGridViewRow>();
        foreach (var item in contents.Items)
        {
            rows.Add(_grid.NewRow(item, item.Path, item.IsFolder ? "Folder" : "File", item.FileCount.ToString("N0", CultureInfo.CurrentCulture), Sizes.Format(item.SizeBytes)));
            if (!_showFiles.Checked || !item.IsFolder) continue;
            foreach (var file in item.Files)
                rows.Add(FileRow(file.Path, Sizes.Format(file.SizeBytes)));
            var unlisted = item.FileCount - item.Files.Count;
            if (unlisted > 0) rows.Add(FileRow($"…and {BackupStatus.Count(unlisted, "more file")} (not listed on very big backups)", ""));
        }
        var hiddenItems = contents.ItemCount - contents.Items.Count;
        if (hiddenItems > 0) rows.Add(FileRow($"…and {BackupStatus.Count(hiddenItems, "more item")} (not listed)", ""));
        _grid.ReplaceRows(rows);
    }

    /// <summary>An indented, quieter row for a file inside a backed-up folder.</summary>
    private DataGridViewRow FileRow(string path, string size)
    {
        var row = _grid.NewRow(null, path, "", "", size);
        row.DefaultCellStyle.ForeColor = Theme.InkMuted;
        row.DefaultCellStyle.Font = Theme.BodySmall;
        row.Cells[0].Style.Padding = new Padding(36, 0, 8, 0);
        return row;
    }

    private async Task BackUpAsync()
    {
        if (_work is not null) return;
        Ui.HideBanner(_banner);

        // Never upload before we know the saved list, or an offline start could wipe it.
        string? warning;
        try { warning = await _state.EnsureAppsLoadedAsync(); }
        catch (ApiException e) when (e.Kind == ApiErrorKind.SessionExpired) { Program.RequestRelogin(FindForm()); return; }
        if (!_state.AppsLoaded)
        {
            Ui.ShowBanner(_banner, warning ?? "Couldn't load your saved app list. Check your connection and try again.", Ui.Tone.Error);
            return;
        }

        var apps = _state.SelectedApps;
        var sources = _state.Sources.ToList();
        var existing = _state.Backup;

        if (sources.Count > 0 && existing is { HasFiles: true, UploadedAt: { } uploaded } &&
            !Ui.Confirm(FindForm(), "Replace your current backup?",
                $"Your backup from {uploaded.ToLocalTime():d MMMM} ({Sizes.Format(existing.SizeBytes)}) will be replaced by this new one.",
                "Replace backup", warning: true))
            return;

        var work = _work = new CancellationTokenSource();
        SetBusy(true);
        string? zipPath = null;
        try
        {
            if (sources.Count == 0)
            {
                SetStage("Saving app list…", null);
                var row = await _state.Api.SaveAppsAsync(apps, work.Token);
                _state.SetBackup(row);
                Ui.ShowBanner(_banner, $"Saved {apps.Count} {(apps.Count == 1 ? "app" : "apps")} to your account.", Ui.Tone.Success);
                return;
            }

            SetStage("Checking your files…", null);
            var plan = await Task.Run(() => BackupBuilder.Plan(sources, work.Token), work.Token);
            if (plan.FileCount == 0)
            {
                Ui.ShowBanner(_banner, "None of the picked files exist anymore. Check the Files page.", Ui.Tone.Error);
                return;
            }

            zipPath = Path.Combine(AppPaths.TempDir, $"backup-{Guid.NewGuid():N}.zip");
            var builder = new BackupBuilder(_roots, $"SaveState {Application.ProductVersion.Split('+')[0]}");
            var buildProgress = new Progress<BuildProgress>(p => SetStage($"Zipping {plan.FileCount:N0} files…", p.Fraction));
            var result = await Task.Run(() => builder.Build(plan, apps, zipPath, buildProgress, work.Token), work.Token);

            var max = _state.Api.Config.MaxBackupBytes;
            if (result.ZipBytes > max)
            {
                Ui.ShowBanner(_banner,
                    $"Your backup is {Sizes.Format(result.ZipBytes)} zipped, over the {Sizes.Format(max)} limit. Remove about {Sizes.Format(result.ZipBytes - max)} on the Files page and try again. Nothing was uploaded.",
                    Ui.Tone.Error);
                return;
            }

            var uploadProgress = new Progress<TransferProgress>(p => SetStage(
                p.Stage == "Uploading" ? $"Uploading {Sizes.Format(p.DoneBytes)} of {Sizes.Format(p.TotalBytes)}…" : $"{p.Stage}…",
                p.Stage == "Uploading" ? p.Fraction : null));
            var saved = await _state.Transfer.UploadAsync(zipPath, apps, uploadProgress, work.Token);
            _state.SetBackup(saved);

            var skipped = plan.Missing.Count > 0 ? $" {plan.Missing.Count} missing {(plan.Missing.Count == 1 ? "item was" : "items were")} skipped." : "";
            Ui.ShowBanner(_banner,
                $"Backed up {apps.Count} apps and {result.FileCount:N0} files ({Sizes.Format(result.ZipBytes)}). Files are kept until {saved.ExpiresAt?.ToLocalTime():d MMMM}.{skipped}",
                Ui.Tone.Success);
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested)
        {
            Ui.ShowBanner(_banner, "Backup cancelled. Nothing was changed.", Ui.Tone.Info);
        }
        catch (FileLockedException e)
        {
            Ui.ShowBanner(_banner, e.Message, Ui.Tone.Error);
        }
        catch (BackupBuildException e)
        {
            Ui.ShowBanner(_banner, e.Message, Ui.Tone.Error);
        }
        catch (ApiException e)
        {
            Ui.ShowBanner(_banner, e.Message, Ui.Tone.Error);
            if (e.Kind == ApiErrorKind.SessionExpired) Program.RequestRelogin(FindForm());
        }
        catch (IOException e) when (e.HResult == unchecked((int)0x80070070)) // ERROR_DISK_FULL
        {
            Ui.ShowBanner(_banner, "Your disk is full, so the backup zip couldn't be created. Free up some space and try again.", Ui.Tone.Error);
        }
        catch (Exception e)
        {
            Log.Error("Backup failed", e);
            Ui.ShowBanner(_banner, Ui.Describe(e), Ui.Tone.Error);
        }
        finally
        {
            if (zipPath is not null) { try { File.Delete(zipPath); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            _work = null;
            work.Dispose();
            SetBusy(false);
            RenderSummary();
        }
    }

    private async Task DownloadAsync()
    {
        if (_work is not null) return;
        Ui.HideBanner(_banner);
        var date = _state.Backup?.UploadedAt?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "latest";
        using var dialog = new SaveFileDialog
        {
            Title = "Save your backup",
            FileName = $"SaveState-backup-{date}.zip",
            Filter = "Zip archive (*.zip)|*.zip",
            InitialDirectory = KnownFolders.Downloads,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

        var work = _work = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            var progress = new Progress<TransferProgress>(p => SetStage($"Downloading {Sizes.Format(p.DoneBytes)} of {Sizes.Format(p.TotalBytes)}…", p.Fraction));
            SetStage("Preparing download…", null);
            await _state.Transfer.DownloadAsync(dialog.FileName, progress, work.Token);
            Ui.ShowBanner(_banner, $"Saved to {dialog.FileName}", Ui.Tone.Success);
            try { Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\""); } catch (Exception) { /* optional nicety */ }
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested)
        {
            Ui.ShowBanner(_banner, "Download cancelled.", Ui.Tone.Info);
        }
        catch (ApiException e)
        {
            Ui.ShowBanner(_banner, e.Message, Ui.Tone.Error);
            if (e.Code == "expired") _state.SetBackup(await SafeReloadAsync());
            if (e.Kind == ApiErrorKind.SessionExpired) Program.RequestRelogin(FindForm());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Ui.ShowBanner(_banner, $"Couldn't save the file there: {e.Message}", Ui.Tone.Error);
        }
        finally
        {
            _work = null;
            work.Dispose();
            SetBusy(false);
        }
    }

    private async Task<Core.Models.BackupRow?> SafeReloadAsync()
    {
        try { return await _state.Api.GetBackupAsync(); }
        catch (ApiException) { return _state.Backup; }
    }

    private void SetBusy(bool busy)
    {
        _state.TransferRunning = busy;
        _backup.Enabled = !busy;
        _download.Enabled = !busy;
        _cancel.Visible = busy;
        _progress.Visible = busy;
        _stage.Visible = busy;
        if (!busy) _progress.Value = null;
        UseWaitCursor = busy;
    }

    private void SetStage(string text, double? fraction)
    {
        _stage.Text = text;
        _progress.Value = fraction;
    }

    private static TableLayoutPanel Stack()
    {
        // Auto-sizes vertically only; width comes from the card (docked Top).
        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static void AddRows(TableLayoutPanel layout, params Control[] controls)
    {
        foreach (var c in controls)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(c);
        }
    }
}

/// <summary>Thin rounded progress line. Null value = indeterminate (a calm moving segment).</summary>
internal sealed class ProgressLine : Control
{
    private double? _value;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };
    private float _phase;

    public ProgressLine()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 8;
        AccessibleRole = AccessibleRole.ProgressBar;
        _timer.Tick += (_, _) => { _phase = (_phase + 0.012f) % 1.4f; Invalidate(); };
    }

    public double? Value
    {
        get => _value;
        set
        {
            _value = value;
            // Respect Windows' "Show animations" setting: no moving segment when it's off.
            _timer.Enabled = value is null && Visible && SystemInformation.UIEffectsEnabled;
            AccessibleName = value is null ? "Working" : $"{Math.Round(value.Value * 100)}%";
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        _timer.Enabled = Visible && _value is null && SystemInformation.UIEffectsEnabled;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        Theme.HighQuality(g);
        var h = Height - 1f;
        using (var track = Theme.RoundedRect(new RectangleF(0, 0, Width - 1, h), h / 2))
        using (var brush = new SolidBrush(Theme.SurfaceSunk))
            g.FillPath(brush, track);

        RectangleF fill;
        if (_value is { } v) fill = new RectangleF(0, 0, Math.Max(h, (float)v * (Width - 1)), h);
        else
        {
            var seg = Width * 0.3f;
            var x = (_phase - 0.3f) * Width;
            fill = RectangleF.Intersect(new RectangleF(x, 0, seg, h), new RectangleF(0, 0, Width - 1, h));
            if (fill.Width <= 0) return;
        }
        using var path = Theme.RoundedRect(fill, h / 2);
        using var accent = new SolidBrush(Theme.Accent);
        g.FillPath(accent, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
