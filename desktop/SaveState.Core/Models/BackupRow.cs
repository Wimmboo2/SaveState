using System.Text.Json.Serialization;

namespace SaveState.Core.Models;

/// <summary>The user's single row in <c>public.backups</c>.</summary>
public sealed record BackupRow
{
    [JsonPropertyName("apps")] public List<SavedApp> Apps { get; init; } = [];
    [JsonPropertyName("file_path")] public string? FilePath { get; init; }
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; init; }
    [JsonPropertyName("uploaded_at")] public DateTimeOffset? UploadedAt { get; init; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>What's inside the stored zip (read by the server from the zip itself). Null if unknown.</summary>
    [JsonPropertyName("files")] public BackupContents? Files { get; init; }

    /// <summary>When the last zip went away (deleted on the website, or expired). Cleared by a new upload.</summary>
    [JsonPropertyName("files_removed_at")] public DateTimeOffset? FilesRemovedAt { get; init; }

    /// <summary>"deleted" or "expired".</summary>
    [JsonPropertyName("files_removed_reason")] public string? FilesRemovedReason { get; init; }

    [JsonIgnore] public bool HasFiles => FilePath is not null;
    [JsonIgnore] public bool FilesWereDeleted => !HasFiles && FilesRemovedAt is not null && FilesRemovedReason == "deleted";
    [JsonIgnore] public bool FilesExpired => !HasFiles && FilesRemovedAt is not null && FilesRemovedReason == "expired";
}

/// <summary>Summary of the zip's manifest, written by the backup edge function.</summary>
public sealed record BackupContents
{
    /// <summary>True when the zip had no readable manifest (the list can't be shown).</summary>
    [JsonPropertyName("unavailable")] public bool Unavailable { get; init; }
    [JsonPropertyName("file_count")] public int FileCount { get; init; }
    [JsonPropertyName("total_bytes")] public long TotalBytes { get; init; }
    [JsonPropertyName("item_count")] public int ItemCount { get; init; }
    [JsonPropertyName("listed_count")] public int ListedCount { get; init; }
    [JsonPropertyName("items")] public List<BackupContentItem> Items { get; init; } = [];

    /// <summary>
    /// Whether a local pick (as a tokenized path like <c>%APPDATA%\.minecraft\mods</c>) is in this
    /// backup: it was picked itself, or it sits inside a folder that was backed up.
    /// </summary>
    public bool Covers(string tokenizedPath)
    {
        var path = tokenizedPath.TrimEnd('\\', '/');
        foreach (var item in Items)
        {
            var itemPath = item.Path.TrimEnd('\\', '/');
            if (string.Equals(path, itemPath, StringComparison.OrdinalIgnoreCase)) return true;
            if (item.IsFolder && path.Length > itemPath.Length && path.StartsWith(itemPath, StringComparison.OrdinalIgnoreCase)
                && path[itemPath.Length] is '\\' or '/')
                return true;
        }
        return false;
    }
}

public sealed record BackupContentItem
{
    [JsonPropertyName("path")] public string Path { get; init; } = "";
    [JsonPropertyName("kind")] public string Kind { get; init; } = "file";
    [JsonPropertyName("file_count")] public int FileCount { get; init; }
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; init; }

    /// <summary>Files inside a folder, relative to it. May be fewer than <see cref="FileCount"/> on huge backups.</summary>
    [JsonPropertyName("files")] public List<BackupContentFile> Files { get; init; } = [];

    [JsonIgnore] public bool IsFolder => Kind == "folder";
}

public sealed record BackupContentFile
{
    [JsonPropertyName("path")] public string Path { get; init; } = "";
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; init; }
}
