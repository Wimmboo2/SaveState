using System.Text.Json;

namespace SaveState.Services;

/// <summary>Remembers the picked files/folders between launches (plain paths, no contents).</summary>
internal static class SelectionStore
{
    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SelectionFile)) return [];
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(AppPaths.SelectionFile)) ?? [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Couldn't read saved selection: {e.GetType().Name}");
            return [];
        }
    }

    public static void Save(IReadOnlyList<string> sources)
    {
        try
        {
            AppPaths.EnsureRoot();
            File.WriteAllText(AppPaths.SelectionFile, JsonSerializer.Serialize(sources));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Couldn't save selection: {e.Message}");
        }
    }
}
