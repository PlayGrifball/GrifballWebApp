using System.Text.RegularExpressions;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Domain.League;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using static GrifballWebApp.Infrastructure.Test.DatabaseScriptExportTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// DatabaseScriptExporter: the suite's database written as each server's script, run into a new database on that
/// server as psql and sqlcmd -b would, and checked table by table against the source, as the converter checks its copy.
/// SQL Server and PostgreSQL both, where the suite has them (the SQLite suite starts PostgreSQL only).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseScriptExportTests
{
    private static IDatabaseContainer _other = null!;
    private readonly List<DatabaseEndpoint> _databases = [];
    private static readonly string[] expected = new[] { "Already here" };

    [OneTimeSetUp]
    public static async Task StartOtherServer() => _other = await TestDatabase.StartServer(TestDatabase.Other);

    [OneTimeTearDown]
    public static async Task StopOtherServer() => await _other.DisposeAsync();

    [TearDown]
    public async Task DropDatabases()
    {
        foreach (var database in _databases)
        {
            if (database.Provider == DatabaseProvider.Sqlite)
                Microsoft.Data.Sqlite.SqliteConnection.ClearPool(new Microsoft.Data.Sqlite.SqliteConnection(database.ConnectionString));
            else if (database.Provider == DatabaseProvider.Postgres)
                NpgsqlConnection.ClearPool(new NpgsqlConnection(database.ConnectionString));
            else
                SqlConnection.ClearPool(new SqlConnection(database.ConnectionString));
            await using var context = DatabaseConverter.NewContext(database);
            await context.Database.EnsureDeletedAsync();
        }
        _databases.Clear();
    }

    private DatabaseEndpoint Database(DatabaseProvider provider)
    {
        IDatabaseContainer? server;
        if (provider == TestDatabase.Provider)
            server = SetUpFixture.DatabaseContainer;
        else if (provider == TestDatabase.Other)
            server = _other;
        else
        {
            Assert.Ignore($"This suite has no {provider} server.");
            return null!;
        }
        var endpoint = new DatabaseEndpoint(provider, TestDatabase.ConnectionString(server, $"Script_{Guid.NewGuid():N}", provider));
        _databases.Add(endpoint);
        return endpoint;
    }

    private static async Task<string> ScriptAsync(DatabaseEndpoint source, DatabaseProvider target)
    {
        await using var script = new StringWriter();
        await Exporter().WriteAsync(source, target, script);
        return script.ToString();
    }

    /// <summary>A new, empty database for the script.</summary>
    private DatabaseEndpoint EmptyDatabase(DatabaseProvider provider)
    {
        var database = Database(provider);
        using var context = DatabaseConverter.NewContext(database);
        context.GetService<IRelationalDatabaseCreator>().Create();
        return database;
    }

    /// <summary>The script run as psql -v ON_ERROR_STOP=1 or sqlcmd -b runs it: stopping at the first error.</summary>
    internal static async Task RunAsync(DatabaseEndpoint database, string script)
    {
        if (database.Provider == DatabaseProvider.Postgres)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(script, connection) { CommandTimeout = 0 };
            await command.ExecuteNonQueryAsync();
            return;
        }
        await using var sql = new SqlConnection(database.ConnectionString);
        await sql.OpenAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;
            await using var command = new SqlCommand(batch, sql) { CommandTimeout = 0 };
            await command.ExecuteNonQueryAsync();
        }
    }

    [TestCase(DatabaseProvider.Postgres)]
    [TestCase(DatabaseProvider.SqlServer)]
    public async Task Restores_EveryRowAndItsHistory_OnEitherServer(DatabaseProvider target)
    {
        var source = Database(TestDatabase.Provider);
        var filled = await DatabaseConversionTests.Fill(source);
        var restored = EmptyDatabase(target);

        await RunAsync(restored, await ScriptAsync(source, target));

        Assert.That(await new DatabaseConverter(NullLogger<DatabaseConverter>.Instance).CompareAsync(source, restored), Is.Empty,
            "every table's rows, to the microsecond");
        await using var db = DatabaseConverter.NewContext(restored);
        Assert.That(await db.Database.GetAppliedMigrationsAsync(), Is.EqualTo(db.Database.GetMigrations()));

        // Identity carries on after the highest key, and row history is on again: a change keeps the old row.
        var season = Season.Seed(seasonName: "After the restore");
        db.Seasons.Add(season);
        await db.SaveChangesAsync();
        Assert.That(season.SeasonID, Is.GreaterThan(filled.SeasonID));
        var before = await db.Seasons.TemporalAll().CountAsync(s => s.SeasonID == filled.SeasonID);
        var first = await db.Seasons.SingleAsync(s => s.SeasonID == filled.SeasonID);
        first.Edited(seasonName: first.SeasonName + " (renamed)");
        await db.SaveChangesAsync();
        Assert.That(await db.Seasons.TemporalAll().CountAsync(s => s.SeasonID == filled.SeasonID), Is.EqualTo(before + 1));
    }

    [TestCase(DatabaseProvider.Postgres)]
    [TestCase(DatabaseProvider.SqlServer)]
    public async Task Refuses_ADatabaseThatHasTables_AndLeavesIt(DatabaseProvider target)
    {
        var source = Database(TestDatabase.Provider);
        await DatabaseConversionTests.Fill(source);
        var script = await ScriptAsync(source, target);
        var existing = Database(target);
        await using (var db = DatabaseConverter.NewContext(existing))
        {
            await db.Database.MigrateAsync();
            db.Seasons.Add(Season.Seed(seasonName: "Already here"));
            await db.SaveChangesAsync();
        }

        var e = await Assert.CatchAsync(() => RunAsync(existing, script));

        Assert.That(e!.Message, Does.Contain("already has tables"));
        await using var check = DatabaseConverter.NewContext(existing);
        Assert.That(await check.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(expected));
    }

    [Test]
    public async Task A_failure_part_way_leaves_nothing_on_SqlServer_even_without_stopping()
    {
        var source = Database(TestDatabase.Provider);
        await DatabaseConversionTests.Fill(source);
        var script = await ScriptAsync(source, DatabaseProvider.SqlServer);
        var restored = EmptyDatabase(DatabaseProvider.SqlServer);
        // A batch that fails after the migrations, run as sqlcmd without -b: the batches after it go on.
        var broken = script.Replace("-- Temporal tables: system versioning off", "SELECT 1/0;\nGO\n-- Temporal tables: system versioning off");

        await using (var sql = new SqlConnection(restored.ConnectionString))
        {
            await sql.OpenAsync();
            foreach (var batch in Regex.Split(broken, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                try
                {
                    await using var command = new SqlCommand(batch, sql);
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqlException)
                {
                }
            }
        }

        await using var connection = new SqlConnection(restored.ConnectionString);
        await connection.OpenAsync();
        await using var tables = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0", connection);
        Assert.That(await tables.ExecuteScalarAsync(), Is.EqualTo(0), "rolled back, and nothing after it ran");
    }

    [Test]
    public async Task Compare_names_the_tables_that_differ()
    {
        var source = Database(TestDatabase.Provider);
        await DatabaseConversionTests.Fill(source);
        var empty = Database(TestDatabase.Provider);
        await using (var db = DatabaseConverter.NewContext(empty))
            await db.Database.MigrateAsync();

        var mismatches = await new DatabaseConverter(NullLogger<DatabaseConverter>.Instance).CompareAsync(source, empty);

        Assert.That(mismatches, Has.Some.Match(@"Seasons \(\d+ rows in the source, 0 in the target\)"));
    }

    [Test]
    public async Task SqlServer_target_lists_each_temporal_tables_history()
    {
        await using var context = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.SqlServer, "Server=localhost;Database=Unused"));
        var tables = DatabaseScriptExporter.TargetColumns(context, DatabaseProvider.SqlServer);
        var seasons = tables.Single(t => t.Key.Name == "Seasons");
        var history = tables.Single(t => t.Key.Name == context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Season))!.GetHistoryTableName());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(history.Value.Select(c => c.Name), Is.EqualTo(seasons.Value.Select(c => c.Name)));
            Assert.That(history.Value.Any(c => c.Generated), Is.False);
        }
    }
}
