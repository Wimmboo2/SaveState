namespace SaveState.Core.Models;

/// <summary>A logged-in Supabase Auth session.</summary>
public sealed record Session(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string UserId,
    string Email)
{
    /// <summary>True when the access token expires within <paramref name="margin"/>.</summary>
    public bool ExpiresWithin(TimeSpan margin, DateTimeOffset? now = null) =>
        ExpiresAt - (now ?? DateTimeOffset.UtcNow) <= margin;
}

/// <summary>Where the session is persisted between launches (DPAPI-encrypted file on Windows).</summary>
public interface ISessionStore
{
    Session? Load();
    void Save(Session session);
    void Clear();
}

/// <summary>Non-persistent store, used by tests and as a fallback.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private Session? _session;
    public Session? Load() => _session;
    public void Save(Session session) => _session = session;
    public void Clear() => _session = null;
}
