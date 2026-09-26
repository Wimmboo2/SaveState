namespace SaveState.Core.Api;

public enum ApiErrorKind
{
    /// <summary>No internet / DNS failure / connection refused.</summary>
    Network,
    Timeout,
    /// <summary>Refresh token is gone or revoked: the user must log in again.</summary>
    SessionExpired,
    InvalidCredentials,
    UserAlreadyExists,
    WeakPassword,
    EmailNotConfirmed,
    RateLimited,
    /// <summary>The server rejected the request (4xx) for another reason.</summary>
    Rejected,
    /// <summary>5xx or an unexpected response.</summary>
    Server,
}

/// <summary>An API failure with a message that is safe and friendly to show to the user.</summary>
public sealed class ApiException(ApiErrorKind kind, string message, int? statusCode = null, string? code = null, Exception? inner = null)
    : Exception(message, inner)
{
    public ApiErrorKind Kind { get; } = kind;
    public int? StatusCode { get; } = statusCode;
    /// <summary>Machine-readable code from the server (e.g. Supabase <c>error_code</c>), if any.</summary>
    public string? Code { get; } = code;

    public static ApiException Network(Exception inner) => new(ApiErrorKind.Network,
        "Can't reach SaveState. Check your internet connection and try again.", inner: inner);

    public static ApiException Timeout(Exception inner) => new(ApiErrorKind.Timeout,
        "The server took too long to answer. Check your connection and try again.", inner: inner);

    public static ApiException SessionExpired() => new(ApiErrorKind.SessionExpired,
        "Your login has expired. Please log in again.");
}
