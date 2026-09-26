using System.Globalization;
using System.Text;

namespace SaveState.Core.Backup;

/// <summary>README.txt inside the zip: plain-English restore instructions a person can follow.</summary>
public static class ReadmeWriter
{
    public static string Write(Manifest manifest)
    {
        var sb = new StringBuilder();
        var nl = "\r\n"; // Notepad-friendly line endings
        void Line(string text = "") => sb.Append(text).Append(nl);

        Line("SaveState backup");
        Line($"Created {manifest.CreatedAtUtc.ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture)} (UTC)");
        Line();
        Line("HOW TO PUT YOUR FILES BACK");
        Line("1. Unzip this file somewhere easy to find, like your Desktop.");
        Line("2. Close the app the files belong to (for example Minecraft) before copying.");
        Line("3. For each item below, copy what is in the \"from\" folder of this zip");
        Line("   to the \"to\" location on your PC.");
        Line("   Tip: to open a location like %APPDATA%, paste it into the File Explorer");
        Line("   address bar (or the Win+R box) and press Enter.");
        Line();

        if (manifest.Items.Count > 0)
        {
            Line("YOUR FILES");
            foreach (var item in manifest.Items)
            {
                var what = item.Kind == "folder" ? $"folder, {item.FileCount} {(item.FileCount == 1 ? "file" : "files")}" : "file";
                Line($"  from: {item.ZipPath.TrimEnd('/').Replace('/', '\\')}   ({what}, {Sizes.Format(item.SizeBytes)})");
                Line($"    to: {item.OriginalPath}");
                Line();
            }
        }
        else
        {
            Line("No files were included in this backup, only your app list.");
            Line();
        }

        Line($"APPS YOU SAVED ({manifest.Apps.Count})");
        if (manifest.Apps.Count == 0) Line("  (none)");
        foreach (var app in manifest.Apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var meta = string.Join(", ", new[] { app.Publisher, app.Version is null ? null : $"v{app.Version}" }.Where(s => !string.IsNullOrWhiteSpace(s)));
            Line(meta.Length > 0 ? $"  - {app.Name} ({meta})" : $"  - {app.Name}");
            if (!string.IsNullOrWhiteSpace(app.Note)) Line($"      Note: {app.Note}");
        }
        Line();
        Line("manifest.json lists every single file with its original location and size.");
        Line("Your app list is also on the SaveState website any time you log in.");
        return sb.ToString();
    }
}
