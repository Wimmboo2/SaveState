using SaveState.Core.Api;
using SaveState.Core.Config;
using SaveState.Core.Models;

namespace SaveState.Core.Tests;

/// <summary>
/// Opt-in tests against the real Supabase project. Set SAVESTATE_TEST_EMAIL and
/// SAVESTATE_TEST_PASSWORD for a throwaway account; they're skipped otherwise.
/// </summary>
[Collection("Live account")] // both classes use the same account, so never run them in parallel
public class LiveSupabaseTests
{
    private static readonly string? Email = Environment.GetEnvironmentVariable("SAVESTATE_TEST_EMAIL");
    private static readonly string? Password = Environment.GetEnvironmentVariable("SAVESTATE_TEST_PASSWORD");

    private static async Task<SupabaseApi?> LoginAsync()
    {
        if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password)) return null;
        var api = new SupabaseApi(new HttpClient(), AppConfig.Default, new InMemorySessionStore());
        await api.SignInAsync(Email, Password);
        return api;
    }

    [Fact]
    public async Task Saves_and_reads_back_the_app_list()
    {
        var api = await LoginAsync();
        if (api is null) return;

        var apps = new List<SavedApp> { new("Steam", "Valve Corporation", "2.10", "Library on D:"), new("Discord", "Discord Inc.", "1.0", null) };
        var saved = await api.SaveAppsAsync(apps);
        Assert.Equal(2, saved.Apps.Count);

        // Upsert again (second call must update, not fail on the unique user_id).
        saved = await api.SaveAppsAsync(apps.Take(1).ToList());
        Assert.Single(saved.Apps);

        var row = await api.GetBackupAsync();
        Assert.NotNull(row);
        Assert.Equal("Library on D:", row!.Apps.Single().Note);
    }

    [Fact]
    public async Task Clients_cannot_write_file_metadata()
    {
        var api = await LoginAsync();
        if (api is null) return;
        await api.SaveAppsAsync([]);

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{AppConfig.Default.SupabaseUrl}/rest/v1/backups?user_id=eq.{api.Session!.UserId}");
        request.Headers.Add("apikey", AppConfig.Default.PublishableKey);
        request.Headers.Authorization = new("Bearer", api.Session.AccessToken);
        request.Content = new StringContent("""{"expires_at":"2099-01-01T00:00:00Z"}""", System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);

        Assert.False(response.IsSuccessStatusCode, "setting expires_at from the client must be denied");
    }

    [Fact]
    public async Task Bad_password_is_invalid_credentials()
    {
        if (string.IsNullOrEmpty(Email)) return;
        var api = new SupabaseApi(new HttpClient(), AppConfig.Default, new InMemorySessionStore());
        var e = await Assert.ThrowsAsync<ApiException>(() => api.SignInAsync(Email, "definitely-wrong-password"));
        Assert.Equal(ApiErrorKind.InvalidCredentials, e.Kind);
    }
}
