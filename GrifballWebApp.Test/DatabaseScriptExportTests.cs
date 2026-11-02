using System.Text.RegularExpressions;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using GrifballWebApp.Database.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace GrifballWebApp.Test;

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

    private static DatabaseScriptExporter Exporter() => new(NullLogger<DatabaseConverter>.Instance);

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
        var season = new Season { SeasonName = "After the restore" };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();
        Assert.That(season.SeasonID, Is.GreaterThan(filled.SeasonID));
        var before = await db.Seasons.TemporalAll().CountAsync(s => s.SeasonID == filled.SeasonID);
        var first = await db.Seasons.SingleAsync(s => s.SeasonID == filled.SeasonID);
        first.SeasonName += " (renamed)";
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
            db.Seasons.Add(new Season { SeasonName = "Already here" });
            await db.SaveChangesAsync();
        }

        var e = await Assert.CatchAsync(() => RunAsync(existing, script));

        Assert.That(e!.Message, Does.Contain("already has tables"));
        await using var check = DatabaseConverter.NewContext(existing);
        Assert.That(await check.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(new[] { "Already here" }));
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
    public async Task Is_for_the_servers_only()
    {
        Assert.That(DatabaseScriptExporter.Supports(DatabaseProvider.SqlServer), Is.True);
        Assert.That(DatabaseScriptExporter.Supports(DatabaseProvider.Postgres), Is.True);
        Assert.That(DatabaseScriptExporter.Supports(DatabaseProvider.Sqlite), Is.False);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Exporter().WriteAsync(
            new DatabaseEndpoint(DatabaseProvider.Sqlite, "Data Source=unused"), DatabaseProvider.Sqlite, TextWriter.Null));
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
    public void SqlServer_keeps_every_literal_on_one_line()
    {
        Assert.That(SqlServerScript.Unicode("it's"), Is.EqualTo("N'it''s'"));
        Assert.That(SqlServerScript.Unicode("a\r\nGO\nb"),
            Is.EqualTo("CONVERT(nvarchar(max), N'a' + NCHAR(13) + N'' + NCHAR(10) + N'GO' + NCHAR(10) + N'b')"));
    }

    [Test]
    public void Literals_as_each_server_reads_them()
    {
        var sqlServer = new SqlServerScript();
        var postgres = new PostgresScript();
        ColumnInfo Column(string type) => new("C", type, Generated: false, Identity: false);
        var instant = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567);
        Assert.Multiple(() =>
        {
            Assert.That(sqlServer.Literal(instant, Column("datetime2")), Is.EqualTo("'2026-10-10T12:00:00.1234567'"));
            Assert.That(sqlServer.Literal(instant, Column("datetime")), Is.EqualTo("'2026-10-10T12:00:00.123'"));
            Assert.That(sqlServer.Literal(instant, Column("datetimeoffset")), Is.EqualTo("'2026-10-10T12:00:00.1234567+00:00'"));
            Assert.That(sqlServer.Literal(DateTime.MaxValue, Column("datetime2")), Is.EqualTo("'9999-12-31T23:59:59.9999999'"));
            Assert.That(sqlServer.Literal(true, Column("bit")), Is.EqualTo("1"));
            Assert.That(sqlServer.Literal(new byte[] { 0xDE, 0xAD }, Column("varbinary(max)")), Is.EqualTo("0xDEAD"));
            Assert.That(sqlServer.Literal(TimeSpan.FromHours(20.5), Column("time")), Is.EqualTo("'20:30:00.0000000'"));
            Assert.That(sqlServer.Literal(new TimeOnly(9, 15), Column("time")), Is.EqualTo("'09:15:00.0000000'"));
            Assert.That(sqlServer.Literal(new DateOnly(2026, 1, 2), Column("date")), Is.EqualTo("'2026-01-02'"));
            Assert.That(sqlServer.Literal(1.5f, Column("real")), Is.EqualTo("1.5"));
            Assert.That(sqlServer.Literal(0.25, Column("float")), Is.EqualTo("0.25"));
            Assert.That(sqlServer.Literal(2535405290000001L, Column("bigint")), Is.EqualTo("2535405290000001"));
            Assert.That(sqlServer.Literal(12.345m, Column("decimal(6, 3)")), Is.EqualTo("12.345"));
            Assert.That(sqlServer.Literal('x', Column("nchar(1)")), Is.EqualTo("N'x'"));
            Assert.That(sqlServer.Literal(null, Column("int")), Is.EqualTo("NULL"));

            Assert.That(postgres.Literal(instant, Column("timestamp with time zone")), Is.EqualTo("'2026-10-10 12:00:00.123456+00'"));
            Assert.That(postgres.Literal(DateTime.MaxValue, Column("timestamp with time zone")), Is.EqualTo("'infinity'"));
            Assert.That(postgres.Literal(DateTime.MinValue, Column("timestamp with time zone")), Is.EqualTo("'-infinity'"));
            Assert.That(postgres.Literal(42, Column("integer")), Is.EqualTo("42"));
            Assert.That(postgres.Literal(12.345m, Column("numeric")), Is.EqualTo("12.345"));
            Assert.That(postgres.Literal(false, Column("boolean")), Is.EqualTo("FALSE"));
            Assert.That(postgres.Literal("it's \\ not", Column("text")), Is.EqualTo("'it''s \\ not'"));
            Assert.That(postgres.Literal(new byte[] { 0xBE, 0xEF }, Column("bytea")), Is.EqualTo("'\\xBEEF'"));
            Assert.That(postgres.Literal(new TimeSpan(1, 2, 3, 4, 500), Column("interval")), Is.EqualTo("'P1DT2H3M4.5S'"));
            Assert.That(postgres.Literal(-TimeSpan.FromMinutes(90), Column("interval")), Is.EqualTo("'-P0DT1H30M0S'"));
            Assert.That(postgres.Literal(TimeSpan.FromHours(20.5), Column("time without time zone")), Is.EqualTo("'20:30:00.000000'"));
            Assert.That(postgres.Literal(new TimeOnly(9, 15), Column("time without time zone")), Is.EqualTo("'09:15:00.000000'"));
            Assert.That(postgres.Literal(new DateOnly(2026, 1, 2), Column("date")), Is.EqualTo("'2026-01-02'"));
            Assert.That(postgres.Literal(double.NaN, Column("double precision")), Is.EqualTo("'NaN'"));
            Assert.That(postgres.Literal(double.NegativeInfinity, Column("double precision")), Is.EqualTo("'-Infinity'"));
            Assert.That(postgres.Literal(double.PositiveInfinity, Column("double precision")), Is.EqualTo("'Infinity'"));
            Assert.That(postgres.Literal(float.NegativeInfinity, Column("real")), Is.EqualTo("'-Infinity'"));
            Assert.That(postgres.Literal(float.PositiveInfinity, Column("real")), Is.EqualTo("'Infinity'"));
            Assert.That(postgres.Literal(float.NaN, Column("real")), Is.EqualTo("'NaN'"));
            Assert.That(postgres.Literal(2.25, Column("double precision")), Is.EqualTo("2.25"));
            Assert.That(postgres.Literal(Guid.Empty, Column("uuid")), Is.EqualTo("'00000000-0000-0000-0000-000000000000'"));
            Assert.That(postgres.Literal('x', Column("character(1)")), Is.EqualTo("'x'"));
        });
    }

    [Test]
    public async Task SqlServer_target_lists_each_temporal_tables_history()
    {
        await using var context = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.SqlServer, "Server=localhost;Database=Unused"));
        var tables = DatabaseScriptExporter.TargetColumns(context, DatabaseProvider.SqlServer);
        var seasons = tables.Single(t => t.Key.Name == "Seasons");
        var history = tables.Single(t => t.Key.Name == context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Season))!.GetHistoryTableName());

        Assert.That(history.Value.Select(c => c.Name), Is.EqualTo(seasons.Value.Select(c => c.Name)));
        Assert.That(history.Value.Any(c => c.Generated), Is.False);
    }
}
