using DotNet.Testcontainers.Containers;
using GrifballWebApp.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The database server the tests run against: GRIF_TEST_DATABASE=Postgres or Sqlite, else SQL Server. CI
/// runs the suite on all three. Each test gets its own database on the one server; on SQLite, which has
/// no server (a null one here), its own file in a folder of the run's.
/// </summary>
internal static class TestDatabase
{
    /// <summary>A new test database on the run's server (<see cref="SetUpFixture.DatabaseContainer"/>), and a context on it.</summary>
    internal static Task<GrifballContext> NewGrifballContext(params IInterceptor[] interceptors)
        => Utility.NewGrifballContext(SetUpFixture.DatabaseContainer, interceptors);

    public static DatabaseProvider Provider { get; } = ProviderFrom(Environment.GetEnvironmentVariable("GRIF_TEST_DATABASE"));

    internal static DatabaseProvider ProviderFrom(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DatabaseProvider.SqlServer
            : Enum.Parse<DatabaseProvider>(value, ignoreCase: true);
    }

    /// <summary>The other server, for what runs on both at once (the conversion between them): Postgres for SQLite.</summary>
    public static DatabaseProvider Other => Provider == DatabaseProvider.Postgres ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres;

    /// <summary>Starts <see cref="Other"/>, which is always a server (SQL Server or Postgres).</summary>
    public static async Task<IDatabaseContainer> StartOtherServer()
        => await StartServer(Other) ?? throw new InvalidOperationException($"{Other} has no server");

    public static async Task<IDatabaseContainer?> StartServer(DatabaseProvider? provider = null)
    {
        if ((provider ?? Provider) == DatabaseProvider.Sqlite)
        {
            Directory.CreateDirectory(SqliteFolder);
            return null;
        }
        IDatabaseContainer server = (provider ?? Provider) switch
        {
            DatabaseProvider.Postgres => new PostgreSqlBuilder("postgres:18-alpine")
                // Every test's database keeps a connection pool until it's dropped.
                .WithCommand("-c", "max_connections=500")
                .Build(),
            _ => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("yourStrong(!)Password")
                .Build(),
        };
        await server.StartAsync();
        return server;
    }

