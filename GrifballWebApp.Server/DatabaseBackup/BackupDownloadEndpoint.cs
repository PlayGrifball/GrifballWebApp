using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>The worker's end of <see cref="BackupDownloads"/>: its <see cref="BackupDownloads.WorkerPath"/>, mapped by Program on a worker.</summary>
public static class BackupDownloadEndpoint
{
    /// <summary>
    /// The worker's side (App:Role worker): the kept backup with the ID (HEAD: whether it's there), for an API pod that
    /// brings a token for it. 401 without a valid one; 404 when the file is gone.
    /// </summary>
    public static IResult Serve(string fileId, HttpRequest request, BackupFiles files, IDataProtectionProvider protection)
    {
        try
        {
            if (request.Headers[BackupDownloads.TokenHeader].ToString() is not { Length: > 0 } token || BackupDownloads.Protector(protection).Unprotect(token) != fileId)
                return Results.Unauthorized();
        }
        catch (CryptographicException)
        {
            return Results.Unauthorized();
        }
        if (HttpMethods.IsHead(request.Method))
            return files.Exists(fileId) ? Results.Ok() : Results.NotFound();
        return files.Open(fileId) is { } file ? Results.Stream(file, "application/octet-stream") : Results.NotFound();
    }
}
