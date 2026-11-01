using System.Security.Claims;
using GrifballWebApp.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>What the Database Backup page shows: the server the app is on and the migration a backup is at.</summary>
public sealed record BackupInfoDto(string Provider, string LatestMigration);

/// <summary>Where to export the database: SqlServer, Postgres or Sqlite, and the new database's connection string.</summary>
public sealed record ExportRequestDto(string Provider, string ConnectionString);

/// <summary>
/// Sysadmin only: the whole database as a SQLite file (<see cref="DatabaseBackupService"/>), to keep, or to load into
/// SQL Server, PostgreSQL or SQLite with the converter from the same build; or copied straight into a new database on
/// any of the three (<see cref="Export"/>).
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
            context.Database.GetMigrations().Last());
    }

    /// <summary>The database as a new SQLite file; 409 while another backup is being made.</summary>
    [HttpGet(Name = "BackupSqlite")]
    public async Task<IActionResult> Sqlite(CancellationToken cancellationToken)
    {
        var backup = await backups.CreateAsync(cancellationToken);
        if (backup is null)
            return Conflict("A backup is already being made; try again when it's done.");
        logger.LogInformation("{User} downloaded a database backup: {Tables} tables, {Rows} rows",
            User.FindFirstValue(ClaimTypes.Name), backup.Tables.Count, backup.Tables.Sum(t => t.Rows));
        return File(backup.File, "application/vnd.sqlite3", backup.FileName);
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
