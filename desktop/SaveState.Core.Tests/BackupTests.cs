using System.IO.Compression;
using System.Text.Json;
using SaveState.Core.Backup;
using SaveState.Core.Models;

namespace SaveState.Core.Tests;

public class PathTokensTests
{
    private static readonly IReadOnlyList<PathRoot> Roots = PathTokens.Normalize([
        new("USERPROFILE", @"C:\Users\Wim"),
        new("APPDATA", @"C:\Users\Wim\AppData\Roaming"),
        new("LOCALAPPDATA", @"C:\Users\Wim\AppData\Local"),
        new("ProgramFiles", @"C:\Program Files"),
    ]);

    [Theory]
    [InlineData(@"C:\Users\Wim\AppData\Roaming\.minecraft\options.txt", @"%APPDATA%\.minecraft\options.txt")]
    [InlineData(@"c:\users\wim\appdata\local\Packages\x", @"%LOCALAPPDATA%\Packages\x")]
    [InlineData(@"C:\Users\Wim\Documents\My Games", @"%USERPROFILE%\Documents\My Games")]
    [InlineData(@"C:\Users\Wim\AppData\Roaming", @"%APPDATA%")]
    [InlineData(@"C:\Users\Wimbledon\file.txt", @"C:\Users\Wimbledon\file.txt")]
    [InlineData(@"D:\Games\Saves\", @"D:\Games\Saves")]
    public void Tokenizes_with_most_specific_folder(string path, string expected) =>
        Assert.Equal(expected, PathTokens.Tokenize(path, Roots));

    [Theory]
    [InlineData(@"C:\Users\Wim\AppData\Roaming\.minecraft\mods\sodium.jar", "files/APPDATA/.minecraft/mods/sodium.jar")]
    [InlineData(@"D:\Games\Saves\slot1.sav", "files/_drives/D/Games/Saves/slot1.sav")]
    [InlineData(@"\\nas\share\configs\a.ini", "files/_network/nas/share/configs/a.ini")]
    [InlineData(@"C:\Program Files\Game\config.cfg", "files/ProgramFiles/Game/config.cfg")]
    public void Maps_to_collision_free_zip_paths(string path, string expected) =>
        Assert.Equal(expected, PathTokens.ZipPath(path, Roots));
}

public sealed class BackupBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "savestate-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _appData;
    private readonly IReadOnlyList<PathRoot> _roots;

    public BackupBuilderTests()
    {
        _appData = Path.Combine(_root, "AppData", "Roaming");
        _roots = PathTokens.Normalize([new("APPDATA", _appData)]);
        Write(@".minecraft/options.txt", "fov:90");
        Write(@".minecraft/resourcepacks/Faithful.zip", new string('x', 5000));
        Write(@".minecraft/resourcepacks/nested/pack.mcmeta", "{}");
        Write(@".minecraft/servers.dat", "servers");
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_appData, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Plan_measures_folders_skips_duplicates_and_reports_missing()
    {
        var folder = Path.Combine(_appData, ".minecraft", "resourcepacks");
        var fileInside = Path.Combine(folder, "Faithful.zip");
        var plan = BackupBuilder.Plan([fileInside, folder, Path.Combine(_appData, "nope.txt")]);

        Assert.Single(plan.Items); // the file inside the folder isn't counted twice
        Assert.Equal(2, plan.FileCount);
        Assert.Equal(5002, plan.TotalBytes);
        Assert.Single(plan.Missing);
    }

    [Fact]
    public void Builds_zip_with_files_manifest_and_readme()
    {
        var plan = BackupBuilder.Plan([
            Path.Combine(_appData, ".minecraft", "resourcepacks"),
            Path.Combine(_appData, ".minecraft", "options.txt"),
        ]);
        var apps = new List<SavedApp> { new("Minecraft Launcher", "Mojang", "2.24", "Fabric 0.16") };
        var zipPath = Path.Combine(_root, "out", "backup.zip");
        var reports = new List<BuildProgress>();

        var result = new BackupBuilder(_roots, "SaveState 1.0").Build(plan, apps, zipPath, new SyncProgress<BuildProgress>(reports.Add));

        Assert.True(File.Exists(zipPath));
        Assert.Equal(3, result.FileCount);
        Assert.NotEmpty(reports);
        using var zip = ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("files/APPDATA/.minecraft/options.txt", names);
        Assert.Contains("files/APPDATA/.minecraft/resourcepacks/Faithful.zip", names);
        Assert.Contains("files/APPDATA/.minecraft/resourcepacks/nested/pack.mcmeta", names);
        Assert.Contains("manifest.json", names);
        Assert.Contains("README.txt", names);

        using var manifestStream = zip.GetEntry("manifest.json")!.Open();
        var manifest = JsonDocument.Parse(manifestStream).RootElement;
        Assert.Equal(1, manifest.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Minecraft Launcher", manifest.GetProperty("apps")[0].GetProperty("name").GetString());
        var files = manifest.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("originalPath").GetString()).ToList();
        Assert.Contains(@"%APPDATA%\.minecraft\options.txt", files);
        Assert.DoesNotContain(files, f => f!.Contains(_root, StringComparison.OrdinalIgnoreCase));

        using var readme = new StreamReader(zip.GetEntry("README.txt")!.Open());
        var text = readme.ReadToEnd();
        Assert.Contains(@"to: %APPDATA%\.minecraft\resourcepacks", text);
        Assert.Contains("Note: Fabric 0.16", text);
    }

    [Fact]
    public void Cancelling_removes_the_partial_zip()
    {
        var plan = BackupBuilder.Plan([Path.Combine(_appData, ".minecraft")]);
        var zipPath = Path.Combine(_root, "out", "cancelled.zip");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new BackupBuilder(_roots).Build(plan, [], zipPath, ct: cts.Token));
        Assert.False(File.Exists(zipPath));
    }

    [Fact]
    public void Minecraft_preset_resolves_only_existing_paths()
    {
        var preset = Presets.Presets.All.Single(p => p.Id == "minecraft");
        var found = Presets.Presets.ResolveExisting(preset, p => p.Replace("%APPDATA%", _appData).Replace('\\', Path.DirectorySeparatorChar));
        Assert.Equal(3, found.Count); // resourcepacks, options.txt, servers.dat (no mods/config folder here)
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    /// <summary>IProgress that reports synchronously (Progress&lt;T&gt; posts asynchronously).</summary>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
