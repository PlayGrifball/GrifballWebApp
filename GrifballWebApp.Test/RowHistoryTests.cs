using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GrifballWebApp.Test;

// Raw SQL with names and values the tests made themselves.
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.

/// <summary>
/// Row history on PostgreSQL (readme.md, Database): the periods extension, the RowHistory migration and
/// RowHistoryMigrationsSqlGenerator, on the tests' PostgreSQL, which has the extension
/// (TestDatabase.BuildPostgresImage). The history is read as the app reads it, with EF's temporal queries
/// (TemporalQueryTests has them all); raw SQL only for the database itself: periods' catalog, privileges,
/// DDL, and tables a test renamed out of the model. Ignored on SQL Server, whose tables are temporal.
/// </summary>
[TestFixture]
public class RowHistoryTests
{
    private GrifballContext _context;

    [OneTimeSetUp]
    public void OnlyOnPostgres()
    {
        if (TestDatabase.Provider != DatabaseProvider.Postgres)
            Assert.Ignore("Row history is PostgreSQL's; SQL Server's tables are temporal.");
    }

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContextWithHistory();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static Task<List<T>> Query<T>(GrifballContext context, string sql) => context.Database.SqlQueryRaw<T>(sql).ToListAsync();

    private static async Task<int> Count(GrifballContext context, string table) =>
        (await Query<int>(context, $"SELECT count(*)::int AS \"Value\" FROM {table}")).Single();

    /// <summary>The versioned tables, "Schema"."Table".</summary>
    private static Task<List<string>> VersionedTables(GrifballContext context) => Query<string>(context, """
        SELECT format('"%s"."%s"', n.nspname, c.relname) AS "Value"
        FROM periods.system_versioning AS v
        JOIN pg_class AS c ON c.oid = v.table_name
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        """);

    private static Season NewSeason(string name) => new()
    {
        SeasonName = name,
        PublicAt = new DateTime(2026, 1, 1),
        SignupsOpen = new DateTime(2026, 1, 1),
        SignupsClose = new DateTime(2026, 1, 8),
        DraftStart = new DateTime(2026, 1, 9),
        SeasonStart = new DateTime(2026, 1, 15),
        SeasonEnd = new DateTime(2026, 3, 1),
    };

    /// <summary>Each save is its own transaction: a row changed twice in one keeps only its last version.</summary>
    private async Task<Season> SeasonRenamed(string from, string to)
    {
        var season = NewSeason(from);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        season.SeasonName = to;
        await _context.SaveChangesAsync();
        return season;
    }

    /// <summary>The model's tables with the period: those SQL Server keeps history of.</summary>
    private static IEnumerable<string> TemporalTables(GrifballContext context) =>
        context.Model.GetRelationalModel().Tables
            .Where(t => t.EntityTypeMappings.Any(m => ((IEntityType)m.TypeBase).HasSystemTimePeriod()))
            .Select(t => $"\"{t.Schema ?? "public"}\".\"{t.Name}\"");

    [Test]
    public async Task Migration_VersionsTheTablesSqlServerKeepsHistoryOf()
    {
        var versioned = await VersionedTables(_context);

        Assert.Multiple(() =>
        {
            Assert.That(versioned, Is.EquivalentTo(TemporalTables(_context)));
            Assert.That(versioned, Has.Count.EqualTo(40));
            Assert.That(versioned, Does.Not.Contain("\"public\".\"MatchReschedules\""));
        });
    }

    private record Version(string Name, DateTime Start, DateTime End);

    /// <summary>The row's versions, oldest first: EF's TemporalAll, as the app reads them.</summary>
    private static Task<List<Version>> Versions(GrifballContext context, int seasonID) =>
        context.Seasons.TemporalAll().Where(s => s.SeasonID == seasonID)
            .OrderBy(s => EF.Property<DateTime>(s, RowHistory.PeriodStart))
            .Select(s => new Version(s.SeasonName, EF.Property<DateTime>(s, RowHistory.PeriodStart), EF.Property<DateTime>(s, RowHistory.PeriodEnd)))
            .ToListAsync();

