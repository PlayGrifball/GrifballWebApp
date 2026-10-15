using DotNet.Testcontainers.Containers;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace GrifballWebApp.Test;

/// <summary>
/// The database server the tests run against: GRIF_TEST_DATABASE=Postgres, else SQL Server. CI runs
/// the suite on both. Each test gets its own database on the one server.
/// </summary>
internal static class TestDatabase
{
    public static DatabaseProvider Provider { get; } = ProviderFrom(Environment.GetEnvironmentVariable("GRIF_TEST_DATABASE"));

    internal static DatabaseProvider ProviderFrom(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DatabaseProvider.SqlServer
            : Enum.Parse<DatabaseProvider>(value, ignoreCase: true);
    }

    /// <summary>The other server, for what runs on both at once (the conversion between them).</summary>
    public static DatabaseProvider Other => Provider == DatabaseProvider.Postgres ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres;

    public static async Task<IDatabaseContainer> StartServer(DatabaseProvider? provider = null)
    {
        IDatabaseContainer server = (provider ?? Provider) switch
        {
            DatabaseProvider.Postgres => new PostgreSqlBuilder()
                .WithImage("postgres:18-alpine")
                // Every test's database keeps a connection pool until it's dropped.
                .WithCommand("-c", "max_connections=500")
                .Build(),
            _ => new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("yourStrong(!)Password")
                .Build(),
        };
        await server.StartAsync();
        return server;
    }

    /// <summary>The server's connection string on <paramref name="database"/>.</summary>
    public static string ConnectionString(IDatabaseContainer server, string database, DatabaseProvider? provider = null)
    {
        return (provider ?? Provider) switch
        {
            DatabaseProvider.Postgres => new NpgsqlConnectionStringBuilder(server.GetConnectionString()) { Database = database }.ConnectionString,
            _ => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server.GetConnectionString()) { InitialCatalog = database }.ConnectionString,
        };
    }

    /// <summary>The server's own database, which always exists.</summary>
    public static string MaintenanceConnectionString(IDatabaseContainer server)
    {
        return ConnectionString(server, Provider == DatabaseProvider.Postgres ? "postgres" : "master");
    }

    /// <summary>A connection string nothing listens on: refused at once.</summary>
    public static string UnreachableConnectionString()
    {
        return Provider switch
        {
            DatabaseProvider.Postgres => "Host=127.0.0.1;Port=9;Database=nope;Username=x;Password=x;Timeout=2",
            _ => "Server=127.0.0.1,9;Database=nope;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True",
        };
    }

    public static DbContextOptionsBuilder<GrifballContext> Options(string connectionString)
    {
        return new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(Provider, connectionString);
    }

    public static async Task CreateDatabase(IDatabaseContainer server, string name)
    {
        await using var connection = Provider == DatabaseProvider.Postgres
            ? (System.Data.Common.DbConnection)new NpgsqlConnection(MaintenanceConnectionString(server))
            : new Microsoft.Data.SqlClient.SqlConnection(MaintenanceConnectionString(server));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Provider == DatabaseProvider.Postgres ? $"CREATE DATABASE \"{name}\"" : $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

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
    public static async Task CreateTemplate(IDatabaseContainer server)
    {
        await CreateDatabase(server, TemplateName);
        var template = ConnectionString(server, TemplateName);
        await using (var context = new GrifballContext(Options(template).Options))
        {
            await context.Database.MigrateAsync();
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
    public static async Task CreateMigratedDatabase(IDatabaseContainer server, string name)
    {
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

    private static async Task ExecuteAsync(IDatabaseContainer server, string sql)
    {
        await using var connection = Provider == DatabaseProvider.Postgres
            ? (System.Data.Common.DbConnection)new NpgsqlConnection(MaintenanceConnectionString(server))
            : new Microsoft.Data.SqlClient.SqlConnection(MaintenanceConnectionString(server));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Drops the database behind <paramref name="connectionString"/>, closing its connections.</summary>
    public static async Task DropDatabase(string connectionString)
    {
        if (Provider == DatabaseProvider.Postgres)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var name = builder.Database;
            NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
            builder.Database = "postgres";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE \"{name}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            var name = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
            ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{name}];
            ";
            await command.ExecuteNonQueryAsync();
        }
    }
}
