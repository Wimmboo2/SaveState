namespace SaveState.Core.Presets;

/// <summary>A one-click bundle of common files/folders for an app. Paths use environment variables.</summary>
public sealed record Preset(string Id, string Name, string Description, IReadOnlyList<string> Paths);

/// <summary>
/// Built-in presets. To add one (Discord, VS Code, ...), append a new <see cref="Preset"/> here:
/// the Files page picks it up automatically and only adds paths that exist on this PC.
/// </summary>
public static class Presets
{
    public static IReadOnlyList<Preset> All { get; } =
    [
        new("minecraft", "Minecraft",
            "Resource packs, mods, mod configs, video/key settings and your server list.",
            [
                @"%APPDATA%\.minecraft\resourcepacks",
                @"%APPDATA%\.minecraft\options.txt",
                @"%APPDATA%\.minecraft\servers.dat",
                @"%APPDATA%\.minecraft\mods",
                @"%APPDATA%\.minecraft\config",
            ]),
    ];

    /// <summary>Expands the preset's paths and returns those that exist here.</summary>
    public static IReadOnlyList<string> ResolveExisting(Preset preset, Func<string, string>? expand = null)
    {
        expand ??= Environment.ExpandEnvironmentVariables;
        return preset.Paths
            .Select(expand)
            .Where(p => File.Exists(p) || Directory.Exists(p))
            .ToList();
    }
}
