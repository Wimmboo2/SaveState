using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SaveState.Core.Api;
using SaveState.Core.Backup;
using SaveState.Core.Models;

namespace SaveState.Core.Tests;

public class SupabaseApiTests
{
    [Theory]
    [InlineData(400, """{"code":400,"error_code":"invalid_credentials","msg":"Invalid login credentials"}""", ApiErrorKind.InvalidCredentials)]
    [InlineData(422, """{"code":422,"error_code":"user_already_exists","msg":"User already registered"}""", ApiErrorKind.UserAlreadyExists)]
    [InlineData(422, """{"error_code":"weak_password","msg":"Password should be at least 6 characters."}""", ApiErrorKind.WeakPassword)]
    [InlineData(429, """{"msg":"slow down"}""", ApiErrorKind.RateLimited)]
    [InlineData(503, "", ApiErrorKind.Server)]
    [InlineData(403, """{"code":"42501","message":"permission denied for table backups"}""", ApiErrorKind.Rejected)]
    public void Maps_error_bodies_to_friendly_kinds(int status, string body, ApiErrorKind kind)
    {
        var node = string.IsNullOrEmpty(body) ? null : JsonNode.Parse(body);
        var e = SupabaseApi.ToApiException((HttpStatusCode)status, node);
        Assert.Equal(kind, e.Kind);
        Assert.False(string.IsNullOrWhiteSpace(e.Message));
    }

    [Fact]
    public void Edge_function_error_sentences_are_passed_through()
    {
        var e = SupabaseApi.ToApiException(HttpStatusCode.BadRequest, JsonNode.Parse("""{"error":"Your backup is 120 MB, over the 100 MB limit.","code":"too_large"}"""));
        Assert.Equal("Your backup is 120 MB, over the 100 MB limit.", e.Message);
        Assert.Equal("too_large", e.Code);
    }

    [Fact]
    public void Parses_token_response_into_session()
    {
        var session = SupabaseApi.ParseSession(JsonNode.Parse("""
            {"access_token":"a","refresh_token":"r","expires_in":3600,"expires_at":1790418000,
             "user":{"id":"0b33c4be","email":"me@example.com"}}
            """));
        Assert.Equal("a", session.AccessToken);
        Assert.Equal("r", session.RefreshToken);
        Assert.Equal("0b33c4be", session.UserId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790418000), session.ExpiresAt);
    }

    [Fact]
    public void Saved_apps_serialize_as_snake_case_json()
    {
        var json = JsonSerializer.Serialize(new { apps = new[] { new SavedApp("Steam", "Valve", "2.1", null) } }, SupabaseApi.Json);
        Assert.Equal("""{"apps":[{"name":"Steam","publisher":"Valve","version":"2.1","note":null}]}""", json);
    }

    [Fact]
    public async Task Refreshes_expiring_token_before_calling_and_persists_it()
    {
        var store = new InMemorySessionStore();
        store.Save(new Session("old", "refresh-1", DateTimeOffset.UtcNow.AddSeconds(10), "u1", "me@example.com"));
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery.StartsWith("/auth/v1/token?grant_type=refresh_token"))
                return (HttpStatusCode.OK, """{"access_token":"new","refresh_token":"refresh-2","expires_in":3600,"user":{"id":"u1","email":"me@example.com"}}""");
            Assert.Equal("Bearer new", request.Headers.Authorization!.ToString());
            return (HttpStatusCode.OK, "[]");
        });
        var api = new SupabaseApi(new HttpClient(handler), Config.AppConfig.Default, store);

        var backup = await api.GetBackupAsync();

        Assert.Null(backup);
        Assert.Equal("refresh-2", store.Load()!.RefreshToken);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Revoked_refresh_token_clears_session_and_reports_expired()
    {
        var store = new InMemorySessionStore();
        store.Save(new Session("old", "gone", DateTimeOffset.UtcNow.AddSeconds(5), "u1", "me@example.com"));
        var handler = new FakeHandler(_ => (HttpStatusCode.BadRequest, """{"error_code":"refresh_token_not_found","msg":"Invalid Refresh Token"}"""));
        var api = new SupabaseApi(new HttpClient(handler), Config.AppConfig.Default, store);

        var e = await Assert.ThrowsAsync<ApiException>(() => api.GetBackupAsync());

        Assert.Equal(ApiErrorKind.SessionExpired, e.Kind);
        Assert.Null(store.Load());
        Assert.False(api.IsLoggedIn);
    }

    [Fact]
    public async Task Network_failure_is_reported_as_network()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("No such host"));
        var api = new SupabaseApi(new HttpClient(handler), Config.AppConfig.Default, new InMemorySessionStore());
        var e = await Assert.ThrowsAsync<ApiException>(() => api.SignInAsync("a@b.c", "x"));
        Assert.Equal(ApiErrorKind.Network, e.Kind);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(44_040_192, "42 MB")]
    [InlineData(104_857_600, "100 MB")]
    public void Formats_sizes(long bytes, string expected) => Assert.Equal(expected, Sizes.Format(bytes));

    [Fact]
    public void Formats_time_left()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("in 23 days", Sizes.TimeLeft(now.AddDays(23.5), now));
        Assert.Equal("in 5 hours", Sizes.TimeLeft(now.AddHours(5.2), now));
        Assert.Equal("in under an hour", Sizes.TimeLeft(now.AddMinutes(10), now));
        Assert.Equal("expired", Sizes.TimeLeft(now.AddMinutes(-1), now));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var (status, body) = respond(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
