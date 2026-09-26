using System.Drawing.Imaging;
using SaveState.Core.Api;
using SaveState.Core.Apps;
using SaveState.Core.Config;
using SaveState.Core.Models;

namespace SaveState.UI;

/// <summary>
/// `SaveState.exe --screenshots &lt;dir&gt;` renders each screen with sample data to PNG files.
/// Used by CI to review the UI without a person at the keyboard. Touches no real account or files
/// (sample files are created in a temp folder and removed afterwards).
/// </summary>
internal static class Screenshots
{
    private static string? _logPath;

    /// <summary>CI fails if any screen or control takes longer than this to paint (UI-freeze guard).</summary>
    private const long SlowPaintLimitMs = 2000;

    /// <summary>Renders all screens. Returns the process exit code (0 = all captured).</summary>
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        _logPath = Path.Combine(outDir, "screenshots.log");
        Note("start");

        // Watchdog: CI must never hang on a stuck UI.
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(150));
            Note("watchdog: timed out, exiting");
            Environment.Exit(3);
        }) { IsBackground = true };
        watchdog.Start();

        try
        {
            RunCore(outDir);
            Note(_failed ? "done with errors" : "done");
            return _failed ? 1 : 0;
        }
        catch (Exception e)
        {
            Fail(e);
            return 1;
        }
    }

    /// <summary>Records an unexpected error without showing any UI.</summary>
    public static void Fail(Exception? e)
    {
        _failed = true;
        Note($"ERROR {e}");
    }

    private static bool _failed;

    /// <summary>Progress trace (only written in --screenshots mode).</summary>
    public static void Trace(string line) { if (Program.Headless) Note(line); }

    private static void Note(string line)
    {
        Console.Error.WriteLine(line);
        if (_logPath is not null)
        {
            try { File.AppendAllText(_logPath, $"{DateTime.UtcNow:HH:mm:ss.fff} {line}{Environment.NewLine}"); } catch (IOException) { }
        }
    }

    private static void RunCore(string outDir)
    {
        var sample = Directory.CreateTempSubdirectory("savestate-demo-");
        try
        {
            var packs = Directory.CreateDirectory(Path.Combine(sample.FullName, ".minecraft", "resourcepacks"));
            File.WriteAllBytes(Path.Combine(packs.FullName, "Faithful 32x.zip"), new byte[18_400_000]);
            File.WriteAllBytes(Path.Combine(packs.FullName, "Fresh Animations.zip"), new byte[6_100_000]);
            var mods = Directory.CreateDirectory(Path.Combine(sample.FullName, ".minecraft", "mods"));
            File.WriteAllBytes(Path.Combine(mods.FullName, "sodium.jar"), new byte[1_200_000]);
            File.WriteAllText(Path.Combine(sample.FullName, ".minecraft", "options.txt"), "fov:0.25");

            using var http = new HttpClient();
            var store = new InMemorySessionStore();
            store.Save(new Session("demo", "demo", DateTimeOffset.UtcNow.AddHours(1), "demo", "wim@example.com"));
            var api = new SupabaseApi(http, AppConfig.Default, store);
            var state = new AppState(api, http);
            state.LoadDemo(SampleApps(), new BackupRow
            {
                Apps = SampleApps().Where(a => a.IsSelected).Select(a => a.ToSavedApp()).ToList(),
                FilePath = "demo/backup.zip",
                SizeBytes = 26_214_400,
                UploadedAt = DateTimeOffset.UtcNow.AddDays(-7),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(23).AddHours(2),
            }, [packs.FullName, mods.FullName, Path.Combine(sample.FullName, ".minecraft", "options.txt"), @"D:\Games\OldSave.sav"]);

            Note("state ready");
            BannerExperiment(accessible: false);
            BannerExperiment(accessible: true);
            // Experiment: whole run with banner accessibility updates off.
            Controls.Banner.UpdateAccessibility = Environment.GetEnvironmentVariable("SAVESTATE_BANNER_A11Y") != "0";
            Note($"  banner accessibility for pages: {Controls.Banner.UpdateAccessibility}");
            using (var login = new LoginForm(api))
                Capture(login, Path.Combine(outDir, "login.png"));
            Note("login captured");

            using var main = new MainForm(state);
            string[] names = ["apps", "files", "backup"];
            for (var i = 0; i < names.Length; i++)
            {
                if (i == 0) main.Show();
                Note($"showing {names[i]}");
                main.ShowPage(i);
                Note($"shown {names[i]}");
                Capture(main, Path.Combine(outDir, $"{names[i]}.png"), show: false);
                Note($"{names[i]} captured");
            }
            main.Hide();
        }
        finally
        {
            try { sample.Delete(recursive: true); } catch (IOException) { }
        }
    }

    private static void Capture(Form form, string path, bool show = true)
    {
        if (show) form.Show();
        // Let layout, async measuring and painting settle.
        var until = DateTime.UtcNow.AddSeconds(2);
        var pumps = 0;
        while (DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(30);
            if (++pumps % 20 == 0) Note($"  pumping {path} ({pumps})");
        }
        // Diagnostics: how long does a real repaint take (what users see), vs. the bitmap capture?
        TimeSlowControls(form, depth: 0); // first paint of each control, one by one
        var sw = System.Diagnostics.Stopwatch.StartNew();
        form.Refresh();
        Note($"  refresh took {sw.ElapsedMilliseconds} ms");
        if (sw.ElapsedMilliseconds > SlowPaintLimitMs)
            Fail(new TimeoutException($"Repainting {Path.GetFileNameWithoutExtension(path)} took {sw.ElapsedMilliseconds} ms"));
        Note($"  drawing {path}");
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path, ImageFormat.Png);
        if (show) form.Hide();
    }

    /// <summary>Times showing + painting one banner, with or without accessibility updates.</summary>
    private static void BannerExperiment(bool accessible)
    {
        Controls.Banner.UpdateAccessibility = accessible;
        using var form = new Form { Width = 800, Height = 200, BackColor = Theme.Bg };
        var banner = Ui.Banner();
        banner.Width = 700;
        form.Controls.Add(banner);
        form.Show();
        Application.DoEvents();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        banner.Show("1 item doesn't exist anymore and will be skipped. Select and remove it to tidy up.", Ui.Tone.Warning);
        var showMs = sw.ElapsedMilliseconds;
        sw.Restart();
        form.Refresh();
        Note($"  banner experiment accessible={accessible}: show {showMs} ms, paint {sw.ElapsedMilliseconds} ms");
        form.Hide();
    }

    /// <summary>Times each control's first paint, deepest first, and logs slow ones (finds slow painters).</summary>
    private static void TimeSlowControls(Control parent, int depth)
    {
        if (depth > 8) return;
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible || child.Width <= 0 || child.Height <= 0) continue;
            TimeSlowControls(child, depth + 1);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var bmp = new Bitmap(child.Width, child.Height))
                child.DrawToBitmap(bmp, new Rectangle(Point.Empty, child.Size));
            if (sw.ElapsedMilliseconds > 200)
                Note($"  slow first paint {sw.ElapsedMilliseconds} ms: {child.GetType().Name} '{child.Text}' {child.Width}x{child.Height} depth {depth}");
            if (sw.ElapsedMilliseconds > SlowPaintLimitMs)
                Fail(new TimeoutException($"{child.GetType().Name} took {sw.ElapsedMilliseconds} ms to paint"));
        }
    }

    private static List<AppListItem> SampleApps() =>
    [
        new() { Name = "7-Zip 24.08 (x64)", Publisher = "Igor Pavlov", Version = "24.08", IsInstalled = true },
        new() { Name = "Discord", Publisher = "Discord Inc.", Version = "1.0.9214", IsInstalled = true, IsSelected = true },
        new() { Name = "Git", Publisher = "The Git Development Community", Version = "2.51.0", IsInstalled = true },
        new() { Name = "Minecraft Launcher", Publisher = "Mojang", Version = "2.24.17", IsInstalled = true, IsSelected = true, Note = "Fabric 0.16 for 1.21.4, then drop the mods folder back in." },
        new() { Name = "Mozilla Firefox (x64 en-US)", Publisher = "Mozilla", Version = "143.0", IsInstalled = true, IsSelected = true },
        new() { Name = "OBS Studio", Publisher = "OBS Project", Version = "31.1.2", IsInstalled = true, IsSelected = true, Note = "Scene collection export is in Documents." },
        new() { Name = "Paint.NET", Publisher = "dotPDN LLC", Version = "5.1.9", IsInstalled = false, IsSelected = true },
        new() { Name = "Spotify", Publisher = "Spotify AB", Version = "1.2.74", IsInstalled = true },
        new() { Name = "Steam", Publisher = "Valve Corporation", Version = "2.10.91.91", IsInstalled = true, IsSelected = true, Note = "Library lives on D:\\SteamLibrary." },
        new() { Name = "Visual Studio Code", Publisher = "Microsoft Corporation", Version = "1.104.2", IsInstalled = true, IsSelected = true },
        new() { Name = "VLC media player", Publisher = "VideoLAN", Version = "3.0.21", IsInstalled = true },
    ];
}
