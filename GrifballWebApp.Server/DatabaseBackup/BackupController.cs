using System.Security.Claims;
using GrifballWebApp.Application.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure;

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
[ApiController]
[Authorize(Roles = "Sysadmin")]
public partial class BackupController(DatabaseBackupService backups, BackupRuns runs, BackupDownloads downloads, IApplicationDbContext context, IConfiguration configuration,
    ILogger<BackupController> logger) : ControllerBase
{
    /// <summary>The database's provider, its latest migration, and the backup formats available for it. Sysadmins only.</summary>
    [HttpGet("backups/info", Name = "BackupInfo")]
    [ProducesResponseType<BackupInfoDto>(StatusCodes.Status200OK)]
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
    /// runs; 409 when there's no server here to make that native backup on (the message says why).
    /// </summary>
    [HttpPost("backups/{format}", Name = "BackupStartDownload")]
    [ProducesResponseType<BackupDownloadStatus>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> StartDownload([FromRoute] string format, CancellationToken ct)
    {
        if (!TryFormat(format, out var parsed))
            return this.BadRequestProblem(BadFormat);
        if (parsed is BackupFormat.Bak or BackupFormat.Dump)
        {
            try
            {
                backups.RequireNativeServer(parsed);
            }
            catch (NativeBackupUnavailableException e)
            {
                return this.ConflictProblem(e.Message);
            }
        }
        var user = User.FindFirstValue(ClaimTypes.Name);
        if (await runs.StartDownloadAsync(parsed, user, JobUser.IdOf(User), ct) is not { } status)
            return this.ConflictProblem("A backup or export is already running; try again when it's done.");
        LogBackupStarted(logger, user, parsed);
        return Accepted(status);
    }

    private const string BadFormat = "The format must be sqlite, postgres, sqlserver, bak or dump.";

    private static bool TryFormat(string? format, out BackupFormat parsed)
        => Enum.TryParse(format, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    /// <summary>The format's backup (one kept a format): being made, made, or failed; and the format being made now, whichever. 400 for another format.</summary>
    [HttpGet("backups/{format}/status", Name = "BackupDownloadStatus")]
    [ProducesResponseType<BackupDownloadStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BackupDownloadStatus>> DownloadStatus([FromRoute] string format, CancellationToken ct)
    {
        if (!TryFormat(format, out var parsed))
            return this.BadRequestProblem(BadFormat);
        var status = await runs.DownloadStatusAsync(parsed, ct);
        // The run says it's ready, but the pod that made it may have restarted since, its file gone with it.
        if (status.State == ExportState.Succeeded && (await runs.ReadyAsync(parsed, ct) is not { } ready || !await downloads.ExistsAsync(ready, ct)))
            return new BackupDownloadStatus(ExportState.None, status.Format, Message: Gone, RunningFormat: status.RunningFormat);
        return status;
    }

    /// <summary>What the page says of a backup a restart took: there's none ready anymore.</summary>
    public const string Gone = "The last backup in this format is gone: the server restarted since it was made. Make a new one.";

    /// <summary>The format's backup <see cref="StartDownload"/> made, once it's made; 404 when there's none (not made, or discarded since). 400 for another format.</summary>
    [HttpGet("backups/{format}/file", Name = "BackupDownload")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Download([FromRoute] string format, CancellationToken ct)
    {
        const string none = "No backup is ready in this format: make one first. A backup is kept for an hour, or until the next one of its format.";
        if (!TryFormat(format, out var parsed))
            return this.BadRequestProblem(BadFormat);
        if (await runs.ReadyAsync(parsed, ct) is not { } ready || await downloads.OpenAsync(ready, ct) is not { } file)
            return this.NotFoundProblem(none + " (A restart of the server discards them too.)");
        var status = await runs.DownloadStatusAsync(parsed, ct);
        var user = User.FindFirstValue(ClaimTypes.Name);
        LogBackupDownloaded(logger, user, ready.FileName, status.Tables, status.Rows);
        return File(file, ready.ContentType, ready.FileName);
    }

    /// <summary>
    /// Queues copying the database into a new one (missing, or without tables) on the given server: 202, then
    /// <see cref="ExportStatus"/> follows it. 409 while a backup or export is queued or runs; 400 for an unknown
    /// provider or a connection string it can't read. The connection string is used for this export only: never kept
    /// or logged, and the job it's handed to gets it protected (readable for an hour).
    /// </summary>
    [HttpPost("database-exports", Name = "BackupExport")]
    [ProducesResponseType<BackupExportStatus>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Export([FromBody] ExportRequestDto request, CancellationToken ct)
    {
        if (!Enum.TryParse<DatabaseProvider>(request.Provider, ignoreCase: true, out var provider) || !Enum.IsDefined(provider))
            return this.BadRequestProblem("The provider must be SqlServer, Postgres or Sqlite.");
        var user = User.FindFirstValue(ClaimTypes.Name);
        BackupExportStatus? status;
        try
        {
            status = await runs.StartExportAsync(provider, request.ConnectionString, user, JobUser.IdOf(User), ct);
        }
        catch (ArgumentException e)
        {
            return this.BadRequestProblem($"The connection string isn't one {provider} reads: {e.Message}");
        }
        if (status is null)
            return this.ConflictProblem("A backup or export is already running; try again when it's done.");
        LogExportStarted(logger, user, provider);
        return Accepted(status);
    }

    /// <summary>The currently queued or last database export's status. Sysadmins only.</summary>
    [HttpGet("database-exports/status", Name = "BackupExportStatus")]
    [ProducesResponseType<BackupExportStatus>(StatusCodes.Status200OK)]
    public Task<BackupExportStatus> ExportStatus(CancellationToken ct) => runs.ExportStatusAsync(ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "{User} started a database backup ({Format})")]
    private static partial void LogBackupStarted(ILogger logger, string? user, BackupFormat format);

    [LoggerMessage(Level = LogLevel.Information, Message = "{User} downloaded a database backup ({FileName}): {Tables} tables, {Rows} rows")]
    private static partial void LogBackupDownloaded(ILogger logger, string? user, string fileName, int? tables, int? rows);

    [LoggerMessage(Level = LogLevel.Information, Message = "{User} started a database export to {Provider}")]
    private static partial void LogExportStarted(ILogger logger, string? user, DatabaseProvider provider);
}
