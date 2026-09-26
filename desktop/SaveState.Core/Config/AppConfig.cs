namespace SaveState.Core.Config;

/// <summary>
/// App-wide settings. Everything here is PUBLIC by design: the Supabase URL and the publishable
/// (anon) key ship inside every client and are protected by Row Level Security.
/// Never put a service-role / secret key or any R2 credential in the desktop app.
/// </summary>
public sealed record AppConfig
{
    public required string SupabaseUrl { get; init; }
    public required string PublishableKey { get; init; }
    public required string WebsiteUrl { get; init; }

    /// <summary>Max size of the uploaded zip. Keep in sync with web/src/lib/constants.ts and the edge function.</summary>
    public long MaxBackupBytes { get; init; } = 100L * 1024 * 1024;

    /// <summary>Uploaded files are deleted this many days after upload. The app list is kept.</summary>
    public int ExpiryDays { get; init; } = 30;

    public static AppConfig Default { get; } = new()
    {
        SupabaseUrl = "https://ehitvxhdscevcvlkuegs.supabase.co",
        PublishableKey = "sb_publishable_KcjP42SE8v3nrZuEdKauZw_fxIRPBXh",
        WebsiteUrl = "https://savestate-woad.vercel.app",
    };
}
