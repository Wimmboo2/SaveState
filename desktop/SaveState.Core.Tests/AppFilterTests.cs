using SaveState.Core.Apps;
using SaveState.Core.Models;

namespace SaveState.Core.Tests;

public class AppFilterTests
{
    private static UninstallEntry App(string? name, string? publisher = "Pub", string? version = "1.0") =>
        new() { DisplayName = name, Publisher = publisher, DisplayVersion = version };

    [Fact]
    public void Drops_entries_without_display_name()
    {
        Assert.False(AppFilter.IsUserFacing(App(null)));
        Assert.False(AppFilter.IsUserFacing(App("   ")));
    }

    [Fact]
    public void Drops_system_components_updates_and_hotfixes()
    {
        Assert.False(AppFilter.IsUserFacing(App("Driver thing") with { SystemComponent = 1 }));
        Assert.False(AppFilter.IsUserFacing(App("Office patch") with { ParentKeyName = "Office16.PROPLUS" }));
        Assert.False(AppFilter.IsUserFacing(App("Some fix") with { ReleaseType = "Hotfix" }));
        Assert.False(AppFilter.IsUserFacing(App("Some fix") with { ReleaseType = "Security Update" }));
        Assert.False(AppFilter.IsUserFacing(App("Security Update for Microsoft Windows (KB5034441)")));
        Assert.True(AppFilter.IsUserFacing(App("Steam") with { SystemComponent = 0 }));
    }

    [Fact]
    public void Collapses_duplicates_keeping_highest_version_and_sorts()
    {
        var apps = AppFilter.Clean([
            App("Steam", "Valve Corporation", "2.10.91.91"),
            App("7-Zip 23.01 (x64)", "Igor Pavlov", "23.01"),
            App("steam ", "Valve Corporation", "2.10.91.95"),
            App("Discord", "Discord Inc.", "1.0.9214"),
        ]);

        Assert.Equal(["7-zip 23.01 (x64)", "discord", "steam"], apps.Select(a => a.Name.ToLowerInvariant()).ToArray());
        Assert.Equal("2.10.91.95", apps.Single(a => a.Name.StartsWith("steam", StringComparison.OrdinalIgnoreCase)).Version);
    }

    [Fact]
    public void Same_name_different_publisher_are_different_apps()
    {
        var apps = AppFilter.Clean([App("Launcher", "Epic"), App("Launcher", "Mojang")]);
        Assert.Equal(2, apps.Count);
    }

    [Fact]
    public void Merge_restores_saved_notes_and_keeps_uninstalled_saved_apps()
    {
        var installed = new List<InstalledApp>
        {
            new("Steam", "Valve Corporation", "2.10"),
            new("Discord", "Discord Inc.", "1.0"),
        };
        var saved = new List<SavedApp>
        {
            new("Steam", "Valve Corporation", "2.9", "Library on D:"),
            new("OBS Studio", "OBS Project", "31.1", null),
        };

        var merged = AppListMerger.Merge(installed, saved);

        Assert.Equal(["Discord", "OBS Studio", "Steam"], merged.Select(m => m.Name).ToArray());
        var steam = merged.Single(m => m.Name == "Steam");
        Assert.True(steam.IsSelected);
        Assert.True(steam.IsInstalled);
        Assert.Equal("Library on D:", steam.Note);
        Assert.Equal("2.10", steam.Version);
        var obs = merged.Single(m => m.Name == "OBS Studio");
        Assert.True(obs.IsSelected);
        Assert.False(obs.IsInstalled);
        Assert.False(merged.Single(m => m.Name == "Discord").IsSelected);
    }

    [Fact]
    public void ToSavedApp_trims_note_and_nulls_empty()
    {
        var item = new AppListItem { Name = "Steam", Publisher = "Valve", Version = "1", Note = "  " };
        Assert.Null(item.ToSavedApp().Note);
        item.Note = " keep library ";
        Assert.Equal("keep library", item.ToSavedApp().Note);
    }
}
