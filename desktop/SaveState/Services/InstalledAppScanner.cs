using Microsoft.Win32;
using SaveState.Core.Apps;

namespace SaveState.Services;

/// <summary>Reads installed programs from the three Uninstall registry locations Windows uses.</summary>
internal static class InstalledAppScanner
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow64UninstallPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public static List<InstalledApp> Scan()
    {
        var entries = new List<UninstallEntry>();
        // 64-bit view explicitly, so a 32-bit process wouldn't be silently redirected to WOW6432Node.
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, UninstallPath, entries);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, Wow64UninstallPath, entries);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Default, UninstallPath, entries);
        return AppFilter.Clean(entries);
    }

    private static void ReadHive(RegistryHive hive, RegistryView view, string path, List<UninstallEntry> into)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = root.OpenSubKey(path);
            if (uninstall is null) return;

            foreach (var name in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var key = uninstall.OpenSubKey(name);
                    if (key is null) continue;
                    into.Add(new UninstallEntry
                    {
                        DisplayName = key.GetValue("DisplayName") as string,
                        Publisher = key.GetValue("Publisher") as string,
                        DisplayVersion = key.GetValue("DisplayVersion") as string,
                        SystemComponent = key.GetValue("SystemComponent") is int flag ? flag : null,
                        ParentKeyName = key.GetValue("ParentKeyName") as string,
                        ReleaseType = key.GetValue("ReleaseType") as string,
                    });
                }
                catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    // Some vendor keys have locked-down ACLs; skip them.
                }
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Warn($"Couldn't read {hive}\\{path}: {e.Message}");
        }
    }
}
