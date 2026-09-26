using System.Net;
using SaveState.Core.Api;
using SaveState.Core.Config;
using SaveState.Services;
using SaveState.UI;

namespace SaveState;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);

        using var http = CreateHttpClient();
        var api = new SupabaseApi(http, AppConfig.Default, new DpapiSessionStore());
        Log.Info("SaveState started");

        string? loginMessage = null;
        if (api.IsLoggedIn)
        {
            // Validate/refresh the saved session before showing anything. Offline is fine: keep the
            // session and let pages show a "can't reach SaveState" message with retry.
            try
            {
                if (!Task.Run(() => api.RestoreSessionAsync()).GetAwaiter().GetResult())
                    loginMessage = "Your login has expired. Please log in again.";
            }
            catch (ApiException e)
            {
                Log.Warn($"Session restore failed: {e.Kind}");
            }
        }

        while (true)
        {
            if (!api.IsLoggedIn)
            {
                using var login = new LoginForm(api, loginMessage);
                if (login.ShowDialog() != DialogResult.OK) return;
            }

            var main = new MainForm(new AppState(api));
            Application.Run(main);

            if (main.SessionExpired) { loginMessage = "Your login has expired. Please log in again."; continue; }
            if (main.SignedOut) { loginMessage = null; continue; }
            return;
        }
    }

    /// <summary>Called by pages when the API reports the session is gone.</summary>
    public static void RequestRelogin(Form? form)
    {
        if (form is MainForm main) main.ExpireSession();
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
        };
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }; // per-request timeouts instead
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"SaveState-Desktop/{Application.ProductVersion.Split('+')[0]}");
        return http;
    }

    private static void ReportCrash(Exception? e)
    {
        Log.Error("Unhandled exception", e);
        try
        {
            Ui.Error(null, "SaveState ran into a problem",
                $"Something unexpected went wrong. Details were saved to:\n{AppPaths.LogFile}\n\nIf it keeps happening, restart SaveState.");
        }
        catch { /* never throw from the crash handler */ }
    }
}
