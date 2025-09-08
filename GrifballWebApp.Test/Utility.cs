using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;

namespace GrifballWebApp.Test;
internal static class Utility
{
    internal static async Task<MsSqlContainer> NewSqlServer()
    {
        var server = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("yourStrong(!)Password")
            .Build();
        await server.StartAsync();
        return server;
    }
    internal static async Task<GrifballContext> NewGrifballContext(MsSqlContainer server, params IInterceptor[] interceptors)
    {
        // Create a unique database name per test
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        var masterConnectionString = server.GetConnectionString();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = "master"
        };
        using (var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{dbName}]";
            await command.ExecuteNonQueryAsync();
        }
        // Use the new database in the connection string
        var testDbConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = dbName
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseSqlServer(testDbConnectionString)
            .AddInterceptors(interceptors)
            .Options;

        var context = new GrifballContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    internal static async Task DropDatabaseAndDispose(this GrifballContext context)
    {
        await context.DropDatabase();
        await context.DisposeAsync();
    }

    internal static async Task DropDatabase(this GrifballContext context)
    {
        var cs = context.Database.GetConnectionString();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs);
        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
        ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [{dbName}];
        ";
        await command.ExecuteNonQueryAsync();
    }
}
