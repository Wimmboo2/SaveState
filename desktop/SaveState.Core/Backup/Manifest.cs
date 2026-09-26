using System.Text.Json.Serialization;
using SaveState.Core.Models;

namespace SaveState.Core.Backup;

/// <summary>manifest.json inside the zip: machine-readable record of what was backed up and from where.</summary>
public sealed record Manifest
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("createdAtUtc")] public DateTimeOffset CreatedAtUtc { get; init; }
    [JsonPropertyName("generator")] public string Generator { get; init; } = "SaveState";
    [JsonPropertyName("apps")] public IReadOnlyList<SavedApp> Apps { get; init; } = [];
    [JsonPropertyName("items")] public IReadOnlyList<ManifestItem> Items { get; init; } = [];
    [JsonPropertyName("files")] public IReadOnlyList<ManifestFile> Files { get; init; } = [];
}

/// <summary>One thing the user picked (a file or a whole folder).</summary>
public sealed record ManifestItem(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("originalPath")] string OriginalPath,
    [property: JsonPropertyName("zipPath")] string ZipPath,
    [property: JsonPropertyName("fileCount")] int FileCount,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes);

/// <summary>Every individual file in the zip.</summary>
public sealed record ManifestFile(
    [property: JsonPropertyName("originalPath")] string OriginalPath,
    [property: JsonPropertyName("zipPath")] string ZipPath,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes);
