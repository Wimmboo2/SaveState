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

    [JsonIgnore] public bool HasFiles => FilePath is not null;
}
