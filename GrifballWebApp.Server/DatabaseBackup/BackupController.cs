using System.Security.Claims;
using GrifballWebApp.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>What the Database Backup page shows: the server the app is on, the migration a backup is at, and the formats it can download.</summary>
public sealed record BackupInfoDto(string Provider, string LatestMigration, IReadOnlyList<string> Formats);

/// <summary>Where to export the database: SqlServer, Postgres or Sqlite, and the new database's connection string.</summary>
public sealed record ExportRequestDto(string Provider, string ConnectionString);

/// <summary>
/// Sysadmin only: the whole database as a download (<see cref="StartDownload"/>): a SQLite file, a script for PostgreSQL or
/// SQL Server, or the server's native backup; or copied straight into a new database on any of the three
/// (<see cref="Export"/>). Both run as jobs (BackupJobs), on the pod that runs the jobs, one at a time; the download is
/// fetched from there (BackupDownloads).
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class BackupController(DatabaseBackupService backups, BackupRuns runs, BackupDownloads downloads, GrifballContext context, IConfiguration configuration,
    ILogger<BackupController> logger) : ControllerBase
{
    [HttpGet(Name = "BackupInfo")]
    public BackupInfoDto Info()
    {
        // The build's last migration: a backup has exactly this build's migrations, and loads with its converter.
        return new BackupInfoDto(configuration.GetDatabaseProvider().ToString(),
            context.Database.GetMigrations().Last(), backups.Formats.Select(f => f.ToString().ToLowerInvariant()).ToList());
    }

    /// <summary>
    /// Queues making the database as a new file: format sqlite, postgres or sqlserver (a script), bak or dump (native).
    /// 202, then <see cref="DownloadStatus"/> follows it and <see cref="Download"/> hands it over: a large one takes
    /// longer than a proxy waits for a response. 400 for another format; 409 while another backup or export is queued or
    /// runs; 422 when there's no server here to make that native backup on (the message says why).
    /// </summary>
    [HttpPost(Name = "BackupStartDownload")]
    public async Task<IActionResult> StartDownload([FromQuery] string format, CancellationToken ct)
    {
        if (!TryFormat(format, out var parsed))
            return BadRequest(BadFormat);
        if (parsed is BackupFormat.Bak or BackupFormat.Dump)
        {
            try
            {
                backups.RequireNativeSource(parsed, out _);
            }
            catch (NativeBackupUnavailableException e)
            {
                return UnprocessableEntity(e.Message);
            }
        }
        var user = User.FindFirstValue(ClaimTypes.Name);
        if (await runs.StartDownloadAsync(parsed, user, ct) is not { } status)
            return Conflict("A backup or export is already running; try again when it's done.");
        logger.LogInformation("{User} started a database backup ({Format})", user, parsed);
        return Accepted(status);
    }

    private const string BadFormat = "The format must be sqlite, postgres, sqlserver, bak or dump.";

    private static bool TryFormat(string? format, out BackupFormat parsed)
        => Enum.TryParse(format, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    /// <summary>The format's backup (one kept a format): being made, made, or failed; and the format being made now, whichever. 400 for another format.</summary>
    [HttpGet(Name = "BackupDownloadStatus")]
    public async Task<ActionResult<BackupDownloadStatus>> DownloadStatus([FromQuery] string format, CancellationToken ct)
        => TryFormat(format, out var parsed) ? await runs.DownloadStatusAsync(parsed, ct) : BadRequest(BadFormat);

    /// <summary>The format's backup <see cref="StartDownload"/> made, once it's made; 404 when there's none (not made, or discarded since). 400 for another format.</summary>
    [HttpGet(Name = "BackupDownload")]
    public async Task<IActionResult> Download([FromQuery] string format, CancellationToken ct)
    {
        const string none = "No backup is ready in this format: make one first. A backup is kept for an hour, or until the next one of its format.";
        if (!TryFormat(format, out var parsed))
            return BadRequest(BadFormat);
        if (await runs.ReadyAsync(parsed, ct) is not { } ready || await downloads.OpenAsync(ready, ct) is not { } file)
            return NotFound(none);
        var status = await runs.DownloadStatusAsync(parsed, ct);
        logger.LogInformation("{User} downloaded a database backup ({FileName}): {Tables} tables, {Rows} rows",
            User.FindFirstValue(ClaimTypes.Name), ready.FileName, status.Tables, status.Rows);
        return File(file, ready.ContentType, ready.FileName);
    }

    /// <summary>
    /// Queues copying the database into a new one (missing, or without tables) on the given server: 202, then
    /// <see cref="ExportStatus"/> follows it. 409 while a backup or export is queued or runs; 400 for an unknown
    /// provider or a connection string it can't read. The connection string is used for this export only: never kept
    /// or logged, and the job it's handed to gets it protected (readable for an hour).
    /// </summary>
    [HttpPost(Name = "BackupExport")]
    public async Task<IActionResult> Export([FromBody] ExportRequestDto request, CancellationToken ct)
    {
        if (!Enum.TryParse<DatabaseProvider>(request.Provider, ignoreCase: true, out var provider) || !Enum.IsDefined(provider))
            return BadRequest("The provider must be SqlServer, Postgres or Sqlite.");
        var user = User.FindFirstValue(ClaimTypes.Name);
        BackupExportStatus? status;
        try
        {
            status = await runs.StartExportAsync(provider, request.ConnectionString, user, ct);
        }
        catch (ArgumentException e)
        {
            return BadRequest($"The connection string isn't one {provider} reads: {e.Message}");
        }
        if (status is null)
            return Conflict("A backup or export is already running; try again when it's done.");
        logger.LogInformation("{User} started a database export to {Provider}", user, provider);
        return Accepted(status);
    }

    [HttpGet(Name = "BackupExportStatus")]
    public Task<BackupExportStatus> ExportStatus(CancellationToken ct) => runs.ExportStatusAsync(ct);
}