    /// <summary>The server's connection string on <paramref name="database"/>.</summary>
    public static string ConnectionString(IDatabaseContainer? server, string database, DatabaseProvider? provider = null)
    {
        return (provider ?? Provider) switch
        {
            DatabaseProvider.Sqlite => new SqliteConnectionStringBuilder { DataSource = Path.Combine(SqliteFolder, $"{database}.db") }.ConnectionString,
            DatabaseProvider.Postgres when server is null => throw new ArgumentNullException(nameof(server)),
            DatabaseProvider.Postgres => new NpgsqlConnectionStringBuilder(server!.GetConnectionString()) { Database = database }.ConnectionString,
            _ => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server!.GetConnectionString()) { InitialCatalog = database }.ConnectionString,
        };
    }

    /// <summary>The server's own database, which always exists.</summary>
    public static string MaintenanceConnectionString(IDatabaseContainer? server)
    {
        return ConnectionString(server, Provider == DatabaseProvider.Postgres ? "postgres" : "master");
    }

    /// <summary>A connection string nothing listens on: refused at once.</summary>
    public static string UnreachableConnectionString()
    {
        return Provider switch
        {
            // A file in a folder that isn't there, opened without creating it.
            DatabaseProvider.Sqlite => "Data Source=/nonexistent-grif-folder/nope.db;Mode=ReadWrite",
            DatabaseProvider.Postgres => "Host=127.0.0.1;Port=9;Database=nope;Username=x;Password=x;Timeout=2",
            _ => "Server=127.0.0.1,9;Database=nope;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True",
        };
    }

    public static DbContextOptionsBuilder<GrifballContext> Options(string connectionString)
    {
        return new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(Provider, connectionString);
    }

    public static async Task CreateDatabase(IDatabaseContainer? server, string name)
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            // Opening it makes the file.
            var connectionString = ConnectionString(server, name);
            await using (var file = new SqliteConnection(connectionString))
                await file.OpenAsync();
            SqliteConnection.ClearPool(new SqliteConnection(connectionString));
            return;
        }
        await using var connection = Provider == DatabaseProvider.Postgres
            ? (System.Data.Common.DbConnection)new NpgsqlConnection(MaintenanceConnectionString(server))
            : new Microsoft.Data.SqlClient.SqlConnection(MaintenanceConnectionString(server));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Provider == DatabaseProvider.Postgres ? $"CREATE DATABASE \"{name}\"" : $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>SQLite: the run's folder of database files, under the temp folder.</summary>
    public static readonly string SqliteFolder = Path.Combine(Path.GetTempPath(), $"grif-tests-{Environment.ProcessId}");

    /// <summary>The database every test's is copied from, migrated once (<see cref="CreateTemplate"/>).</summary>
    private const string TemplateName = "GrifTemplate";

    // SQL Server: the template's backup, and its files' logical names and the folder new ones go in.
    private static string? _templateBackup;
    private static (string Data, string Log, string Folder)? _templateFiles;

    /// <summary>
    /// Makes the template once per run (SetUpFixture): a database with every migration. Migrating each
    /// test's database took most of the suite's time (1.5s of SQL Server's, 0.9s of Postgres's, at the
    /// least, for each of some 550 tests); a copy is made in a fraction of that. Postgres copies it
    /// itself (CREATE DATABASE ... TEMPLATE, its default WAL_LOG strategy: 69ms a copy, FILE_COPY's
    /// 101ms), with no connection to it left open; SQL Server, which has no templates, restores a
    /// backup of it, its files shrunk first (each restore makes files the size the backup's were:
    /// 520ms against 590ms). SQL Server makes its databases one at a time, even an empty CREATE
    /// DATABASE taking 620ms: that, not the copy, is the floor of its suite (2026-10-15).
    /// </summary>
    public static async Task CreateTemplate(IDatabaseContainer? server)
    {
        await CreateDatabase(server, TemplateName);
        var template = ConnectionString(server, TemplateName);
        await using (var context = new GrifballContext(Options(template).Options))
        {
            await context.Database.MigrateAsync();
            // SQLite: everything in the file itself, the WAL emptied into it, so a copy of the file is the database.
            if (Provider == DatabaseProvider.Sqlite)
                await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
            if (Provider == DatabaseProvider.SqlServer)
                await context.Database.ExecuteSqlRawAsync($"""
                    ALTER DATABASE [{TemplateName}] SET RECOVERY SIMPLE;
                    CHECKPOINT;
                    DECLARE @log sysname = (SELECT name FROM sys.database_files WHERE type = 1);
                    DECLARE @data sysname = (SELECT name FROM sys.database_files WHERE type = 0);
                    DBCC SHRINKFILE (@log, 1) WITH NO_INFOMSGS;
                    DBCC SHRINKFILE (@data, 1) WITH NO_INFOMSGS;
                    """);
        }

        if (Provider == DatabaseProvider.Sqlite)
        {
            SqliteConnection.ClearPool(new SqliteConnection(template));
            return;
        }

        if (Provider == DatabaseProvider.Postgres)
        {
            NpgsqlConnection.ClearPool(new NpgsqlConnection(template));
            await ExecuteAsync(server, $"ALTER DATABASE \"{TemplateName}\" WITH ALLOW_CONNECTIONS false");
            return;
        }

        Microsoft.Data.SqlClient.SqlConnection.ClearPool(new Microsoft.Data.SqlClient.SqlConnection(template));
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(MaintenanceConnectionString(server));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'))";
        var folder = ((string)(await command.ExecuteScalarAsync())!).TrimEnd('/');
        _templateBackup = $"{folder}/{TemplateName}.bak";
        command.CommandText = $"BACKUP DATABASE [{TemplateName}] TO DISK = N'{_templateBackup}' WITH INIT, COPY_ONLY";
        await command.ExecuteNonQueryAsync();
        command.CommandText = $"RESTORE FILELISTONLY FROM DISK = N'{_templateBackup}'";
        string? data = null, log = null;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (reader.GetString(reader.GetOrdinal("Type")) == "L")
                    log = reader.GetString(reader.GetOrdinal("LogicalName"));
                else
                    data = reader.GetString(reader.GetOrdinal("LogicalName"));
            }
        }
        _templateFiles = (data!, log!, folder);
    }

    /// <summary>A database with every migration: a copy of the template.</summary>
    public static async Task CreateMigratedDatabase(IDatabaseContainer? server, string name)
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            File.Copy(Path.Combine(SqliteFolder, $"{TemplateName}.db"), Path.Combine(SqliteFolder, $"{name}.db"));
            return;
        }

        if (Provider == DatabaseProvider.Postgres)
        {
            await ExecuteAsync(server, $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateName}\"");
            return;
        }

        var (data, log, folder) = _templateFiles ?? throw new InvalidOperationException("No template: SetUpFixture makes it.");
        await ExecuteAsync(server, $"""
            RESTORE DATABASE [{name}] FROM DISK = N'{_templateBackup}'
            WITH MOVE N'{data}' TO N'{folder}/{name}.mdf', MOVE N'{log}' TO N'{folder}/{name}_log.ldf'
            """);
    }

    private static async Task ExecuteAsync(IDatabaseContainer? server, string sql)
    {
        await using var connection = Provider == DatabaseProvider.Postgres
            ? (System.Data.Common.DbConnection)new NpgsqlConnection(MaintenanceConnectionString(server))
            : new Microsoft.Data.SqlClient.SqlConnection(MaintenanceConnectionString(server));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>SQL Server's "Cannot drop database because it is currently in use".</summary>
    private const int DatabaseInUse = 3702;

    /// <summary>SQL Server's "cannot be made at this time": the database is in single-user mode and someone else holds it.</summary>
    private const int SingleUserTaken = 5064;

    /// <summary>
    /// Drops the database behind <paramref name="connectionString"/>, closing its connections; with
    /// <paramref name="ifExists"/>, nothing when there is none.
    /// </summary>
    public static async Task DropDatabase(string connectionString, bool ifExists = false)
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            SqliteConnection.ClearPool(new SqliteConnection(connectionString));
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (!ifExists && !File.Exists(path))
                throw new FileNotFoundException("No such SQLite database", path);
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                File.Delete(file);
        }
        else if (Provider == DatabaseProvider.Postgres)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var name = builder.Database;
            NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
            builder.Database = "postgres";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE {(ifExists ? "IF EXISTS " : "")}\"{name}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            var name = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            // A pooled connection of a host just stopped can still take the one SINGLE_USER slot between the ALTER and the
            // DROP ("currently in use", seen in CI): its pool is cleared first, and a drop that still loses the race is retried.
            // A connection holding the slot also blocks the retry's ALTER (5064), so every session left on it is killed first.
            Microsoft.Data.SqlClient.SqlConnection.ClearPool(new Microsoft.Data.SqlClient.SqlConnection(connectionString));
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = $@"
                    {(ifExists ? $"IF DB_ID('{name}') IS NULL RETURN;" : "")}
                    DECLARE @kill nvarchar(max) = N'';
                    SELECT @kill += N'BEGIN TRY KILL ' + CAST(session_id AS nvarchar(10)) + N'; END TRY BEGIN CATCH END CATCH;'
                    FROM sys.dm_exec_sessions WHERE database_id = DB_ID('{name}') AND session_id <> @@SPID;
                    EXEC (@kill);
                    ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{name}];
                    ";
                    await command.ExecuteNonQueryAsync();
                    return;
                }
                catch (Microsoft.Data.SqlClient.SqlException e) when (attempt < 5
                    && e.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(x => x.Number is DatabaseInUse or SingleUserTaken))
                {
                    Microsoft.Data.SqlClient.SqlConnection.ClearPool(new Microsoft.Data.SqlClient.SqlConnection(connectionString));
                    await Task.Delay(200 * attempt);
                }
            }
        }
    }
}
