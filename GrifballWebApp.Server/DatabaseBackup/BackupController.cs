using System.Security.Claims;
using GrifballWebApp.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>What the Database Backup page shows: the server the app is on and the migration a backup is at.</summary>
public sealed record BackupInfoDto(string Provider, string LatestMigration);

/// <summary>
/// Sysadmin only: the whole database as a SQLite file (<see cref="DatabaseBackupService"/>), to keep, or to load into
/// SQL Server, PostgreSQL or SQLite with the converter from the same build.
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
}
