using System.Net.Http.Headers;
using SaveState.Core.Api;
using SaveState.Core.Backup;
using SaveState.Core.Config;
using SaveState.Core.Models;

namespace SaveState.Core.Tests;

/// <summary>
/// Opt-in end-to-end test of the storage loop (edge function + R2). Needs SAVESTATE_TEST_EMAIL,
/// SAVESTATE_TEST_PASSWORD and SAVESTATE_TEST_STORAGE=1 (only once R2 secrets are configured).
/// </summary>
public class LiveStorageTests
{
    private static readonly string? Email = Environment.GetEnvironmentVariable("SAVESTATE_TEST_EMAIL");
    private static readonly string? Password = Environment.GetEnvironmentVariable("SAVESTATE_TEST_PASSWORD");
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("SAVESTATE_TEST_STORAGE") == "1";

    [Fact]
    public async Task Upload_download_delete_round_trip()
    {
        if (!Enabled || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password)) return;
        using var http = new HttpClient();
        var api = new SupabaseApi(http, AppConfig.Default, new InMemorySessionStore());
        await api.SignInAsync(Email, Password);
        var transfer = new BackupTransfer(api, http);

        var dir = Directory.CreateTempSubdirectory("savestate-live-");
        try
        {
            var zip = Path.Combine(dir.FullName, "backup.zip");
            var payload = new byte[300_000];
            Random.Shared.NextBytes(payload);
            await File.WriteAllBytesAsync(zip, payload);

            var row = await transfer.UploadAsync(zip, [new SavedApp("Steam", "Valve", "1", "live test")]);
            Assert.True(row.HasFiles);
            Assert.Equal(payload.Length, row.SizeBytes);
            Assert.NotNull(row.ExpiresAt);
            Assert.InRange((row.ExpiresAt!.Value - row.UploadedAt!.Value).TotalDays, 29.9, 30.1);

            var downloaded = Path.Combine(dir.FullName, "downloaded.zip");
            await transfer.DownloadAsync(downloaded);
            Assert.Equal(payload, await File.ReadAllBytesAsync(downloaded));

            var cleared = await transfer.DeleteFilesAsync();
            Assert.False(cleared.HasFiles);
            Assert.Single(cleared.Apps); // the app list survives

            var e = await Assert.ThrowsAsync<ApiException>(() => transfer.DownloadAsync(downloaded));
            Assert.Equal("no_files", e.Code);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Presigned_upload_rejects_a_bigger_body_than_signed()
    {
        if (!Enabled || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password)) return;
        using var http = new HttpClient();
        var api = new SupabaseApi(http, AppConfig.Default, new InMemorySessionStore());
        await api.SignInAsync(Email, Password);

        var ticket = await api.InvokeFunctionAsync("backup", new { action = "upload-url", size_bytes = 1000 });
        using var request = new HttpRequestMessage(HttpMethod.Put, ticket!["url"]!.GetValue<string>())
        {
            Content = new ByteArrayContent(new byte[5000]),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var response = await http.SendAsync(request);
        Assert.False(response.IsSuccessStatusCode, $"expected storage to reject a mismatched size, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Oversized_upload_request_is_refused_up_front()
    {
        if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password)) return;
        var api = new SupabaseApi(new HttpClient(), AppConfig.Default, new InMemorySessionStore());
        await api.SignInAsync(Email, Password);
        var e = await Assert.ThrowsAsync<ApiException>(() =>
            api.InvokeFunctionAsync("backup", new { action = "upload-url", size_bytes = 101L * 1024 * 1024 }));
        // 413 too_large once storage is configured; 503 before that. Either way nothing is signed.
        Assert.Contains(e.Code, new[] { "too_large", "storage_not_configured" });
    }
}
