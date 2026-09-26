using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SaveState.Core.Models;

namespace SaveState.Services;

/// <summary>
/// Persists the login session between launches, encrypted with Windows DPAPI (CurrentUser scope):
/// only this Windows account on this PC can decrypt it.
/// </summary>
internal sealed class DpapiSessionStore : ISessionStore
{
    // Extra entropy so other apps using DPAPI for the same user can't trivially decrypt our blob.
    private static readonly byte[] Entropy = "SaveState.session.v1"u8.ToArray();

    public Session? Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SessionFile)) return null;
            var encrypted = File.ReadAllBytes(AppPaths.SessionFile);
            var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Session>(json);
        }
        catch (Exception e) when (e is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt, copied from another PC/user, or unreadable: treat as logged out.
            Log.Warn($"Couldn't read saved session: {e.GetType().Name}");
            Clear();
            return null;
        }
    }

    public void Save(Session session)
    {
        AppPaths.EnsureRoot();
        var json = JsonSerializer.SerializeToUtf8Bytes(session);
        var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        var temp = AppPaths.SessionFile + ".tmp";
        File.WriteAllBytes(temp, encrypted);
        File.Move(temp, AppPaths.SessionFile, overwrite: true);
    }

    public void Clear()
    {
        try { File.Delete(AppPaths.SessionFile); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
