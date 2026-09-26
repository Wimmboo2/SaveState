using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SaveState.Core.Models;

namespace SaveState.Core.Backup;

/// <summary>A file that couldn't be read because another program has it open (e.g. Minecraft is running).</summary>
public sealed class FileLockedException(string path, Exception inner)
    : IOException($"\"{System.IO.Path.GetFileName(path)}\" is in use by another program. Close it (for example Minecraft) and try again.", inner)
{
    public string Path { get; } = path;
}

/// <summary>Any other problem building the zip, with a message that's safe to show.</summary>
public sealed class BackupBuildException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record BuildProgress(string Stage, long DoneBytes, long TotalBytes, string? CurrentFile = null)
{
    public double Fraction => TotalBytes <= 0 ? 0 : Math.Clamp((double)DoneBytes / TotalBytes, 0, 1);
}

/// <summary>What will go into the zip, measured before building.</summary>
public sealed record BackupPlan(IReadOnlyList<PlannedItem> Items, IReadOnlyList<string> Missing)
{
    public long TotalBytes => Items.Sum(i => i.SizeBytes);
    public int FileCount => Items.Sum(i => i.Files.Count);
}

public sealed record PlannedItem(string SourcePath, bool IsFolder, IReadOnlyList<PlannedFile> Files)
{
    public long SizeBytes => Files.Sum(f => f.SizeBytes);
}

public sealed record PlannedFile(string FullPath, long SizeBytes, DateTime LastWriteTimeUtc);

public sealed record BackupResult(string ZipPath, long ZipBytes, int FileCount, long RawBytes, Manifest Manifest);

