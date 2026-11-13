using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace GrifballWebApp.Infrastructure.DatabaseBackup;

/// <summary>Why a native backup can't be made here; the message says what to change.</summary>
public sealed class NativeBackupUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The app's own server's backup, as its tools make and restore it. SQL Server: a copy-only full backup, compressed where the edition can (BACKUP DATABASE
/// ... COPY_ONLY, so the scheduled backups' chain is untouched), written by SQL Server into its backup folder as
/// download_&lt;database&gt;.bak (each download overwrites it), then read back through SQL Server (OPENROWSET BULK),
/// since only SQL Server's pod mounts that folder. Its login needs db_backupoperator and ADMINISTER BULK OPERATIONS, and
/// on Linux the folder has to be in mssql.conf's bulkadmin allowedpathslist and outside /var/opt/mssql, which bulk reads
/// never take: database.logins.app.nativeBackups gives the chart's SQL Server such a folder.
/// PostgreSQL: pg_dump's custom-format archive, without owners or grants, so it restores under any role.
/// </summary>
public static class NativeBackups
{
    /// <summary>SQL Server's folder for the download's .bak, on its side (the chart's SQL Server: its native backups folder). Empty: SQL Server's default.</summary>
    public const string SqlServerDirectoryKey = "DatabaseBackup:SqlServerDirectory";

    /// <summary>The pg_dump to run; pg_dump on the PATH unless set.</summary>
    public const string PgDumpKey = "DatabaseBackup:PgDump";

    // Permission denied: a statement (262), BACKUP DATABASE (3201 wraps it as "BACKUP DATABASE is terminating
    // abnormally" after 262).
    private static readonly int[] PermissionErrors = [262];

    /// <summary>
    /// Whether the edition makes compressed backups: Enterprise, Developer and Standard do; Web and Express don't
    /// (https://learn.microsoft.com/sql/relational-databases/backup-restore/backup-compression-sql-server). EngineEdition
    /// 3 is Enterprise, Developer or Evaluation; 2 is Standard or Web, told apart by the edition's name.
    /// </summary>
    public static bool CompressesBackups(int engineEdition, string edition) =>
        engineEdition == 3 || (engineEdition == 2 && !edition.StartsWith("Web", StringComparison.OrdinalIgnoreCase));

    /// <summary>The copy-only BACKUP DATABASE statement, compressed or not; @database and @file are its parameters.</summary>
    public static string BackupStatement(bool compress) => compress
        ? "BACKUP DATABASE @database TO DISK = @file WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, COMPRESSION, NAME = N'GrifballWebApp download'"
        : "BACKUP DATABASE @database TO DISK = @file WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, NO_COMPRESSION, NAME = N'GrifballWebApp download'";

    /// <param name="fileName">The .bak's name in the folder; download_&lt;database&gt;.bak unless given.</param>
    public static async Task SqlServerAsync(string connectionString, string? directory, string path, CancellationToken cancellationToken, string? fileName = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await SqlServerAsync(connection, directory, path, cancellationToken, fileName);
    }

    /// <summary><see cref="SqlServerAsync(string, string?, string, CancellationToken, string?)"/> on a connection made already:
    /// for the tests, which give it one of their own.</summary>
    internal static async Task SqlServerAsync(DbConnection connection, string? directory, string path, CancellationToken cancellationToken, string? fileName = null)
    {
        await connection.OpenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(directory))
        {
            await using var defaultPath = Command(connection, "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'))", timeout: null);
            // NULL (DBNull) where the instance has no default folder: a managed instance, an older server
            directory = await defaultPath.ExecuteScalarAsync(cancellationToken) as string
                ?? throw new NativeBackupUnavailableException(
                    $"SQL Server has no default backup folder, so the backup can't be made: set {SqlServerDirectoryKey}, or download a SQL Server script instead.");
        }
        // Forward slashes: SQL Server takes them on Windows too.
        var file = $"{directory.TrimEnd('/', '\\')}/{fileName ?? $"download_{connection.Database}.bak"}";

        await using var edition = Command(connection,
            "SELECT CONVERT(int, SERVERPROPERTY('EngineEdition')), CONVERT(nvarchar(128), SERVERPROPERTY('Edition'))", timeout: null);
        bool compress;
        await using (var reader = await edition.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new NativeBackupUnavailableException("SQL Server didn't say its edition, so the backup can't be made: download a SQL Server script instead.");
            compress = CompressesBackups(reader.GetInt32(0), reader.GetString(1));
        }

        try
        {
            await using var backup = Command(connection, BackupStatement(compress), timeout: 0, ("@database", connection.Database), ("@file", file));
            await backup.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException e) when (e.Errors.Cast<SqlError>().Any(error => PermissionErrors.Contains(error.Number)))
        {
            throw new NativeBackupUnavailableException(
                "The app's database login can't make SQL Server backups. Give it the rights with the chart's database.logins.app.nativeBackups " +
                "(db_backupoperator and ADMINISTER BULK OPERATIONS), or download a SQL Server script instead.", e);
        }

        try
        {
            // OPENROWSET BULK takes the path as a literal only: SQL Server writes it into the statement from the parameter, quoted
            await using var read = Command(connection,
                "DECLARE @sql nvarchar(max) = N'SELECT BulkColumn FROM OPENROWSET(BULK N''' + REPLACE(@file, N'''', N'''''') + N''', SINGLE_BLOB) AS b'; EXEC sp_executesql @sql",
                timeout: 0, ("@file", file));
            await using var reader = await read.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            // No row: nothing to save, and no empty file left behind
            if (!await reader.ReadAsync(cancellationToken))
                throw new NativeBackupUnavailableException($"SQL Server made the backup ({file}) but reading it back gave nothing. Download a SQL Server script instead.");
            await using var output = File.Create(path);
            await using var stream = reader.GetStream(0);
            await stream.CopyToAsync(output, cancellationToken);
        }
        catch (SqlException e)
        {
            // Without sysadmin, on Linux, a bulk read needs ADMINISTER BULK OPERATIONS, SQL Server 2022 CU24 or 2025 CU3
            // or later, and a folder in mssql.conf's bulkadmin allowedpathslist that isn't under /var/opt/mssql.
            throw new NativeBackupUnavailableException(
                $"SQL Server made the backup ({file}) but wouldn't read it back: {e.Message} Its login needs ADMINISTER BULK OPERATIONS, and on Linux " +
                "SQL Server 2022 CU24 or 2025 CU3 or later and a folder named in mssql.conf's bulkadmin allowedpathslist, outside /var/opt/mssql " +
                "(the chart's database.logins.app.nativeBackups sets both up on its own SQL Server). Or download a SQL Server script instead.", e);
        }
    }

    /// <summary>A command on the connection with its parameters, and its timeout: 0 (none) for the backup and its read
    /// back, which take as long as they take; null for the driver's default.</summary>
    private static DbCommand Command(DbConnection connection, string sql, int? timeout, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (timeout is { } seconds)
            command.CommandTimeout = seconds;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    /// <summary>pg_dump's arguments and environment for the app's connection string: the password in PGPASSWORD, never on its command line.</summary>
    public static ProcessStartInfo PgDumpStartInfo(string connectionString, string executable, string path)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "--format=custom", "--no-owner", "--no-privileges", "--file", path, "--dbname", builder.Database ?? "" })
            start.ArgumentList.Add(argument);
        if (builder.Host is { } host)
            start.Environment["PGHOST"] = host;
        start.Environment["PGPORT"] = builder.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (builder.Username is { } user)
            start.Environment["PGUSER"] = user;
        if (builder.Password is { } password)
            start.Environment["PGPASSWORD"] = password;
        start.Environment["PGSSLMODE"] = builder.SslMode switch
        {
            SslMode.Disable => "disable",
            SslMode.Allow => "allow",
            SslMode.Require => "require",
            SslMode.VerifyCA => "verify-ca",
            SslMode.VerifyFull => "verify-full",
            _ => "prefer",
        };
        // As the app's connection string has it (the chart's: the image has no Kerberos).
        start.Environment["PGGSSENCMODE"] = builder.GssEncryptionMode == GssEncryptionMode.Disable ? "disable" : "prefer";
        return start;
    }

    /// <summary>ETXTBSY: Linux refuses to run a file that something has open for writing.</summary>
    private const int TextFileBusy = 26;

    public static async Task PgDumpAsync(string connectionString, string executable, string path, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = PgDumpStartInfo(connectionString, executable, path) };
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                process.Start();
                break;
            }
            // "Text file busy": a process another thread was starting at that moment still held the file open for
            // writing (seen in the tests, which write their fake pg_dump while others start processes). Gone in moments.
            catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == TextFileBusy && attempt < 5)
            {
                await Task.Delay(50 * attempt, cancellationToken);
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new NativeBackupUnavailableException($"pg_dump ({executable}) isn't installed here, so a PostgreSQL dump can't be made: download a PostgreSQL script instead.", e);
            }
        }
        string errors;
        try
        {
            var reading = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            errors = await reading;
        }
        catch (OperationCanceledException)
        {
            // The download was stopped: so is pg_dump, rather than left writing.
            process.Kill(entireProcessTree: true);
            throw;
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"pg_dump failed ({process.ExitCode}): {errors.Trim()}");
    }
}
