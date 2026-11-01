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
/// (<see cref="Export"/>).
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class BackupController(DatabaseBackupService backups, GrifballContext context, IConfiguration configuration, ILogger<BackupController> logger) : ControllerBase
{
    [HttpGet(Name = "BackupInfo")]
    public BackupInfoDto Info()
    {
        // The build's last migration: a backup has exactly this build's migrations, and loads with its converter.
        return new BackupInfoDto(configuration.GetDatabaseProvider().ToString(),
            context.Database.GetMigrations().Last(), backups.Formats.Select(f => f.ToString().ToLowerInvariant()).ToList());
    }

    /// <summary>
    /// Starts making the database as a new file, in the background: format sqlite, postgres or sqlserver (a script), bak
    /// or dump (native). 202, then <see cref="DownloadStatus"/> follows it and <see cref="Download"/> hands it over: a
    /// large one takes longer than a proxy waits for a response. 400 for another format; 409 while another backup or
    /// export runs; 422 when there's no server here to make that native backup on (the message says why).
    /// </summary>
    [HttpPost(Name = "BackupStartDownload")]
    public IActionResult StartDownload([FromQuery] string format)
    {
        if (!Enum.TryParse<BackupFormat>(format, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return BadRequest("The format must be sqlite, postgres, sqlserver, bak or dump.");
        Task? backup;
        try
        {
            backup = backups.StartDownload(parsed);
        }
        catch (NativeBackupUnavailableException e)
        {
            return UnprocessableEntity(e.Message);
        }
        if (backup is null)
            return Conflict("A backup or export is already running; try again when it's done.");
        logger.LogInformation("{User} started a database backup ({Format})", User.FindFirstValue(ClaimTypes.Name), parsed);
        return Accepted(backups.Download);
    }

    [HttpGet(Name = "BackupDownloadStatus")]
    public BackupDownloadStatus DownloadStatus() => backups.Download;

    /// <summary>The backup <see cref="StartDownload"/> made, once it's made; 404 when there's none (not made, or discarded since).</summary>
    [HttpGet(Name = "BackupDownload")]
    public IActionResult Download()
    {
        var ready = backups.OpenReady();
        if (ready is null)
            return NotFound("No backup is ready: make one first. A backup is kept for an hour, or until the next one starts.");
        var status = backups.Download;
        logger.LogInformation("{User} downloaded a database backup ({FileName}): {Tables} tables, {Rows} rows",
            User.FindFirstValue(ClaimTypes.Name), ready.FileName, status.Tables, status.Rows);
        return File(ready.File, ready.ContentType, ready.FileName);
    }

    /// <summary>
    /// Starts copying the database into a new one (missing, or without tables) on the given server: 202, then
    /// <see cref="ExportStatus"/> follows it. 409 while a backup or export runs; 400 for an unknown provider or a
    /// connection string it can't read. The connection string is used for this export only: never kept or logged.
    /// </summary>
    [HttpPost(Name = "BackupExport")]
    public IActionResult Export([FromBody] ExportRequestDto request)
    {
        if (!Enum.TryParse<DatabaseProvider>(request.Provider, ignoreCase: true, out var provider) || !Enum.IsDefined(provider))
            return BadRequest("The provider must be SqlServer, Postgres or Sqlite.");
        Task? export;
        try
        {
            export = backups.StartExport(provider, request.ConnectionString);
        }
        catch (ArgumentException e)
        {
            return BadRequest($"The connection string isn't one {provider} reads: {e.Message}");
        }
        if (export is null)
            return Conflict("A backup or export is already running; try again when it's done.");
        logger.LogInformation("{User} started a database export to {Provider}", User.FindFirstValue(ClaimTypes.Name), provider);
        return Accepted(backups.Export);
    }

    [HttpGet(Name = "BackupExportStatus")]
    public BackupExportStatus ExportStatus() => backups.Export;
}
