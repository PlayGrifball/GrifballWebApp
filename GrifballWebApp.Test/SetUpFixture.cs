using DotNet.Testcontainers.Containers;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GrifballWebApp.Test;

[SetUpFixture]
public class SetUpFixture
{
    /// <summary>SQL Server or Postgres (<see cref="TestDatabase.Provider"/>).</summary>
    public static IDatabaseContainer DatabaseContainer { get; private set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        DatabaseContainer = await TestDatabase.StartServer();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await DatabaseContainer.DisposeAsync();
    }

    public static async Task<GrifballContext> NewGrifballContext(params IInterceptor[] interceptors)
    {
        return await Utility.NewGrifballContext(SetUpFixture.DatabaseContainer, interceptors);
    }

    private static readonly SemaphoreSlim HistoryTemplateLock = new(1, 1);
    private static string? _historyTemplate;

    /// <summary>
    /// A new database that keeps row history. SQL Server's tables are temporal: any. Postgres's need the
    /// periods extension (readme.md, Row history), which a database has only when it's put there, as the
    /// Helm chart's database.history does: a copy of one database migrated with it, made once (versioning
    /// every table takes seconds).
    /// </summary>
    public static async Task<GrifballContext> NewGrifballContextWithHistory()
    {
        if (TestDatabase.Provider != DatabaseProvider.Postgres)
            return await NewGrifballContext();

        var name = $"TestDb_{Guid.NewGuid():N}";
        await TestDatabase.CreateDatabase(DatabaseContainer, name, template: await HistoryTemplate());
        return new GrifballContext(TestDatabase.Options(TestDatabase.ConnectionString(DatabaseContainer, name)).Options);
    }

    private static async Task<string> HistoryTemplate()
    {
        await HistoryTemplateLock.WaitAsync();
        try
        {
            if (_historyTemplate is null)
            {
                var name = $"TestDbHistory_{Guid.NewGuid():N}";
                await TestDatabase.CreateDatabase(DatabaseContainer, name);
                var connectionString = TestDatabase.ConnectionString(DatabaseContainer, name);
                await using (var context = new GrifballContext(TestDatabase.Options(connectionString).Options))
                {
                    await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION periods CASCADE");
                    await context.Database.MigrateAsync();
                }
                // CREATE DATABASE ... TEMPLATE needs the template's connections closed.
                NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
                _historyTemplate = name;
            }
            return _historyTemplate;
        }
        finally
        {
            HistoryTemplateLock.Release();
        }
    }
}
