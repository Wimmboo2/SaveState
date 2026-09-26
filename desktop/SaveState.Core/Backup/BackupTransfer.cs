using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using SaveState.Core.Api;
using SaveState.Core.Models;

namespace SaveState.Core.Backup;

public sealed record TransferProgress(string Stage, long DoneBytes, long TotalBytes)
{
    public double Fraction => TotalBytes <= 0 ? 0 : Math.Clamp((double)DoneBytes / TotalBytes, 0, 1);
}

/// <summary>
/// Moves the backup zip to and from private storage. The `backup` edge function checks the user's
/// login and hands out short-lived presigned URLs; bytes go straight to/from storage, never through
/// Supabase. The app never holds a storage key.
/// </summary>
public sealed class BackupTransfer(SupabaseApi api, HttpClient http)
{
    private const string Function = "backup";

    /// <summary>Saves the app list, uploads the zip (overwriting the previous one) and records it.</summary>
    public async Task<BackupRow> UploadAsync(string zipPath, IReadOnlyList<SavedApp> apps,
        IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        var size = new FileInfo(zipPath).Length;
        if (size > api.Config.MaxBackupBytes)
            throw new ApiException(ApiErrorKind.Rejected,
                $"Your backup is {Sizes.Format(size)}, over the {Sizes.Format(api.Config.MaxBackupBytes)} limit. Remove {Sizes.Format(size - api.Config.MaxBackupBytes)} of files and try again.");

        progress?.Report(new TransferProgress("Saving app list", 0, size));
        await api.SaveAppsAsync(apps, ct);

        // Some networks break big uploads now and then; each retry gets a fresh upload link.
        for (var attempt = 1; ; attempt++)
        {
            progress?.Report(new TransferProgress(attempt == 1 ? "Preparing upload" : $"Retrying upload ({attempt} of {UploadAttempts})", 0, size));
            try
            {
                await PutOnceAsync(zipPath, size, progress, ct);
                break;
            }
            catch (ApiException e) when (attempt < UploadAttempts && e.Kind is ApiErrorKind.Server or ApiErrorKind.Network or ApiErrorKind.Timeout)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }

        progress?.Report(new TransferProgress("Finishing", size, size));
        var confirmed = await api.InvokeFunctionAsync(Function, new { action = "confirm-upload" }, ct);
        return ParseRow(confirmed);
    }

    private const int UploadAttempts = 3;

    private async Task PutOnceAsync(string zipPath, long size, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        var ticket = await api.InvokeFunctionAsync(Function, new { action = "upload-url", size_bytes = size }, ct);
        var url = ticket?["url"]?.GetValue<string>() ?? throw Unexpected();

        using var file = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Content = new ProgressStreamContent(file, size, bytes => progress?.Report(new TransferProgress("Uploading", bytes, size)));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        request.Content.Headers.ContentLength = size;
        using var response = await SendTransferAsync(request, size, ct);
        if (response.IsSuccessStatusCode) return;

        // Say exactly what storage answered, so a failure can be tracked down.
        var status = (int)response.StatusCode;
        var body = "";
        try { body = await response.Content.ReadAsStringAsync(ct); } catch (HttpRequestException) { }
        var code = System.Text.RegularExpressions.Regex.Match(body, "<Code>([^<]+)</Code>").Groups[1].Value;
        var detail = $"(storage answered HTTP {status}{(code.Length > 0 ? $", {code}" : "")})";
        throw response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest
            ? new ApiException(ApiErrorKind.Rejected, $"The upload was refused {detail}. Please try again.", status, code)
            : new ApiException(ApiErrorKind.Server, $"Uploading failed on the storage side {detail}. Please try again in a moment.", status, code);
    }

    /// <summary>Downloads the current backup zip to <paramref name="destination"/>.</summary>
    public async Task DownloadAsync(string destination, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        var ticket = await api.InvokeFunctionAsync(Function, new { action = "download-url" }, ct);
        var url = ticket?["url"]?.GetValue<string>() ?? throw Unexpected();
        var expected = ticket?["size_bytes"]?.GetValue<long>() ?? 0;

        var temp = destination + ".part";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SendTransferAsync(request, expected, ct, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(ApiErrorKind.Server, "Downloading failed. Please try again.", (int)response.StatusCode);

            var total = response.Content.Headers.ContentLength ?? expected;
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true))
            {
                var buffer = new byte[128 * 1024];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    progress?.Report(new TransferProgress("Downloading", done, total));
                }
            }
            File.Move(temp, destination, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// Asks the server to read the file list out of the stored zip (for backups made before file
    /// lists existed). Returns the updated row, or null if the user has no row.
    /// </summary>
    public async Task<BackupRow?> FetchFileListAsync(CancellationToken ct = default)
    {
        var body = await api.InvokeFunctionAsync(Function, new { action = "file-list" }, ct);
        return body?["backup"]?.Deserialize<BackupRow>(SupabaseApi.Json);
    }

    /// <summary>Deletes the stored zip (the app list stays).</summary>
    public async Task<BackupRow> DeleteFilesAsync(CancellationToken ct = default) =>
        ParseRow(await api.InvokeFunctionAsync(Function, new { action = "delete" }, ct));

    private async Task<HttpResponseMessage> SendTransferAsync(HttpRequestMessage request, long bytes, CancellationToken ct,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        // Generous timeout: 2 minutes plus a minute per 10 MB, covers slow home uploads.
        var timeout = TimeSpan.FromMinutes(2 + bytes / (10 * 1024 * 1024d));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return await http.SendAsync(request, completion, cts.Token);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw ApiException.Timeout(e);
        }
        catch (HttpRequestException e)
        {
            throw ApiException.Network(e);
        }
    }

    private static BackupRow ParseRow(JsonNode? body) =>
        body?["backup"]?.Deserialize<BackupRow>(SupabaseApi.Json) ?? throw Unexpected();

    private static ApiException Unexpected() =>
        new(ApiErrorKind.Server, "Unexpected response from SaveState. Please try again.");

    /// <summary>Streams a file as request content while reporting how many bytes were sent.</summary>
    private sealed class ProgressStreamContent(Stream source, long length, Action<long> onProgress) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct)
        {
            var buffer = new byte[128 * 1024];
            long sent = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), ct);
                sent += read;
                onProgress(sent);
            }
        }

        protected override bool TryComputeLength(out long length1)
        {
            length1 = length;
            return true;
        }
    }
}
