using System.Globalization;
using SaveState.Core.Backup;
using SaveState.Core.Models;

namespace SaveState.UI;

/// <summary>One plain-language answer to "is my stuff backed up?", shared by the sidebar and the Backup page.</summary>
internal sealed record BackupStatus(Ui.Tone Tone, string Glyph, string Title, string Detail)
{
    // Segoe Fluent Icons / MDL2 code points.
    private const string GlyphDone = "";     // circled check
    private const string GlyphClock = "";
    private const string GlyphDeleted = "";  // trash can
    private const string GlyphInfo = "";
    private const string GlyphSync = "";

    public static BackupStatus From(AppState state)
    {
        if (!state.BackupLoaded) return new(Ui.Tone.Info, GlyphSync, "Checking your backup…", "");

        var backup = state.Backup;
        var days = state.Api.Config.ExpiryDays;
        if (backup is null)
            return new(Ui.Tone.Info, GlyphInfo, "Not backed up yet", "Pick your apps and files, then back up.");

        var apps = Count(backup.Apps.Count, "app");

        if (backup is { HasFiles: true, UploadedAt: { } uploaded, ExpiresAt: { } expires })
        {
            var files = backup.Files is { Unavailable: false } c ? $"{Count(c.FileCount, "file")}, " : "";
            var detail = $"{When(uploaded)}\n{apps}, {files}{Sizes.Format(backup.SizeBytes)}";
            var left = Sizes.TimeLeft(expires);
            if (expires - DateTimeOffset.UtcNow < TimeSpan.FromDays(5))
                return new(Ui.Tone.Warning, GlyphClock, "Backed up", $"{detail}\nFiles expire {left}. Back up again to keep them.");
            return new(Ui.Tone.Success, GlyphDone, "Backed up", $"{detail}\nFiles kept until {expires.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture)}");
        }

        if (backup.FilesWereDeleted)
            return new(Ui.Tone.Error, GlyphDeleted, "Backup files deleted",
                $"Your files were deleted on the website ({When(backup.FilesRemovedAt!.Value)}). Your app list is still saved.");

        if (backup.FilesExpired)
            return new(Ui.Tone.Warning, GlyphClock, "Backup files expired",
                $"Removed {When(backup.FilesRemovedAt!.Value)}, {days} days after upload. Your app list is still saved.");

        return backup.Apps.Count == 0
            ? new(Ui.Tone.Info, GlyphInfo, "Not backed up yet", "Pick your apps and files, then back up.")
            : new(Ui.Tone.Success, GlyphDone, "App list saved", $"{apps} saved. No files backed up yet.");
    }

    public static string When(DateTimeOffset value) =>
        value.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);

    public static string Count(int n, string noun) => $"{n:N0} {noun}{(n == 1 ? "" : "s")}";
}
