using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SaveState.Core.Config;
using SaveState.Core.Models;

namespace SaveState.Core.Api;

/// <summary>Result of a sign-up: either logged in straight away, or waiting for email confirmation.</summary>
public sealed record SignUpResult(Session? Session)
{
    public bool NeedsEmailConfirmation => Session is null;
}

/// <summary>
/// Thin HTTP client for the parts of Supabase the desktop app needs: Auth (GoTrue), PostgREST and
/// Edge Functions. Uses only the public URL + publishable key; the user's JWT authorizes everything
/// else, and Row Level Security keeps each user inside their own row.
/// </summary>
public sealed class SupabaseApi
{
    /// <summary>Refresh the access token when it has less than this left.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly HttpClient _http;
    private readonly AppConfig _config;
    private readonly ISessionStore _store;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public SupabaseApi(HttpClient http, AppConfig config, ISessionStore store)
    {
        _http = http;
        _config = config;
        _store = store;
        Session = store.Load();
    }

    public Session? Session { get; private set; }
    public bool IsLoggedIn => Session is not null;
    public AppConfig Config => _config;

    // ---------------------------------------------------------------- Auth

    public async Task<Session> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "auth/v1/token?grant_type=password");
        request.Content = JsonContent.Create(new { email, password });
        var session = ParseSession(await SendAsync(request, ct));
        SetSession(session);
        return session;
    }

    public async Task<SignUpResult> SignUpAsync(string email, string password, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "auth/v1/signup");
        request.Content = JsonContent.Create(new { email, password });
        var body = await SendAsync(request, ct);

        // With "Confirm email" off, Supabase returns a full session. Otherwise just the user.
        if (body?["access_token"] is null) return new SignUpResult(null);
        var session = ParseSession(body);
        SetSession(session);
        return new SignUpResult(session);
    }

    /// <summary>Validates the stored session at startup, refreshing it if needed.</summary>
    public async Task<bool> RestoreSessionAsync(CancellationToken ct = default)
    {
        if (Session is null) return false;
        try
        {
            await EnsureFreshTokenAsync(force: Session.ExpiresWithin(RefreshMargin), ct);
            return Session is not null;
        }
        catch (ApiException e) when (e.Kind == ApiErrorKind.SessionExpired)
        {
            return false;
        }
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        var session = Session;
        ClearSession();
        if (session is null) return;
        try
        {
            // Revoke this device's refresh token. Local sign-out already happened, so failures are fine.
            using var request = NewRequest(HttpMethod.Post, "auth/v1/logout?scope=local");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await _http.SendAsync(request, cts.Token);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { }
    }

    // ---------------------------------------------------------------- Data

    /// <summary>The user's backup row, or null if they have never backed up.</summary>
    public async Task<BackupRow?> GetBackupAsync(CancellationToken ct = default)
    {
        var body = await SendAuthorizedAsync(
            () => NewRequest(HttpMethod.Get, "rest/v1/backups?select=apps,file_path,size_bytes,uploaded_at,expires_at,updated_at"),
            ct);
        var rows = body?.Deserialize<List<BackupRow>>(Json);
        return rows is { Count: > 0 } ? rows[0] : null;
    }

    /// <summary>
    /// Creates or updates the user's row with a new app list. Only <c>apps</c> is sent: the
    /// database grants clients nothing else, and <c>user_id</c> defaults to the caller.
    /// </summary>
    public async Task<BackupRow> SaveAppsAsync(IReadOnlyList<SavedApp> apps, CancellationToken ct = default)
    {
        var body = await SendAuthorizedAsync(() =>
        {
            var request = NewRequest(HttpMethod.Post, "rest/v1/backups?on_conflict=user_id");
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
            request.Content = JsonContent.Create(new { apps }, options: Json);
            return request;
        }, ct);
        var rows = body?.Deserialize<List<BackupRow>>(Json);
        return rows is { Count: > 0 } ? rows[0] : throw new ApiException(ApiErrorKind.Server, "Saving your app list failed. Please try again.");
    }

    /// <summary>Calls a Supabase Edge Function as the logged-in user.</summary>
    public async Task<JsonNode?> InvokeFunctionAsync(string name, object body, CancellationToken ct = default)
    {
        return await SendAuthorizedAsync(() =>
        {
            var request = NewRequest(HttpMethod.Post, $"functions/v1/{name}");
            request.Content = JsonContent.Create(body, options: Json);
            return request;
        }, ct);
    }

    // ---------------------------------------------------------------- Plumbing

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_config.SupabaseUrl.TrimEnd('/')}/{path}");
        request.Headers.Add("apikey", _config.PublishableKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>Sends a user-authorized request. On 401 it refreshes the token once and retries.</summary>
    private async Task<JsonNode?> SendAuthorizedAsync(Func<HttpRequestMessage> makeRequest, CancellationToken ct)
    {
        if (Session is null) throw ApiException.SessionExpired();
        await EnsureFreshTokenAsync(force: false, ct);

        for (var attempt = 0; ; attempt++)
        {
            using var request = makeRequest();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session!.AccessToken);
            try
            {
                return await SendAsync(request, ct);
            }
            catch (ApiException e) when (e.StatusCode == 401 && attempt == 0)
            {
                await EnsureFreshTokenAsync(force: true, ct);
            }
            catch (ApiException e) when (e.StatusCode == 401)
            {
                ClearSession();
                throw ApiException.SessionExpired();
            }
        }
    }

    private async Task EnsureFreshTokenAsync(bool force, CancellationToken ct)
    {
        var current = Session ?? throw ApiException.SessionExpired();
        if (!force && !current.ExpiresWithin(RefreshMargin)) return;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Another caller may have refreshed while we waited.
            if (!ReferenceEquals(current, Session) && Session is { } fresh && !fresh.ExpiresWithin(RefreshMargin)) return;

            using var request = NewRequest(HttpMethod.Post, "auth/v1/token?grant_type=refresh_token");
            request.Content = JsonContent.Create(new { refresh_token = current.RefreshToken });
            try
            {
                SetSession(ParseSession(await SendAsync(request, ct)));
            }
            catch (ApiException e) when (e.StatusCode is 400 or 401 or 403)
            {
                // Refresh token revoked, reused or expired.
                ClearSession();
                throw ApiException.SessionExpired();
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<JsonNode?> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeout);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cts.Token);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw ApiException.Timeout(e);
        }
        catch (HttpRequestException e)
        {
            throw ApiException.Network(e);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonNode? body = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { body = JsonNode.Parse(text); }
                catch (JsonException) { /* non-JSON body, handled below */ }
            }

            if (response.IsSuccessStatusCode) return body;
            throw ToApiException(response.StatusCode, body);
        }
    }

    /// <summary>Maps Supabase Auth / PostgREST / Edge Function error bodies to friendly messages.</summary>
    internal static ApiException ToApiException(HttpStatusCode status, JsonNode? body)
    {
        var code = (int)status;
        string? errorCode = Str(body, "error_code") ?? Str(body, "code") ?? Str(body, "error");
        string? message = Str(body, "msg") ?? Str(body, "message") ?? Str(body, "error_description") ?? Str(body, "error");
        var probe = $"{errorCode} {message}".ToLowerInvariant();

        if (probe.Contains("invalid_credentials") || probe.Contains("invalid login credentials"))
            return new(ApiErrorKind.InvalidCredentials, "That email and password don't match. Try again.", code, errorCode);
        if (probe.Contains("user_already_exists") || probe.Contains("already registered"))
            return new(ApiErrorKind.UserAlreadyExists, "An account with this email already exists. Log in instead.", code, errorCode);
        if (probe.Contains("weak_password") || probe.Contains("password should"))
            return new(ApiErrorKind.WeakPassword, "Pick a stronger password: at least 8 characters, not a common one.", code, errorCode);
        if (probe.Contains("email_not_confirmed"))
            return new(ApiErrorKind.EmailNotConfirmed, "Please confirm your email first (check your inbox), then log in.", code, errorCode);
        if (status == HttpStatusCode.TooManyRequests || probe.Contains("rate limit"))
            return new(ApiErrorKind.RateLimited, "Too many attempts. Wait a minute and try again.", code, errorCode);
        if (code >= 500)
            return new(ApiErrorKind.Server, "SaveState's server had a problem. Please try again in a moment.", code, errorCode);

        // Our edge functions return { error: "<friendly sentence>", code: "<machine code>" }.
        var friendly = Str(body, "error") is { Length: > 12 } sentence && sentence.Contains(' ') ? sentence : null;
        return new(ApiErrorKind.Rejected, friendly ?? "The request was rejected. Please try again.", code, errorCode);
    }

    private static string? Str(JsonNode? node, string key) =>
        node is JsonObject obj && obj.TryGetPropertyValue(key, out var value) && value is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    internal static Session ParseSession(JsonNode? body)
    {
        var accessToken = Str(body, "access_token");
        var refreshToken = Str(body, "refresh_token");
        var user = body?["user"];
        var userId = Str(user, "id");
        if (accessToken is null || refreshToken is null || userId is null)
            throw new ApiException(ApiErrorKind.Server, "Unexpected response from the login server. Please try again.");

        var expiresAt = body?["expires_at"]?.GetValue<long>() is long unix and > 0
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : DateTimeOffset.UtcNow.AddSeconds(body?["expires_in"]?.GetValue<int>() ?? 3600);

        return new Session(accessToken, refreshToken, expiresAt, userId, Str(user, "email") ?? "");
    }

    private void SetSession(Session session)
    {
        Session = session;
        _store.Save(session);
    }

    private void ClearSession()
    {
        Session = null;
        _store.Clear();
    }
}
