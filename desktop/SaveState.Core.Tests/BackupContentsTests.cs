using System.Text.Json;
using SaveState.Core.Api;
using SaveState.Core.Models;

namespace SaveState.Core.Tests;

public class BackupContentsTests
{
    // Shape written by the backup edge function (supabase/functions/_shared/file-list.ts).
    private const string RowJson = """
        {
          "apps": [{ "name": "Steam", "publisher": "Valve", "version": "1", "note": null }],
          "file_path": "u/backup.zip", "size_bytes": 2872064,
          "uploaded_at": "2026-09-26T13:46:21+00:00", "expires_at": "2026-10-26T13:46:21+00:00",
          "updated_at": "2026-09-26T13:46:24.525487+00:00",
          "files": {
            "file_count": 6, "total_bytes": 2400000, "item_count": 2, "listed_count": 6,
            "items": [
              { "path": "%APPDATA%\\.minecraft\\mods", "kind": "folder", "file_count": 5, "size_bytes": 2399991,
                "files": [{ "path": "sodium.jar", "size_bytes": 312000 }, { "path": "sub\\iris.jar", "size_bytes": 176000 }] },
              { "path": "%APPDATA%\\.minecraft\\options.txt", "kind": "file", "file_count": 1, "size_bytes": 9, "files": [] }
            ]
          },
          "files_removed_at": null, "files_removed_reason": null
        }
        """;

    private static BackupRow Parse(string json) => JsonSerializer.Deserialize<BackupRow>(json, SupabaseApi.Json)!;

    [Fact]
    public void Reads_the_file_list_from_the_row()
    {
        var row = Parse(RowJson);
        Assert.True(row.HasFiles);
        Assert.False(row.FilesWereDeleted);
        var files = row.Files!;
        Assert.False(files.Unavailable);
        Assert.Equal(6, files.FileCount);
        Assert.Equal(2, files.Items.Count);
        Assert.True(files.Items[0].IsFolder);
        Assert.Equal(@"sub\iris.jar", files.Items[0].Files[1].Path);
        Assert.False(files.Items[1].IsFolder);
    }

    [Fact]
    public void Unavailable_list_and_missing_list_are_told_apart()
    {
        Assert.True(Parse("""{ "apps": [], "file_path": "u/backup.zip", "files": { "unavailable": true } }""").Files!.Unavailable);
        Assert.Null(Parse("""{ "apps": [], "file_path": "u/backup.zip" }""").Files);
    }

    [Theory]
    [InlineData("deleted", true, false)]
    [InlineData("expired", false, true)]
    public void Knows_why_files_are_gone(string reason, bool deleted, bool expired)
    {
        var row = Parse($$"""{ "apps": [], "file_path": null, "files_removed_at": "2026-09-26T14:00:00+00:00", "files_removed_reason": "{{reason}}" }""");
        Assert.False(row.HasFiles);
        Assert.Equal(deleted, row.FilesWereDeleted);
        Assert.Equal(expired, row.FilesExpired);
    }

    [Theory]
    [InlineData(@"%APPDATA%\.minecraft\mods", true)]
    [InlineData(@"%appdata%\.MINECRAFT\mods\", true)]          // case and trailing slash don't matter
    [InlineData(@"%APPDATA%\.minecraft\mods\sodium.jar", true)] // inside a backed-up folder
    [InlineData(@"%APPDATA%\.minecraft\options.txt", true)]
    [InlineData(@"%APPDATA%\.minecraft\modsextra", false)]      // prefix of a name isn't "inside"
    [InlineData(@"%APPDATA%\.minecraft\options.txt\x", false)]  // files have no children
    [InlineData(@"%APPDATA%\.minecraft", false)]                // parent of a backed-up item
    public void Covers_picks_that_are_in_the_backup(string path, bool expected) =>
        Assert.Equal(expected, Parse(RowJson).Files!.Covers(path));
}