    [Test]
    public async Task Update_KeepsTheOldVersion()
    {
        var season = await SeasonRenamed("Season One", "Season 1");

        var versions = await Versions(_context, season.SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(versions.Select(v => v.Name), Is.EqualTo(new[] { "Season One", "Season 1" }));
            Assert.That(versions[0].End, Is.EqualTo(versions[1].Start), "the old version ends as the new one starts");
            Assert.That(versions[1].End, Is.EqualTo(DateTime.MaxValue));
        });
    }

    [Test]
    public async Task Delete_KeepsTheRows_CascadesIncluded()
    {
        var option = new AvailabilityOption { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(20, 0) };
        var season = NewSeason("Doomed");
        _context.AddRange(option, season);
        await _context.SaveChangesAsync();
        _context.SeasonAvailability.Add(new SeasonAvailability { SeasonID = season.SeasonID, AvailabilityOptionID = option.AvailabilityOptionID });
        await _context.SaveChangesAsync();

        // The database's own ON DELETE CASCADE, not EF's.
        await _context.Seasons.Where(s => s.SeasonID == season.SeasonID).ExecuteDeleteAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Seasons.CountAsync(), Is.Zero);
            Assert.That(await _context.SeasonAvailability.CountAsync(), Is.Zero);
            Assert.That(await _context.Seasons.TemporalAll().CountAsync(), Is.EqualTo(1));
            Assert.That(await _context.SeasonAvailability.TemporalAll().CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AsOf_ReadsTheRowsAsTheyWere()
    {
        var season = NewSeason("Before");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var before = await _context.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => EF.Property<DateTime>(s, RowHistory.PeriodStart)).SingleAsync();
        season.SeasonName = "After";
        await _context.SaveChangesAsync();

        var then = await _context.Seasons.TemporalAsOf(before).SingleAsync();
        var versions = await _context.Seasons.TemporalBetween(DateTime.MinValue, DateTime.MaxValue).CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(then.SeasonName, Is.EqualTo("Before"));
            Assert.That(then.SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(versions, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Truncate_EmptiesTheHistoryToo()
    {
        await SeasonRenamed("One", "Two");

        await _context.Database.ExecuteSqlRawAsync("TRUNCATE \"Event\".\"Seasons\" CASCADE");

        Assert.That(await _context.Seasons.TemporalAll().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task EF_NeverWritesThePeriodColumns_AndReadsThemBack()
    {
        var commands = new List<string>();
        await using var logged = new GrifballContext(TestDatabase.Options(_context.Database.GetConnectionString()!)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options);
        var season = NewSeason("Logged");
        logged.Seasons.Add(season);
        await logged.SaveChangesAsync();
        var inserted = logged.Entry(season).Property<DateTime>(RowHistory.PeriodStart).CurrentValue;
        season.SeasonName = "Logged again";
        await logged.SaveChangesAsync();
        var updated = logged.Entry(season).Property<DateTime>(RowHistory.PeriodStart).CurrentValue;

        // Each write's own part, before what it reads back.
        var writes = commands.Where(c => c.Contains("INSERT INTO") || c.Contains("UPDATE ")).Select(c => c.Split("RETURNING")[0]).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(writes, Has.Count.EqualTo(2));
            Assert.That(writes, Has.All.Not.Contain(RowHistory.PeriodStart).And.All.Not.Contain(RowHistory.PeriodEnd));
            Assert.That(updated, Is.GreaterThan(inserted), "the database's new PeriodStart, read back");
            Assert.That(logged.Entry(season).Property<DateTime>(RowHistory.PeriodEnd).CurrentValue, Is.EqualTo(DateTime.MaxValue));
        });
    }

    // Without the extension (history off, the default): the period columns are there, as the model has
    // them, and nothing keeps history, so there is none to query: temporal queries say so
    // (PostgresTemporalQueries.NoHistoryException).
    [Test]
    public async Task WithoutTheExtension_TheColumnsAreThere_AndThereIsNoHistory()
    {
        await using var plain = await SetUpFixture.NewGrifballContext();
        try
        {
            var season = NewSeason("Plain");
            plain.Seasons.Add(season);
            await plain.SaveChangesAsync();
            season.SeasonName = "Plain, changed";
            await plain.SaveChangesAsync();

            var end = await plain.Seasons.Select(s => EF.Property<DateTime>(s, RowHistory.PeriodEnd)).SingleAsync();
            Assert.That(end, Is.EqualTo(DateTime.MaxValue));

            // A temporal query says there's no history, sync or async, and never returns nothing; the
            // database's error inside. Other queries' errors stay theirs.
            var errors = new (string Query, Exception? Error)[]
            {
                ("ToListAsync", Assert.CatchAsync(() => plain.Seasons.TemporalAll().ToListAsync())),
                ("ToList", Assert.Catch(() => plain.Seasons.TemporalAll().ToList())),
                ("AsOf Include, async", Assert.CatchAsync(() => plain.SeasonMatches.TemporalAsOf(DateTime.UtcNow).Include(m => m.Season).ToListAsync())),
                ("CountAsync", Assert.CatchAsync(() => plain.Seasons.TemporalBetween(DateTime.MinValue, DateTime.UtcNow).CountAsync())),
                ("Any", Assert.Catch(() => plain.Seasons.TemporalFromTo(DateTime.MinValue, DateTime.UtcNow).Any())),
            };
            var other = Assert.CatchAsync(() => plain.Seasons.FromSqlRaw("SELECT * FROM \"Event\".\"NoSuchTable\"").ToListAsync());
            Assert.Multiple(() =>
            {
                foreach (var (query, error) in errors)
                {
                    Assert.That(error, Is.TypeOf<InvalidOperationException>(), query);
                    Assert.That(error!.Message, Does.StartWith("Row history isn't enabled on this PostgreSQL database"), query);
                    Assert.That(error.InnerException, Is.TypeOf<PostgresException>()
                        .With.Property(nameof(PostgresException.SqlState)).EqualTo(PostgresErrorCodes.UndefinedTable), query);
                }
                Assert.That(other, Is.TypeOf<PostgresException>().With.Property(nameof(PostgresException.SqlState)).EqualTo(PostgresErrorCodes.UndefinedTable));
            });

            // The context is fine after: the next query runs.
            Assert.That(await plain.Seasons.CountAsync(), Is.EqualTo(1));
        }
        finally
        {
            await plain.DropDatabaseAndDispose();
        }
    }

    // As the Helm chart's history step and login sync do (charts/grifballwebapp/templates/_postgres.tpl):
    // periods' functions closed to PUBLIC (they're SECURITY DEFINER and check nothing: anyone could turn
    // a table's history off), and the app granted read and write on each table but the history tables
    // and views, which periods gives it SELECT on.
    [Test]
    public async Task AppLogin_WritesTheTables_ReadsTheirHistory_ChangesNoHistory()
    {
        var role = $"grif_app_{Guid.NewGuid():N}";
        await _context.Database.ExecuteSqlRawAsync($"CREATE ROLE \"{role}\" LOGIN PASSWORD 'app-password'");
        try
        {
            // Every table in a schema at once includes the history tables, which periods refuses.
            var all = Assert.ThrowsAsync<PostgresException>(() =>
                _context.Database.ExecuteSqlRawAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"Event\" TO \"{role}\""));
            Assert.That(all!.MessageText, Does.Contain("history objects are read-only"));

            await _context.Database.ExecuteSqlRawAsync($$"""
                DO $$
                DECLARE
                    statement text;
                BEGIN
                    FOR statement IN
                        SELECT 'REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA periods FROM PUBLIC'
                        UNION ALL
                        SELECT format('GRANT USAGE ON SCHEMA %I TO %I', nspname, '{{role}}')
                        FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname NOT IN ('information_schema', 'periods')
                        UNION ALL
                        SELECT format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA %I TO %I', nspname, '{{role}}')
                        FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname NOT IN ('information_schema', 'periods')
                        UNION ALL
                        SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON %s TO %I', c.oid::regclass, '{{role}}')
                        FROM pg_class AS c JOIN pg_namespace AS n ON n.oid = c.relnamespace
                        WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f') AND n.nspname NOT LIKE 'pg\_%' AND n.nspname NOT IN ('information_schema', 'periods')
                          AND c.oid NOT IN (SELECT history_table_name FROM periods.system_versioning UNION ALL SELECT view_name FROM periods.system_versioning)
                    LOOP
                        EXECUTE statement;
                    END LOOP;
                END $$
                """);

            var builder = new NpgsqlConnectionStringBuilder(_context.Database.GetConnectionString()) { Username = role, Password = "app-password" };
            await using (var app = new GrifballContext(TestDatabase.Options(builder.ConnectionString).Options))
            {
                var season = NewSeason("By the app");
                app.Seasons.Add(season);
                await app.SaveChangesAsync();
                season.SeasonName = "Changed by the app";
                await app.SaveChangesAsync();

                // Through EF's temporal queries: periods' view of the table and its history, which the app reads.
                Assert.That((await Versions(app, season.SeasonID)).Select(v => v.Name), Is.EqualTo(new[] { "By the app", "Changed by the app" }));
                foreach (var sql in new[]
                {
                    "DELETE FROM \"Event\".\"Seasons_history\"",
                    "UPDATE \"Event\".\"Seasons_history\" SET \"SeasonName\" = 'forged'",
                    "TRUNCATE \"Event\".\"Seasons\" CASCADE",
                    "SELECT periods.drop_system_versioning('\"Event\".\"Seasons\"')",
                })
                {
                    var denied = Assert.ThrowsAsync<PostgresException>(() => app.Database.ExecuteSqlRawAsync(sql), sql);
                    Assert.That(denied!.SqlState, Is.EqualTo(PostgresErrorCodes.InsufficientPrivilege), sql);
                }
                Assert.That((await Versions(app, season.SeasonID)).Select(v => v.Name), Is.EqualTo(new[] { "By the app", "Changed by the app" }));
            }
            NpgsqlConnection.ClearPool(new NpgsqlConnection(builder.ConnectionString));
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync($"DROP OWNED BY \"{role}\"; DROP ROLE \"{role}\"");
        }
    }

    // The migration is EF's scaffolding: Up adds the columns, which RowHistoryMigrationsSqlGenerator versions
    // each table by once it has both; Down drops them, which it purges each table's history and period for first.
    [Test]
    public async Task Down_RemovesHistory_UpRestoresIt()
    {
        var migrator = _context.GetService<IMigrator>();
        var season = await SeasonRenamed("Season One", "Season 1");

        await migrator.MigrateAsync("20261011055042_InitialCreate");
        var down = await VersionedTables(_context);
        var leftovers = await Query<string>(_context, """
            SELECT format('%I.%I', table_schema, table_name) AS "Value" FROM information_schema.tables
            WHERE table_name LIKE '%\_history' AND table_schema <> 'periods'
            UNION ALL
            SELECT format('%I.%I.%I', table_schema, table_name, column_name) FROM information_schema.columns
            WHERE column_name IN ('PeriodStart', 'PeriodEnd') AND table_schema <> 'periods'
            UNION ALL
            SELECT format('%s %I', t.tgrelid::regclass, t.tgname) FROM pg_trigger AS t
            JOIN pg_proc AS p ON p.oid = t.tgfoid JOIN pg_namespace AS n ON n.oid = p.pronamespace WHERE n.nspname = 'periods'
            """);
        await migrator.MigrateAsync();
        await _context.Seasons.Where(s => s.SeasonID == season.SeasonID).ExecuteUpdateAsync(s => s.SetProperty(x => x.SeasonName, "Season I"));

        var versions = await Versions(_context, season.SeasonID);
        Assert.Multiple(async () =>
        {
            Assert.That(down, Is.Empty);
            Assert.That(leftovers, Is.Empty);
            Assert.That(await VersionedTables(_context), Is.EquivalentTo(TemporalTables(_context)));
            // The history went with Down; the row, there before Up, is current since -infinity.
            Assert.That(versions.Select(v => v.Name), Is.EqualTo(new[] { "Season 1", "Season I" }));
            Assert.That(versions[0].Start, Is.EqualTo(DateTime.MinValue));
            Assert.That(versions[0].End, Is.EqualTo(versions[1].Start));
        });
    }

    /// <summary>A new database, empty: a test runs the migrations as it needs them.</summary>
    private static async Task<GrifballContext> EmptyDatabase()
    {
        var name = $"TestDb_{Guid.NewGuid():N}";
        await TestDatabase.CreateDatabase(SetUpFixture.DatabaseContainer, name);
        return new GrifballContext(TestDatabase.Options(TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, name)).Options);
    }

    /// <summary>
    /// What the Helm chart's history step does to a database migrated before it had the extension
    /// (grif.pgHistoryScript in charts/grifballwebapp/templates/_postgres.tpl): the extension, then SYSTEM
    /// VERSIONING on every table with the period's columns that hasn't it.
    /// </summary>
    private const string ChartHistoryStep = """
        CREATE EXTENSION IF NOT EXISTS periods CASCADE;
        DO $$
        DECLARE
            t regclass;
        BEGIN
            FOR t IN
                SELECT c.oid::regclass
                  FROM pg_class AS c JOIN pg_namespace AS n ON n.oid = c.relnamespace
                 WHERE c.relkind = 'r' AND n.nspname NOT LIKE 'pg\_%' AND n.nspname NOT IN ('information_schema', 'periods')
                   AND (n.nspname, c.relname) <> ('public', '__EFMigrationsHistory')
                   AND c.oid NOT IN (SELECT table_name FROM periods.system_versioning UNION ALL SELECT history_table_name FROM periods.system_versioning)
                   AND NOT EXISTS (SELECT FROM pg_depend AS d WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype = 'e')
                   AND EXISTS (SELECT FROM pg_attribute AS a WHERE a.attrelid = c.oid AND a.attname = 'PeriodStart' AND NOT a.attisdropped)
                   AND EXISTS (SELECT FROM pg_attribute AS a WHERE a.attrelid = c.oid AND a.attname = 'PeriodEnd' AND NOT a.attisdropped)
                 ORDER BY n.nspname, c.relname
            LOOP
                PERFORM periods.add_system_time_period(t, 'PeriodStart', 'PeriodEnd');
                PERFORM periods.add_system_versioning(t);
            END LOOP;
        END $$
        """;

    /// <summary>The database's shape as it bears on history: every period column and periods' constraints, triggers and objects.</summary>
    private static Task<List<string>> HistoryShape(GrifballContext context) => Query<string>(context, """
        SELECT format('%s.%I %s %s default %s', a.attrelid::regclass, a.attname, format_type(a.atttypid, a.atttypmod),
                      CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END, pg_get_expr(d.adbin, d.adrelid)) AS "Value"
          FROM pg_attribute AS a JOIN pg_class AS c ON c.oid = a.attrelid JOIN pg_namespace AS n ON n.oid = c.relnamespace
          LEFT JOIN pg_attrdef AS d ON (d.adrelid, d.adnum) = (a.attrelid, a.attnum)
         WHERE a.attname IN ('PeriodStart', 'PeriodEnd') AND NOT a.attisdropped AND c.relkind = 'r' AND n.nspname <> 'periods'
        UNION ALL
        SELECT format('%s constraint %s', conrelid::regclass, pg_get_constraintdef(oid)) FROM pg_constraint
         WHERE pg_get_constraintdef(oid) LIKE '%Period%'
        UNION ALL
        SELECT format('%s trigger %I', t.tgrelid::regclass, t.tgname) FROM pg_trigger AS t
          JOIN pg_proc AS p ON p.oid = t.tgfoid JOIN pg_namespace AS n ON n.oid = p.pronamespace WHERE n.nspname = 'periods'
        UNION ALL
        SELECT format('%s %s %s %s', table_name, history_table_name, view_name, func_as_of) FROM periods.system_versioning
        """);

    // The extension is the switch, whenever it's put there: before the migrations (as the tests' databases
    // have it), between them, or after them (a database from before history was on, which the Helm chart's
    // history step versions). The same tables versioned the same way, and a row already there before is
    // current since -infinity, its next version kept.
    [Test]
    public async Task Extension_First_Between_OrAfter_TheMigrations_SameHistory()
    {
        await using var between = await EmptyDatabase();
        await using var after = await EmptyDatabase();
        try
        {
            // A row from before the RowHistory migration in each: raw SQL, the model has the period's columns.
            const string before = """
                INSERT INTO "Event"."Seasons" ("SeasonName", "CaptainsLocked", "PublicAt", "SignupsOpen", "SignupsClose", "DraftStart", "SeasonStart", "SeasonEnd", "CreatedAt", "ModifiedAt")
                VALUES ('Before', false, now(), now(), now(), now(), now(), now(), now(), now())
                """;
            foreach (var context in new[] { between, after })
            {
                await context.GetService<IMigrator>().MigrateAsync("20261011055042_InitialCreate");
                await context.Database.ExecuteSqlRawAsync(before);
            }
            await between.Database.ExecuteSqlRawAsync("CREATE EXTENSION periods CASCADE");
            await between.Database.MigrateAsync();

            await after.Database.MigrateAsync();
            await after.Database.ExecuteSqlRawAsync(ChartHistoryStep);

            var first = await HistoryShape(_context);
            foreach (var context in new[] { between, after })
            {
                var id = await context.Seasons.Select(s => s.SeasonID).SingleAsync();
                await context.Seasons.ExecuteUpdateAsync(s => s.SetProperty(x => x.SeasonName, "After"));
                var versions = await Versions(context, id);
                Assert.Multiple(async () =>
                {
                    Assert.That(await HistoryShape(context), Is.EquivalentTo(first));
                    Assert.That(await VersionedTables(context), Is.EquivalentTo(TemporalTables(context)));
                    Assert.That(versions.Select(v => v.Name), Is.EqualTo(new[] { "Before", "After" }));
                    Assert.That(versions[0].Start, Is.EqualTo(DateTime.MinValue));
                });
            }
            Assert.That(first, Has.Some.Contains("\"Event\".\"Seasons\".\"PeriodStart\" timestamp with time zone not null default transaction_timestamp()"));
        }
        finally
        {
            await between.DropDatabaseAndDispose();
            await after.DropDatabaseAndDispose();
        }
    }

    /// <summary>The period's columns, as EF scaffolds them for a table made temporal (RowHistory).</summary>
    private static AddColumnOperation PeriodColumn(string table, string name) => new()
    {
        Table = table, Name = name, ClrType = typeof(DateTime), ColumnType = "timestamp with time zone",
        DefaultValueSql = name == RowHistory.PeriodStart ? RowHistory.StartDefault : RowHistory.EndDefault,
    };

    /// <summary>Each list of operations as a migration, through the migrations SQL generator.</summary>
    private static async Task Migrate(GrifballContext context, params MigrationOperation[] operations) =>
        await context.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(
            context.GetService<IMigrationsSqlGenerator>().Generate(operations), context.GetService<IRelationalConnection>());

    // An existing table made temporal later (MatchReschedules marked IsTemporal: EF scaffolds AddColumn
    // PeriodEnd, then PeriodStart): versioned once it has both columns, whatever their order, in one
    // migration or two. Its rows already there are current since -infinity, and their next versions kept.
    [TestCase("PeriodEnd PeriodStart")]
    [TestCase("PeriodStart PeriodEnd")]
    [TestCase("PeriodStart", "PeriodEnd")]
    public async Task TableMadeTemporalLater_IsVersioned_AndKeepsHistory(params string[] migrations)
    {
        var seeded = await RescheduleTestData.SeedAsync(_context);
        var reschedule = await RescheduleTestData.AddReschedule(_context, seeded.Match.SeasonMatchID, seeded.Requester.Id);

        var notVersioned = new List<string>();
        foreach (var migration in migrations)
        {
            notVersioned.AddRange(await VersionedTables(_context));
            await Migrate(_context, migration.Split(' ').Select(c => PeriodColumn("MatchReschedules", c)).ToArray<MigrationOperation>());
        }
        reschedule.Reason = "Can make it after all";
        await _context.SaveChangesAsync();

        // Out of the model, which has no period for it: raw SQL.
        var current = await Query<DateTime>(_context, $"""SELECT "PeriodStart" AS "Value" FROM "MatchReschedules" WHERE "MatchRescheduleID" = {reschedule.MatchRescheduleID}""");
        var history = await Query<string>(_context, """SELECT format('%s %s', "Reason", "PeriodStart") AS "Value" FROM "MatchReschedules_history" """);
        Assert.Multiple(async () =>
        {
            Assert.That(notVersioned, Does.Not.Contain("\"public\".\"MatchReschedules\""));
            Assert.That(await VersionedTables(_context), Does.Contain("\"public\".\"MatchReschedules\""));
            Assert.That(history, Is.EqualTo(new[] { "Cannot make it -infinity" }));
            Assert.That(current.Single(), Is.GreaterThan(DateTime.MinValue).And.LessThan(DateTime.MaxValue));
        });
    }

    // Without the extension: the columns alone, the rows already there current since -infinity, new rows
    // since they're added (the model's default); no history.
    [Test]
    public async Task TableMadeTemporalLater_WithoutTheExtension_GetsTheColumns()
    {
        await using var plain = await SetUpFixture.NewGrifballContext();
        try
        {
            var seeded = await RescheduleTestData.SeedAsync(plain);
            var before = await RescheduleTestData.AddReschedule(plain, seeded.Match.SeasonMatchID, seeded.Requester.Id);

            await Migrate(plain, PeriodColumn("MatchReschedules", RowHistory.PeriodEnd), PeriodColumn("MatchReschedules", RowHistory.PeriodStart));
            var added = await RescheduleTestData.AddReschedule(plain, seeded.Match.SeasonMatchID, seeded.Requester.Id);

            var periods = await Query<string>(plain, """SELECT format('%s %s %s', "MatchRescheduleID", ("PeriodStart" = '-infinity')::text, "PeriodEnd") AS "Value" FROM "MatchReschedules" ORDER BY "MatchRescheduleID" """);
            Assert.Multiple(async () =>
            {
                Assert.That(periods, Is.EqualTo(new[] { $"{before.MatchRescheduleID} true infinity", $"{added.MatchRescheduleID} false infinity" }));
                Assert.That(await Query<string>(plain, "SELECT to_regclass('\"MatchReschedules_history\"')::text AS \"Value\""), Is.EqualTo(new string?[] { null }));
            });
        }
        finally
        {
            await plain.DropDatabaseAndDispose();
        }
    }

    /// <summary>
    /// What later migrations do to existing tables, through the migrations SQL generator as a migration
    /// would (each list in its own transaction), then a write to each table. Seasons ends up as
    /// "Other"."Leagues", with a column added, changed, renamed and dropped; DataProtectionKeys is
    /// dropped; a new table is created.
    /// </summary>
    private static async Task ApplyLaterMigrations(GrifballContext context, Season season)
    {
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var executor = context.GetService<IMigrationCommandExecutor>();
        var connection = context.GetService<IRelationalConnection>();
        async Task Migrate(string write, params MigrationOperation[] operations)
        {
            await executor.ExecuteNonQueryAsync(generator.Generate(operations), connection);
            await context.Database.ExecuteSqlRawAsync(write);
        }
        var id = season.SeasonID;

        await Migrate($"UPDATE \"Event\".\"Seasons\" SET \"Notes\" = 'noted', \"Round\" = 2 WHERE \"SeasonID\" = {id}",
            new AddColumnOperation { Schema = "Event", Table = "Seasons", Name = "Notes", ClrType = typeof(string), ColumnType = "text", Collation = "und-x-icu", IsNullable = true },
            new AddColumnOperation { Schema = "Event", Table = "Seasons", Name = "Round", ClrType = typeof(int), ColumnType = "integer", DefaultValue = 1 });
        await Migrate($"UPDATE \"Event\".\"Seasons\" SET \"Remarks\" = 'remarked' WHERE \"SeasonID\" = {id}",
            new RenameColumnOperation { Schema = "Event", Table = "Seasons", Name = "Notes", NewName = "Remarks" });
        await Migrate($"UPDATE \"Event\".\"Seasons\" SET \"SeasonName\" = NULL WHERE \"SeasonID\" = {id}",
            new AlterColumnOperation
            {
                Schema = "Event", Table = "Seasons", Name = "SeasonName", ClrType = typeof(string), ColumnType = "character varying(60)", Collation = "und-x-icu", IsNullable = true,
                OldColumn = new AddColumnOperation { ClrType = typeof(string), ColumnType = "character varying(30)", Collation = "und-x-icu", IsNullable = false },
            });
        await Migrate($"UPDATE \"Event\".\"Seasons\" SET \"SeasonName\" = '{new string('x', 60)}' WHERE \"SeasonID\" = {id}",
            new DropColumnOperation { Schema = "Event", Table = "Seasons", Name = "Remarks" });
        await Migrate($"UPDATE \"Other\".\"Leagues\" SET \"Round\" = 3 WHERE \"SeasonID\" = {id}",
            new RenameTableOperation { Schema = "Event", Name = "Seasons", NewSchema = "Other", NewName = "Leagues" });
        await Migrate("SELECT 1",
            new DropTableOperation { Schema = "Auth", Name = "DataProtectionKeys" });
        await Migrate("INSERT INTO \"Other\".\"Notices\" (\"Text\") VALUES ('first')",
            new CreateTableOperation
            {
                Schema = "Other", Name = "Notices",
                // As EF creates a table of the model: with the period's columns, which periods takes over.
                Columns =
                {
                    new AddColumnOperation { Schema = "Other", Table = "Notices", Name = "Text", ClrType = typeof(string), ColumnType = "text" },
                    new AddColumnOperation { Schema = "Other", Table = "Notices", Name = RowHistory.PeriodStart, ClrType = typeof(DateTime), ColumnType = "timestamp with time zone", DefaultValueSql = RowHistory.StartDefault },
                    new AddColumnOperation { Schema = "Other", Table = "Notices", Name = RowHistory.PeriodEnd, ClrType = typeof(DateTime), ColumnType = "timestamp with time zone", DefaultValueSql = RowHistory.EndDefault },
                },
            });
        await context.Database.ExecuteSqlRawAsync("UPDATE \"Other\".\"Notices\" SET \"Text\" = 'second'");
    }

    [Test]
    public async Task LaterMigrations_KeepHistoryWorking_AndKeepIt()
    {
        var season = await SeasonRenamed("Season One", "Season 1");

        await ApplyLaterMigrations(_context, season);

        var names = await Query<string>(_context, $"""
            SELECT coalesce("SeasonName", '<null>') AS "Value" FROM "Other"."Leagues__between"('-infinity', 'infinity')
            WHERE "SeasonID" = {season.SeasonID} ORDER BY "PeriodStart"
            """);
        var rounds = await Query<int>(_context,
            $"SELECT \"Round\" AS \"Value\" FROM \"Other\".\"Leagues_with_history\" WHERE \"SeasonID\" = {season.SeasonID} ORDER BY \"PeriodStart\"");
        Assert.Multiple(async () =>
        {
            // Every version, from before the first migration on: the history moved and changed with the table.
            Assert.That(names, Is.EqualTo(new[] { "Season One", "Season 1", "Season 1", "Season 1", "<null>", new string('x', 60), new string('x', 60) }));
            Assert.That(rounds, Is.EqualTo(new[] { 1, 1, 2, 2, 2, 2, 3 }), "the new column's default fills the older versions too");
            Assert.That(await VersionedTables(_context), Does.Contain("\"Other\".\"Leagues\"").And.Contain("\"Other\".\"Notices\"")
                .And.Not.Contain("\"Event\".\"Seasons\"").And.Not.Contain("\"Auth\".\"DataProtectionKeys\""));
            Assert.That(await Count(_context, "\"Other\".\"Notices_history\""), Is.EqualTo(1));
            Assert.That(await Query<string>(_context, "SELECT to_regclass('\"Auth\".\"DataProtectionKeys_history\"')::text AS \"Value\""), Is.EqualTo(new string?[] { null }));
        });
    }

    // Without the extension (history off, the default): the same migrations, as plain DDL.
    [Test]
    public async Task LaterMigrations_WithoutHistory_RunAsTheyAre()
    {
        await using var plain = await SetUpFixture.NewGrifballContext();
        try
        {
            var season = NewSeason("Plain");
            plain.Seasons.Add(season);
            await plain.SaveChangesAsync();

            await ApplyLaterMigrations(plain, season);

            Assert.Multiple(async () =>
            {
                Assert.That(await Query<int>(plain, $"SELECT \"Round\" AS \"Value\" FROM \"Other\".\"Leagues\" WHERE \"SeasonID\" = {season.SeasonID}"), Is.EqualTo(new[] { 3 }));
                Assert.That(await Query<bool>(plain, "SELECT EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') AS \"Value\""), Is.EqualTo(new[] { false }));
                Assert.That(await Count(plain, "\"Other\".\"Notices\""), Is.EqualTo(1));
            });
        }
        finally
        {
            await plain.DropDatabaseAndDispose();
        }
    }
}
