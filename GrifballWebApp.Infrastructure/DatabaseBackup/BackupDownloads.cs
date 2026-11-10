using Microsoft.AspNetCore.DataProtection;

namespace GrifballWebApp.Infrastructure.DatabaseBackup;

/// <summary>
/// A made backup's file, from the pod that made it: this one (one pod, App:Role all), from <see cref="BackupFiles"/>; a
/// worker (several workers are fine: the run says which, its DatabaseBackup:NodeUrl), from its <see cref="WorkerPath"/> (the web host's endpoint),
/// asked with a token only the app's pods can make (Data Protection, its keys in the database), good for
/// <see cref="TokenLifetime"/>.
/// </summary>
public sealed class BackupDownloads(BackupFiles files, IHttpClientFactory httpClients, IDataProtectionProvider protection)
{
    public const string HttpClientName = "backup-worker";
    public const string WorkerPath = "/internal/backups";
    public const string TokenHeader = "X-Grif-Backup-Token";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>The download tokens' protector: the worker's endpoint checks with it what <see cref="AskWorkerAsync"/> sends.</summary>
    public static ITimeLimitedDataProtector Protector(IDataProtectionProvider protection)
        => protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector();

    /// <summary>
    /// Whether the backup's file can still be fetched: a run can say it's ready while the pod that made it restarted
    /// since (its files gone, its address another pod's or nobody's).
    /// </summary>
    public async Task<bool> ExistsAsync(ReadyBackup backup, CancellationToken ct = default)
    {
        if (backup.NodeUrl is not { Length: > 0 })
            return files.Exists(backup.FileId);
        using var response = await AskWorkerAsync(backup, HttpMethod.Head, ct);
        return response is not null;
    }

    /// <summary>The backup's file opened to read, from here or the pod that made it; null when it's gone there.</summary>
    public async Task<Stream?> OpenAsync(ReadyBackup backup, CancellationToken ct = default)
    {
        if (backup.NodeUrl is not { Length: > 0 })
            return files.Open(backup.FileId);
        return await AskWorkerAsync(backup, HttpMethod.Get, ct) is { } response
            ? new ResponseStream(response, await response.Content.ReadAsStreamAsync(ct))
            : null;
    }

    /// <summary>
    /// The worker's answer for the backup; null when it isn't there: not found, or no answer, or one from another pod
    /// (401: the worker restarted and its address is someone else's now).
    /// </summary>
    private async Task<HttpResponseMessage?> AskWorkerAsync(ReadyBackup backup, HttpMethod method, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(backup.NodeUrl!), $"{WorkerPath}/{Uri.EscapeDataString(backup.FileId)}"));
        request.Headers.Add(TokenHeader, Protector(protection).Protect(backup.FileId, TokenLifetime));
        HttpResponseMessage response;
        try
        {
            response = await httpClients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception e) when (e is HttpRequestException || (e is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
        if (response.IsSuccessStatusCode)
            return response;
        response.Dispose();
        return null;
    }

    /// <summary>The worker's response body, closing the response with it.</summary>
    private sealed class ResponseStream(HttpResponseMessage response, Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
