using SaveState.Core.Models;

namespace SaveState.Core.Apps;

/// <summary>A row in the Apps checklist: an installed and/or previously saved app, plus the user's choices.</summary>
public sealed class AppListItem
{
    public required string Name { get; init; }
    public string? Publisher { get; init; }
    public string? Version { get; init; }
    public bool IsInstalled { get; init; }
    public bool IsSelected { get; set; }
    public string Note { get; set; } = "";

    public string Key => AppFilter.AppKey(Name, Publisher);

    public SavedApp ToSavedApp() =>
        new(Name, Publisher, Version, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());

    public bool Matches(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var s = search.Trim();
        return Name.Contains(s, StringComparison.CurrentCultureIgnoreCase)
            || (Publisher?.Contains(s, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || Note.Contains(s, StringComparison.CurrentCultureIgnoreCase);
    }
}

public static class AppListMerger
{
    /// <summary>
    /// Combines what's installed now with what the user saved last time: saved apps come back
    /// ticked with their notes, and saved apps that aren't installed (e.g. right after a
    /// reinstall) stay in the list so the next upload doesn't silently drop them.
    /// </summary>
    public static List<AppListItem> Merge(IEnumerable<InstalledApp> installed, IEnumerable<SavedApp> saved)
    {
        var savedByKey = new Dictionary<string, SavedApp>();
        foreach (var app in saved)
            savedByKey.TryAdd(AppFilter.AppKey(app.Name, app.Publisher), app);

        var items = new List<AppListItem>();
        var seen = new HashSet<string>();
        foreach (var app in installed)
        {
            var key = AppFilter.AppKey(app.Name, app.Publisher);
            if (!seen.Add(key)) continue;
            savedByKey.TryGetValue(key, out var match);
            items.Add(new AppListItem
            {
                Name = app.Name,
                Publisher = app.Publisher,
                Version = app.Version,
                IsInstalled = true,
                IsSelected = match is not null,
                Note = match?.Note ?? "",
            });
        }

        foreach (var (key, app) in savedByKey)
        {
            if (seen.Contains(key)) continue;
            items.Add(new AppListItem
            {
                Name = app.Name,
                Publisher = app.Publisher,
                Version = app.Version,
                IsInstalled = false,
                IsSelected = true,
                Note = app.Note ?? "",
            });
        }

        return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
