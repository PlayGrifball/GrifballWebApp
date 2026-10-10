using DotNet.Testcontainers.Containers;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
/// (TestDatabase.BuildPostgresImage). Ignored on SQL Server, whose tables are temporal.
/// </summary>
[TestFixture]
public class RowHistoryTests
{
    private static IDatabaseContainer Server => SetUpFixture.DatabaseContainer;

    // Migrated with the extension once, then copied for each test: versioning every table takes seconds.
    private string? _template;
    private GrifballContext _context;

    [OneTimeSetUp]
    public async Task CreateTemplate()
    {
        if (TestDatabase.Provider != DatabaseProvider.Postgres)
            Assert.Ignore("Row history is PostgreSQL's; SQL Server's tables are temporal.");

        _template = $"TestDbHistory_{Guid.NewGuid():N}";
        await using var context = await NewDatabase(_template, history: true);
        // CREATE DATABASE ... TEMPLATE needs the template's connections closed.
        NpgsqlConnection.ClearPool(new NpgsqlConnection(context.Database.GetConnectionString()));
    }

    [OneTimeTearDown]
    public async Task DropTemplate()
    {
        if (_template is not null)
            await TestDatabase.DropDatabase(TestDatabase.ConnectionString(Server, _template));
    }

    [SetUp]
    public async Task Setup()
    {
        _context = await CopyOf(_template!);
    }

    private static async Task<GrifballContext> CopyOf(string template)
    {
        var name = $"TestDb_{Guid.NewGuid():N}";
        await Admin($"CREATE DATABASE \"{name}\" TEMPLATE \"{template}\"");
        return new GrifballContext(TestDatabase.Options(TestDatabase.ConnectionString(Server, name)).Options);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    /// <summary>A new database, migrated; with the periods extension first when <paramref name="history"/>.</summary>
    private static async Task<GrifballContext> NewDatabase(string name, bool history)
    {
        await TestDatabase.CreateDatabase(Server, name);
        var context = new GrifballContext(TestDatabase.Options(TestDatabase.ConnectionString(Server, name)).Options);
        if (history)
            await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION periods CASCADE");
        await context.Database.MigrateAsync();
        return context;
    }

    private static async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.MaintenanceConnectionString(Server));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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

    [Test]
    public async Task Migration_VersionsEveryTableInTheModel()
    {
        var tables = _context.Model.GetRelationalModel().Tables.Select(t => $"\"{t.Schema ?? "public"}\".\"{t.Name}\"");

        Assert.That(await VersionedTables(_context), Is.EquivalentTo(tables));
    }

    [Test]
    public async Task Update_KeepsTheOldVersion()
    {
        var season = await SeasonRenamed("Season One", "Season 1");

        var history = await Query<string>(_context,
            $"SELECT \"SeasonName\" AS \"Value\" FROM \"Event\".\"Seasons_history\" WHERE \"SeasonID\" = {season.SeasonID}");
        var closed = await Query<bool>(_context, $"""
            SELECT h."PeriodEnd" = s."PeriodStart" AS "Value"
            FROM "Event"."Seasons_history" AS h JOIN "Event"."Seasons" AS s USING ("SeasonID")
            WHERE s."SeasonID" = {season.SeasonID}
            """);
        Assert.Multiple(() =>
        {
            Assert.That(history, Is.EqualTo(new[] { "Season One" }));
            Assert.That(closed, Is.EqualTo(new[] { true }), "the old version ends as the new one starts");
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
            Assert.That(await Count(_context, "\"Event\".\"Seasons\""), Is.Zero);
            Assert.That(await Count(_context, "\"Event\".\"SeasonAvailability\""), Is.Zero);
            Assert.That(await Count(_context, "\"Event\".\"Seasons_history\""), Is.EqualTo(1));
            Assert.That(await Count(_context, "\"Event\".\"SeasonAvailability_history\""), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AsOf_ReadsTheRowsAsTheyWere()
    {
        var season = NewSeason("Before");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var before = (await Query<DateTime>(_context, "SELECT clock_timestamp() AS \"Value\"")).Single();
        season.SeasonName = "After";
        await _context.SaveChangesAsync();

        // The table's own entity: the functions return its columns (and the period's, which EF ignores).
        var then = await _context.Seasons.FromSql($"SELECT * FROM \"Event\".\"Seasons__as_of\"({before})").AsNoTracking().SingleAsync();
        var versions = await Count(_context, "\"Event\".\"Seasons__between\"('-infinity', 'infinity')");

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

        Assert.That(await Count(_context, "\"Event\".\"Seasons_history\""), Is.Zero);
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

                Assert.That(await Count(app, "\"Event\".\"Seasons_history\""), Is.EqualTo(1));
                Assert.That(await Count(app, "\"Event\".\"Seasons__as_of\"('-infinity')"), Is.Zero);
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
                Assert.That(await Count(app, "\"Event\".\"Seasons_history\""), Is.EqualTo(1));
            }
            NpgsqlConnection.ClearPool(new NpgsqlConnection(builder.ConnectionString));
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync($"DROP OWNED BY \"{role}\"; DROP ROLE \"{role}\"");
        }
    }

    [Test]
    public async Task Down_RemovesHistory_UpRestoresIt()
    {
        var migrator = _context.GetService<IMigrator>();

        await migrator.MigrateAsync("20261010153733_InitialCreate");
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

        Assert.Multiple(async () =>
        {
            Assert.That(down, Is.Empty);
            Assert.That(leftovers, Is.Empty);
            Assert.That(await VersionedTables(_context), Has.Count.EqualTo(_context.Model.GetRelationalModel().Tables.Count()));
        });
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
                Columns = { new AddColumnOperation { Schema = "Other", Table = "Notices", Name = "Text", ClrType = typeof(string), ColumnType = "text" } },
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