/// <summary>
/// Builds the backup zip: the picked files (organised by known folder so nothing collides),
/// plus manifest.json and README.txt explaining where each file goes back.
/// </summary>
public sealed class BackupBuilder(IReadOnlyList<PathRoot> roots, string generator = "SaveState")
{
    private static readonly HashSet<string> AlreadyCompressed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".jar", ".7z", ".rar", ".gz", ".xz", ".bz2", ".png", ".jpg", ".jpeg", ".webp", ".gif",
        ".ogg", ".mp3", ".mp4", ".m4a", ".webm", ".flac", ".mkv", ".cab", ".msi",
    };

    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Walks the picked paths and measures them. Skips symlinks/junctions (no loops, no following
    /// links outside the picked folder) and files already covered by another pick.
    /// </summary>
    public static BackupPlan Plan(IEnumerable<string> sources, CancellationToken ct = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<PlannedItem>();
        var missing = new List<string>();

        // Folders first, so a file inside a picked folder isn't counted twice.
        var ordered = sources
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(Directory.Exists);

        foreach (var source in ordered)
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(source))
            {
                var files = new List<PlannedFile>();
                foreach (var file in EnumerateFiles(source, ct))
                    if (seen.Add(file.FullName)) files.Add(new(file.FullName, file.Length, file.LastWriteTimeUtc));
                items.Add(new PlannedItem(source, true, files));
            }
            else if (File.Exists(source))
            {
                var info = new FileInfo(source);
                if (seen.Add(info.FullName))
                    items.Add(new PlannedItem(source, false, [new(info.FullName, info.Length, info.LastWriteTimeUtc)]));
            }
            else
            {
                missing.Add(source);
            }
        }

        return new BackupPlan(items, missing);
    }

    /// <summary>Writes the zip to <paramref name="zipPath"/>. Deletes the partial file on failure.</summary>
    public BackupResult Build(BackupPlan plan, IReadOnlyList<SavedApp> apps, string zipPath,
        IProgress<BuildProgress>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        var manifestFiles = new List<ManifestFile>();
        var manifestItems = new List<ManifestItem>();
        long done = 0;
        var total = plan.TotalBytes;
        var buffer = new byte[128 * 1024];

        try
        {
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                var usedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in plan.Items)
                {
                    foreach (var file in item.Files)
                    {
                        ct.ThrowIfCancellationRequested();
                        var entryName = UniqueEntryName(PathTokens.ZipPath(file.FullPath, roots), usedEntries);
                        progress?.Report(new BuildProgress("Zipping", done, total, Path.GetFileName(file.FullPath)));
                        done += CopyIntoZip(zip, file, entryName, buffer, ct, bytes => progress?.Report(new BuildProgress("Zipping", done + bytes, total)));
                        manifestFiles.Add(new ManifestFile(PathTokens.Tokenize(file.FullPath, roots), entryName, file.SizeBytes));
                    }

                    manifestItems.Add(new ManifestItem(
                        item.IsFolder ? "folder" : "file",
                        PathTokens.Tokenize(item.SourcePath, roots),
                        PathTokens.ZipPath(item.SourcePath, roots) + (item.IsFolder ? "/" : ""),
                        item.Files.Count,
                        item.SizeBytes));
                }

                var manifest = new Manifest
                {
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    Generator = generator,
                    Apps = apps,
                    Items = manifestItems,
                    Files = manifestFiles,
                };
                WriteText(zip, "manifest.json", JsonSerializer.Serialize(manifest, ManifestJson));
                WriteText(zip, "README.txt", ReadmeWriter.Write(manifest));

                progress?.Report(new BuildProgress("Finishing", total, total));
                zip.Dispose();
                var zipBytes = new FileInfo(zipPath).Length;
                return new BackupResult(zipPath, zipBytes, manifestFiles.Count, total, manifest);
            }
        }
        catch
        {
            TryDelete(zipPath);
            throw;
        }
    }

    private static long CopyIntoZip(ZipArchive zip, PlannedFile file, string entryName, byte[] buffer,
        CancellationToken ct, Action<long> onBytes)
    {
        var level = AlreadyCompressed.Contains(Path.GetExtension(file.FullPath)) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
        FileStream source;
        try
        {
            // ReadWrite|Delete sharing lets us read files that other apps have open for writing (logs, options.txt).
            source = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, buffer.Length);
        }
        catch (IOException e) when (IsLockViolation(e))
        {
            throw new FileLockedException(file.FullPath, e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new BackupBuildException($"Windows won't let SaveState read \"{file.FullPath}\". Remove it from the list or check its permissions.", e);
        }
        catch (FileNotFoundException e)
        {
            throw new BackupBuildException($"\"{file.FullPath}\" was moved or deleted while backing up. Try again.", e);
        }
        catch (DirectoryNotFoundException e)
        {
            throw new BackupBuildException($"\"{file.FullPath}\" was moved or deleted while backing up. Try again.", e);
        }

        using (source)
        {
            var entry = zip.CreateEntry(entryName, level);
            entry.LastWriteTime = ZipTimestamp(file.LastWriteTimeUtc);
            using var target = entry.Open();
            long copied = 0;
            int read;
            try
            {
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    target.Write(buffer, 0, read);
                    copied += read;
                    onBytes(copied);
                }
            }
            catch (IOException e) when (IsLockViolation(e))
            {
                // Byte-range locks (some games lock their files while running) surface on read.
                throw new FileLockedException(file.FullPath, e);
            }
            return file.SizeBytes;
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string folder, CancellationToken ct)
    {
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(folder));
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            FileSystemInfo[] children;
            try { children = dir.GetFileSystemInfos(); }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException) { continue; }

            foreach (var child in children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (child.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue; // symlinks, junctions, cloud placeholders
                if (child is DirectoryInfo sub) stack.Push(sub);
                else if (child is FileInfo file) yield return file;
            }
        }
    }

    /// <summary>Zip timestamps can't go below 1980 (DOS epoch); clamp odd file dates instead of failing.</summary>
    private static DateTimeOffset ZipTimestamp(DateTime utc)
    {
        var value = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        return value.Year is >= 1980 and <= 2107 ? value : DateTimeOffset.UtcNow;
    }

    private static string UniqueEntryName(string name, HashSet<string> used)
    {
        if (used.Add(name)) return name;
        var ext = Path.GetExtension(name);
        var stem = name[..^ext.Length];
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem} ({i}){ext}";
            if (used.Add(candidate)) return candidate;
        }
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    /// <summary>ERROR_SHARING_VIOLATION (32) or ERROR_LOCK_VIOLATION (33).</summary>
    private static bool IsLockViolation(IOException e) => (e.HResult & 0xFFFF) is 32 or 33;

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
