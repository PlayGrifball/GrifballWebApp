using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
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

    public static async Task<IDatabaseContainer> StartServer()
    {
        IDatabaseContainer server = Provider switch
        {
            DatabaseProvider.Postgres => new PostgreSqlBuilder()
                .WithImage(await BuildPostgresImage())
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

    /// <summary>
    /// PostgreSQL with the periods extension, as the Helm chart runs it for row history (RowHistoryTests):
    /// built from the repository's docker/postgres-periods, once per run - about 15 seconds, the
    /// extension compiled from source. A database has history only once it has the extension, so every
    /// other test runs without, as a default deploy does.
    /// </summary>
    private static async Task<IImage> BuildPostgresImage()
    {
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "docker/postgres-periods")
            .Build();
        await image.CreateAsync();
        return image;
    }

    /// <summary>The server's connection string on <paramref name="database"/>.</summary>
    public static string ConnectionString(IDatabaseContainer server, string database)
    {
        return Provider switch
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
