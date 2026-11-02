using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>
/// A made backup's file, from the pod that made it: this one (one pod, App:Role all), from <see cref="BackupFiles"/>; a
/// worker (several workers are fine: the run says which, its DatabaseBackup:NodeUrl), from its <see cref="WorkerPath"/>,
/// asked with a token only the app's pods can make (Data Protection, its keys in the database), good for
/// <see cref="TokenLifetime"/>.
/// </summary>
public sealed class BackupDownloads(BackupFiles files, IHttpClientFactory httpClients, IDataProtectionProvider protection)
{
    public const string HttpClientName = "backup-worker";
    public const string WorkerPath = "/internal/backups";
    public const string TokenHeader = "X-Grif-Backup-Token";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider protection)
        => protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector();

    /// <summary>The backup's file opened to read, from here or the pod that made it; null when it's gone there.</summary>
    public async Task<Stream?> OpenAsync(ReadyBackup backup, CancellationToken ct = default)
    {
        if (backup.NodeUrl is not { Length: > 0 } nodeUrl)
            return files.Open(backup.FileId);

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(nodeUrl), $"{WorkerPath}/{Uri.EscapeDataString(backup.FileId)}"));
        request.Headers.Add(TokenHeader, Protector(protection).Protect(backup.FileId, TokenLifetime));
        var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }
        response.EnsureSuccessStatusCode();
        return new ResponseStream(response, await response.Content.ReadAsStreamAsync(ct));
    }

    /// <summary>
    /// The worker's side (App:Role worker): the kept backup with the ID, for an API pod that brings a token for it. 401
    /// without a valid one; 404 when the file is gone.
    /// </summary>
    public static IResult Serve(string fileId, HttpRequest request, BackupFiles files, IDataProtectionProvider protection)
    {
        try
        {
            if (request.Headers[TokenHeader].ToString() is not { Length: > 0 } token || Protector(protection).Unprotect(token) != fileId)
                return Results.Unauthorized();
        }
        catch (CryptographicException)
        {
            return Results.Unauthorized();
        }
        return files.Open(fileId) is { } file ? Results.Stream(file, "application/octet-stream") : Results.NotFound();
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
