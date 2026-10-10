using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GrifballWebApp.Test;

/// <summary>
/// Row history on Postgres (PostgresHistory and the AddRowHistory migration's triggers), on a migrated
/// database. SQL Server keeps its history with temporal tables, which need no test of their own.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class PostgresHistoryTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup()
    {
        if (TestDatabase.Provider != DatabaseProvider.Postgres)
            Assert.Ignore("Postgres's row history: SQL Server's is its temporal tables.");
        _context = await SetUpFixture.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0);

    private static Season NewSeason(string name) => new()
    {
        SeasonName = name,
        PublicAt = Now,
        SignupsOpen = Now,
        SignupsClose = Now.AddDays(7),
        DraftStart = Now.AddDays(8),
        SeasonStart = Now.AddDays(14),
        SeasonEnd = Now.AddDays(60),
    };

    private DateTime PeriodStart(object entity) => (DateTime)_context.Entry(entity).Property(PostgresHistory.PeriodStart).CurrentValue!;

    /// <summary>The history table's rows, read through EF as the property bags they are.</summary>
    private Task<List<Dictionary<string, object>>> History(string schema, string table, string key, int id)
    {
        return _context.Set<Dictionary<string, object>>($"{schema}.{table}{PostgresHistory.Suffix}")
            .Where(r => EF.Property<int?>(r, key) == id)
            .OrderBy(r => EF.Property<DateTime>(r, PostgresHistory.PeriodEnd))
            .ToListAsync();
    }

    private async Task<T> Scalar<T>(string sql, params object[] parameters)
    {
        return (await _context.Database.SqlQueryRaw<T>(sql, parameters).ToListAsync()).Single();
    }

    [Test]
    public async Task EveryHistoryTable_HasItsTablesColumnsAndTypes_AndTheTriggers()
    {
        var histories = _context.Model.GetEntityTypes()
            .Where(e => e.FindAnnotation(PostgresHistory.HistoryOfAnnotation) is not null)
            .Select(e => (Schema: e.GetSchema()!, Table: (string)e.FindAnnotation(PostgresHistory.HistoryOfAnnotation)!.Value!, History: e.GetTableName()!))
            .ToList();
        Assert.That(histories, Has.Count.GreaterThan(30));

        foreach (var (schema, table, history) in histories)
        {
            var main = await Columns(schema, table);
            var copy = await Columns(schema, history);
            var triggers = await _context.Database.SqlQueryRaw<string>(
                "SELECT tgname AS \"Value\" FROM pg_trigger WHERE NOT tgisinternal AND tgrelid IN (format('%I.%I', {0}, {1})::regclass, format('%I.%I', {0}, {2})::regclass)",
                schema, table, history).ToListAsync();

            Assert.Multiple(() =>
            {
                // Same names, types and collations; the history table has PeriodEnd besides.
                Assert.That(copy.Where(c => c.Name != PostgresHistory.PeriodEnd).Select(c => (c.Name, c.Type, c.Collation)),
                    Is.EquivalentTo(main.Select(c => (c.Name, c.Type, c.Collation))), $"{schema}.{history}");
                Assert.That(copy.SingleOrDefault(c => c.Name == PostgresHistory.PeriodEnd)?.Type, Is.EqualTo("timestamp without time zone"), $"{schema}.{history}");
                Assert.That(main.Single(c => c.Name == PostgresHistory.PeriodStart).Type, Is.EqualTo("timestamp without time zone"), $"{schema}.{table}");
                // No identity, default or NOT NULL but the periods'.
                Assert.That(copy.Where(c => c.Identity || c.Default), Is.Empty, $"{schema}.{history}");
                Assert.That(copy.Where(c => c.NotNull).Select(c => c.Name), Is.EquivalentTo(new[] { PostgresHistory.PeriodStart, PostgresHistory.PeriodEnd }), $"{schema}.{history}");
                Assert.That(triggers, Is.EquivalentTo(new[] { "period_start", "versioning_update", "versioning_delete", "versioning_truncate", "read_only" }), $"{schema}.{table}");
            });
        }
    }

    private record Column(string Name, string Type, string Collation, bool NotNull, bool Identity, bool Default);

    private Task<List<Column>> Columns(string schema, string table)
    {
        return _context.Database.SqlQueryRaw<Column>("""
            SELECT a.attname AS "Name", format_type(a.atttypid, a.atttypmod) AS "Type", coalesce(co.collname, '') AS "Collation",
                   a.attnotnull AS "NotNull", a.attidentity <> '' AS "Identity", a.atthasdef AS "Default"
              FROM pg_attribute a LEFT JOIN pg_collation co ON co.oid = a.attcollation
             WHERE a.attrelid = format('%I.%I', {0}, {1})::regclass AND a.attnum > 0 AND NOT a.attisdropped
            """, schema, table).ToListAsync();
    }

    [Test]
    public async Task Update_KeepsTheOldVersion_UntilTheNewOnesStart()
    {
        var season = NewSeason("Before");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var inserted = PeriodStart(season);

        season.SeasonName = "After";
        await _context.SaveChangesAsync();
        var updated = PeriodStart(season);

        var history = await History("Event", "Seasons", "SeasonID", season.SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.GreaterThan(inserted));
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0]["SeasonName"], Is.EqualTo("Before"));
            Assert.That(history[0][PostgresHistory.PeriodStart], Is.EqualTo(inserted));
            Assert.That(history[0][PostgresHistory.PeriodEnd], Is.EqualTo(updated));
        });
    }

    [Test]
    public async Task PeriodStart_IsTheDatabasesTime_WhateverTheAppWrites()
    {
        var season = NewSeason("Forged");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        await _context.Database.ExecuteSqlRawAsync("UPDATE \"Event\".\"Seasons\" SET \"PeriodStart\" = '2000-01-01' WHERE \"SeasonID\" = {0}", season.SeasonID);

        var periodStart = await Scalar<DateTime>("SELECT \"PeriodStart\" AS \"Value\" FROM \"Event\".\"Seasons\" WHERE \"SeasonID\" = {0}", season.SeasonID);
        Assert.That(periodStart, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public async Task Delete_KeepsTheLastVersion()
    {
        var season = NewSeason("Deleted");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var inserted = PeriodStart(season);

        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync();

        var history = await History("Event", "Seasons", "SeasonID", season.SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0]["SeasonName"], Is.EqualTo("Deleted"));
            Assert.That(history[0][PostgresHistory.PeriodStart], Is.EqualTo(inserted));
            Assert.That((DateTime)history[0][PostgresHistory.PeriodEnd], Is.GreaterThan(inserted));
        });
    }

    [Test]
    public async Task ExecuteDelete_KeepsTheRowsItCascadesTo()
    {
        var season = NewSeason("Cascading");
        var option = new AvailabilityOption { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(20, 0) };
        _context.SeasonAvailability.Add(new SeasonAvailability { Season = season, AvailabilityOption = option });
        await _context.SaveChangesAsync();

        // In the database, not by EF: the foreign key's ON DELETE CASCADE deletes the availability.
        var deleted = await _context.Seasons.Where(s => s.SeasonID == season.SeasonID).ExecuteDeleteAsync();

        var seasons = await History("Event", "Seasons", "SeasonID", season.SeasonID);
        var availability = await History("Event", "SeasonAvailability", "SeasonID", season.SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(seasons, Has.Count.EqualTo(1));
            Assert.That(availability, Has.Count.EqualTo(1));
            Assert.That(availability[0]["AvailabilityOptionID"], Is.EqualTo(option.AvailabilityOptionID));
            Assert.That(availability[0][PostgresHistory.PeriodEnd], Is.EqualTo(seasons[0][PostgresHistory.PeriodEnd]), "one transaction, one time");
        });
    }

    [Test]
    public async Task ExecuteUpdate_KeepsEveryRow_AndNoRowsKeepNothing()
    {
        _context.Seasons.AddRange(NewSeason("One"), NewSeason("Two"), NewSeason("Three"));
        await _context.SaveChangesAsync();

        var updated = await _context.Seasons.ExecuteUpdateAsync(s => s.SetProperty(x => x.CaptainsLocked, true));
        var none = await _context.Seasons.Where(s => s.SeasonName == "Nobody").ExecuteUpdateAsync(s => s.SetProperty(x => x.CaptainsLocked, false));

        var history = await Scalar<int>("SELECT count(*)::int AS \"Value\" FROM \"Event\".\"SeasonsHistory\" WHERE NOT \"CaptainsLocked\"");
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(3));
            Assert.That(none, Is.Zero);
            Assert.That(history, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Truncate_KeepsEveryRow_OfEveryTableItEmpties()
    {
        var option = new AvailabilityOption { DayOfWeek = DayOfWeek.Friday, Time = new TimeOnly(21, 0) };
        _context.SeasonAvailability.Add(new SeasonAvailability { Season = NewSeason("Truncated"), AvailabilityOption = option });
        await _context.SaveChangesAsync();

        await _context.Database.ExecuteSqlRawAsync("TRUNCATE \"Event\".\"Seasons\" CASCADE");

        var seasons = await Scalar<int>("SELECT count(*)::int AS \"Value\" FROM \"Event\".\"SeasonsHistory\"");
        var availability = await Scalar<int>("SELECT count(*)::int AS \"Value\" FROM \"Event\".\"SeasonAvailabilityHistory\"");
        Assert.Multiple(() =>
        {
            Assert.That(seasons, Is.EqualTo(1));
            Assert.That(availability, Is.EqualTo(1));
        });
    }

    // The query the README shows: the row as it was at a time.
    private const string AsOf = """
        SELECT "SeasonName" AS "Value" FROM "Event"."Seasons"
         WHERE "SeasonID" = @id AND "PeriodStart" <= @at
        UNION ALL
        SELECT "SeasonName" FROM "Event"."SeasonsHistory"
         WHERE "SeasonID" = @id AND "PeriodStart" <= @at AND "PeriodEnd" > @at
        """;

    [Test]
    public async Task AsOf_FindsTheVersionInEffectThen()
    {
        var season = NewSeason("First");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var first = PeriodStart(season);
        season.SeasonName = "Second";
        await _context.SaveChangesAsync();
        var second = PeriodStart(season);
        season.SeasonName = "Third";
        await _context.SaveChangesAsync();
        var third = PeriodStart(season);

        async Task<string[]> At(DateTime time) => [.. await _context.Database.SqlQueryRaw<string>(AsOf,
            new NpgsqlParameter("id", season.SeasonID), new NpgsqlParameter("at", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = time }).ToListAsync()];

        var before = await At(first.AddTicks(-10));
        var atFirst = await At(first);
        var beforeSecond = await At(second.AddTicks(-10));
        var atSecond = await At(second);
        var atThird = await At(third);
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Empty);
            Assert.That(atFirst, Is.EqualTo(new[] { "First" }));
            Assert.That(beforeSecond, Is.EqualTo(new[] { "First" }));
            Assert.That(atSecond, Is.EqualTo(new[] { "Second" }));
            Assert.That(atThird, Is.EqualTo(new[] { "Third" }));
        });
    }

    [Test]
    public async Task AppLogin_WritesHistoryThroughTheTriggers_ButCannotChangeIt()
    {
        var season = NewSeason("Guarded");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        // A login with the rights the Helm chart grants the app (charts/grifballwebapp, grif.pgLoginSyncScript).
        var role = $"grif_app_{Guid.NewGuid():N}";
        await _context.Database.ExecuteSqlRawAsync($"""
            CREATE ROLE "{role}" LOGIN PASSWORD 'app-password';
            DO $$
            DECLARE s text;
            BEGIN
              EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), '{role}');
              FOR s IN SELECT nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' LOOP
                EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', s, '{role}');
                EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I', s, '{role}');
                EXECUTE format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA %I TO %I', s, '{role}');
              END LOOP;
            END $$;
            """);
        var appConnection = new NpgsqlConnectionStringBuilder(_context.Database.GetConnectionString()) { Username = role, Password = "app-password", Pooling = false }.ConnectionString;

        try
        {
            await using (var app = new GrifballContext(TestDatabase.Options(appConnection).Options))
            {
                var mine = await app.Seasons.SingleAsync(s => s.SeasonID == season.SeasonID);
                mine.SeasonName = "Guarded, renamed";
                await app.SaveChangesAsync();
            }

            await using var connection = new NpgsqlConnection(appConnection);
            await connection.OpenAsync();
            PostgresException Refused(string sql)
            {
                using var command = new NpgsqlCommand(sql, connection);
                return Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())!;
            }

            var insert = Refused("INSERT INTO \"Event\".\"SeasonsHistory\" (\"SeasonID\", \"PeriodStart\", \"PeriodEnd\") VALUES (99, now(), now())");
            var update = Refused("UPDATE \"Event\".\"SeasonsHistory\" SET \"SeasonName\" = 'tampered'");
            var delete = Refused("DELETE FROM \"Event\".\"SeasonsHistory\"");
            var truncate = Refused("TRUNCATE \"Event\".\"SeasonsHistory\"");
            var disable = Refused("ALTER TABLE \"Event\".\"Seasons\" DISABLE TRIGGER versioning_update");
            await using var read = new NpgsqlCommand("SELECT \"SeasonName\" FROM \"Event\".\"SeasonsHistory\"", connection);
            var kept = await read.ExecuteScalarAsync();

            Assert.Multiple(() =>
            {
                Assert.That(kept, Is.EqualTo("Guarded"), "the app's update kept the old version, and the app can read it");
                Assert.That(insert.MessageText, Is.EqualTo("history table Event.SeasonsHistory is read-only"));
                Assert.That(new[] { insert, update, delete, truncate, disable }.Select(e => e.SqlState),
                    Is.All.EqualTo(PostgresErrorCodes.InsufficientPrivilege));
            });
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync($"DROP OWNED BY \"{role}\"; DROP ROLE \"{role}\"");
        }
    }

    [Test]
    public async Task Migration_DownAndUpAgain()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var addRowHistory = migrations.IndexOf(migrations.Single(m => m.EndsWith("_AddRowHistory")));

        await migrator.MigrateAsync(migrations[addRowHistory - 1]);
        var down = await Scalar<bool>("""
            SELECT to_regclass('"Event"."SeasonsHistory"') IS NULL AND to_regproc('public.grif_versioning') IS NULL
               AND NOT EXISTS (SELECT FROM information_schema.columns WHERE column_name = 'PeriodStart') AS "Value"
            """);
        await migrator.MigrateAsync();

        var season = NewSeason("Again");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        season.SeasonName = "Again, renamed";
        await _context.SaveChangesAsync();

        var history = await History("Event", "Seasons", "SeasonID", season.SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(down, Is.True, "Down() takes away everything Up() adds");
            Assert.That(history, Has.Count.EqualTo(1));
        });
    }
}
