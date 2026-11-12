using GrifballWebApp.Infrastructure;
using GrifballWebApp.Migrations.Sqlite;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Domain.Test;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrifballWebApp.Migrations.Test;

/// <summary>
/// SqliteHistoryTests' tests that scaffold a migration
/// (SqliteHistoryMigrationsModelDiffer, its triggers, and dotnet ef's own design-time services): the migrations
/// assembly (GrifballWebApp.Migrations.Sqlite) loaded, on a migrated SQLite temp file of the test's own.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SqliteHistoryMigrationsTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), $"grif-sqlite-history-migrations-{Environment.ProcessId}");
    private static string Template => Path.Combine(Folder, "template.db");

    private GrifballContext _context = null!;

    [OneTimeSetUp]
    public static async Task MigrateTemplate()
    {
        Directory.CreateDirectory(Folder);
        await using var context = NewContext(Template);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
        SqliteConnection.ClearAllPools();
    }

    [OneTimeTearDown]
    public static void DeleteFolder()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(Folder, recursive: true);
    }

    [SetUp]
    public void SetUp()
    {
        var file = Path.Combine(Folder, $"{Guid.NewGuid():N}.db");
        File.Copy(Template, file);
        _context = NewContext(file);
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    private static GrifballContext NewContext(string file) => new(Options(file));

    private static DbContextOptions<GrifballContext> Options(string file)
        => new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={file}").Options;

    private string File_ => new SqliteConnectionStringBuilder(_context.Database.GetConnectionString()).DataSource;

    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Season NewSeason(string name) => Season.Seed(seasonName: name).WithDates(publicAt: Now, signupsOpen: Now, signupsClose: Now.AddDays(7), draftStart: Now.AddDays(8), seasonStart: Now.AddDays(14), seasonEnd: Now.AddDays(60));

    private record Version(string SeasonName, DateTime PeriodStart, DateTime PeriodEnd);

    /// <summary>Applies the migration migrations add would scaffold from <paramref name="source"/>'s model to <paramref name="target"/>'s.</summary>
    private static async Task<IReadOnlyList<MigrationOperation>> Migrate(GrifballContext source, GrifballContext target)
    {
        var operations = new SqliteHistoryMigrationsModelDiffer(target.GetService<IMigrationsModelDiffer>())
            .GetDifferences(source.GetService<IDesignTimeModel>().Model.GetRelationalModel(), target.GetService<IDesignTimeModel>().Model.GetRelationalModel());
        var commands = target.GetService<IMigrationsSqlGenerator>().Generate(operations, target.GetService<IDesignTimeModel>().Model);
        await target.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, target.GetService<IRelationalConnection>());
        return operations;
    }

    private record NamedNote(string Name, string? Note);

    // A migration that renames a column, adds one and drops one: SQLite rebuilds the table, which takes its
    // triggers with the old one, so they're dropped first and created again last, with the new columns.
    [Test]
    public async Task ChangingATablesColumns_KeepsItsHistoryComplete()
    {
        var season = NewSeason("First");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        season.Edited(seasonName: "Second");
        await _context.SaveChangesAsync();

        await using var reworked = new SeasonReworkedContext(Options(File_));
        var operations = await Migrate(_context, reworked);

        var row = await reworked.Seasons.SingleAsync(s => s.SeasonID == season.SeasonID);
        row.Edited(seasonName: "Third");
        reworked.Entry(row).Property("Note").CurrentValue = "noted";
        await reworked.SaveChangesAsync();
        reworked.Seasons.Remove(row);
        await reworked.SaveChangesAsync();

        var history = await reworked.Database.SqlQueryRaw<NamedNote>(
            "SELECT \"Name\", \"Note\" FROM \"SeasonsHistory\" WHERE \"SeasonID\" = {0} ORDER BY \"PeriodEnd\", rowid", season.SeasonID).ToListAsync();
        var sql = operations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Has.Count.EqualTo(2));
            Assert.That(operations[0], Is.InstanceOf<SqlOperation>(), "the triggers dropped first");
            Assert.That(sql[0], Is.EqualTo(SqliteHistory.DropTriggers("Seasons")));
            Assert.That(operations[^1], Is.InstanceOf<SqlOperation>(), "and created last");
            Assert.That(sql[1], Does.Contain("\"Name\"").And.Contain("\"Note\"").And.Not.Contain("CaptainsLocked"));
            Assert.That(history, Is.EqualTo(new[]
            {
                new NamedNote("First", null), new NamedNote("Second", null), new NamedNote("Third", "noted"),
            }), "every version, the renamed and the new column in each");
        }
    }

    /// <summary>GrifballContext, with Seasons' SeasonName column called Name, a new column Note, and no CaptainsLocked.</summary>
    private sealed class SeasonReworkedContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var season = modelBuilder.Model.FindEntityType(typeof(Season))!;
            modelBuilder.Model.RemoveEntityType((IMutableEntityType)season.FindHistoryEntityType()!);
            modelBuilder.Entity<Season>(b =>
            {
                b.Property(s => s.SeasonName).HasColumnName("Name");
                b.Property<string?>("Note");
                b.Ignore(s => s.CaptainsLocked);
            });
            modelBuilder.AddSqliteHistoryTables([season]);
        }
    }

    // A table that gets history once it has rows: they get the beginning of time, as SQL Server gives them;
    // rows written after, now. The table without it is made by its own model (EnsureCreated), as an older
    // migration would have left it.
    [Test]
    public async Task ATableThatGetsHistory_GivesItsRowsTheBeginningOfTime()
    {
        var file = Path.Combine(Folder, $"{Guid.NewGuid():N}.db");
        await using var before = new NoSeasonHistoryContext(Options(file));
        await before.Database.EnsureCreatedAsync();
        before.Seasons.Add(NewSeason("Old"));
        await before.SaveChangesAsync();

        await using var after = NewContext(file);
        var operations = await Migrate(before, after);
        var old = await after.Seasons.SingleAsync();
        old.Edited(seasonName: "Older");
        await after.SaveChangesAsync();

        var history = await after.Database.SqlQueryRaw<Version>("SELECT \"SeasonName\", \"PeriodStart\", \"PeriodEnd\" FROM \"SeasonsHistory\"").ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(operations.OfType<AddColumnOperation>().Single(o => o.Table == "Seasons" && o.Name == PostgresHistory.PeriodStart).DefaultValueSql,
                Is.EqualTo(SqliteHistory.BeginningOfTime));
            Assert.That(operations.OfType<AlterColumnOperation>().Single(o => o.Table == "Seasons" && o.Name == PostgresHistory.PeriodStart).DefaultValueSql,
                Is.EqualTo(SqliteHistory.Now));
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].SeasonName, Is.EqualTo("Old"));
            Assert.That(history[0].PeriodStart, Is.EqualTo(DateTime.MinValue));
            Assert.That(after.Entry(old).Property<DateTime>(PostgresHistory.PeriodStart).CurrentValue, Is.GreaterThan(DateTime.MinValue));
        }
    }

    // Dropping a table's history drops its triggers first, and nothing is created for it after.
    [Test]
    public void DroppingAHistoryTable_DropsItsTriggersFirst()
    {
        using var with = NewContext(File_);
        using var without = new NoSeasonsContext(Options(File_));

        var operations = new SqliteHistoryMigrationsModelDiffer(with.GetService<IMigrationsModelDiffer>())
            .GetDifferences(with.GetService<IDesignTimeModel>().Model.GetRelationalModel(), without.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        var sql = operations.OfType<SqlOperation>().Select(o => o.Sql).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Does.Contain(SqliteHistory.DropTriggers("Seasons")));
            Assert.That(sql, Has.None.Contains("CREATE TRIGGER \"Seasons_History_Update\""));
            Assert.That(operations.TakeWhile(o => o is SqlOperation).Count(), Is.EqualTo(operations.OfType<SqlOperation>().Count(o => o.Sql.StartsWith("DROP TRIGGER"))));
        }
    }

    // dotnet ef builds its services the same way, from the startup assembly (this project's migrations)
    [Test]
    public void DesignTimeServices_ScaffoldWithTheHistoryDiffer_AndRawSql()
    {
        var assembly = typeof(DesignTimeServices).Assembly;
#pragma warning disable EF1001 // Internal EF Core API usage: what dotnet ef itself calls.
        var services = new Microsoft.EntityFrameworkCore.Design.Internal.DesignTimeServicesBuilder(
            assembly, assembly, new Microsoft.EntityFrameworkCore.Design.Internal.OperationReporter(null), []).Build(_context);
#pragma warning restore EF1001

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.GetService(typeof(IMigrationsModelDiffer)), Is.InstanceOf<SqliteHistoryMigrationsModelDiffer>());
            Assert.That(services.GetService(typeof(Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpMigrationOperationGenerator)),
                Is.InstanceOf<GrifballWebApp.Migrations.Postgres.RawSqlMigrationOperationGenerator>());
        }
    }

    [Test]
    public void TheSameModel_HasNoDifferences()
    {
        using var context = NewContext(File_);
        var model = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var differ = new SqliteHistoryMigrationsModelDiffer(context.GetService<IMigrationsModelDiffer>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(differ.HasDifferences(model, model), Is.False);
            Assert.That(differ.GetDifferences(model, model), Is.Empty);
        }
    }

    // The renamed table's triggers are dropped by its old name and created by its new.
    [Test]
    public void RenamingATableWithHistory_RenamesItsTriggers()
    {
        using var context = NewContext(File_);
        using var renamed = new SeasonsRenamedContext(Options(File_));

        var sql = new SqliteHistoryMigrationsModelDiffer(context.GetService<IMigrationsModelDiffer>())
            .GetDifferences(context.GetService<IDesignTimeModel>().Model.GetRelationalModel(), renamed.GetService<IDesignTimeModel>().Model.GetRelationalModel())
            .OfType<SqlOperation>().Select(o => o.Sql).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Does.Contain(SqliteHistory.DropTriggers("Seasons")));
            Assert.That(sql, Has.Some.Contains("CREATE TRIGGER \"Leagues2_History_Update\" AFTER UPDATE ON \"Leagues2\""));
        }
    }

    // A table moved to another schema only (no new name): a rename SQLite ignores, its triggers made again all the same
    [Test]
    public void MovingATableWithHistoryToAnotherSchema_RemakesItsTriggers()
    {
        using var moved = new SeasonsMovedContext(Options(File_));

        var sql = new SqliteHistoryMigrationsModelDiffer(_context.GetService<IMigrationsModelDiffer>())
            .GetDifferences(_context.GetService<IDesignTimeModel>().Model.GetRelationalModel(), moved.GetService<IDesignTimeModel>().Model.GetRelationalModel())
            .OfType<SqlOperation>().Select(o => o.Sql).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Does.Contain(SqliteHistory.DropTriggers("Seasons")));
            Assert.That(sql, Has.Some.Contains("CREATE TRIGGER \"Seasons_History_Update\""));
        }
    }

    /// <summary>GrifballContext, with Seasons and its history in the schema Archive.</summary>
    private sealed class SeasonsMovedContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var season = modelBuilder.Model.FindEntityType(typeof(Season))!;
            modelBuilder.Model.RemoveEntityType((IMutableEntityType)season.FindHistoryEntityType()!);
            season.RemoveProperty(PostgresHistory.PeriodStart);
            season.RemoveProperty(PostgresHistory.PeriodEnd);
            modelBuilder.Entity<Season>().ToTable("Seasons", "Archive");
            modelBuilder.AddSqliteHistoryTables([season]);
        }
    }

    /// <summary>GrifballContext, with Seasons without history, as before a migration gives it some.</summary>
    private sealed class NoSeasonHistoryContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var season = modelBuilder.Model.FindEntityType(typeof(Season))!;
            modelBuilder.Model.RemoveEntityType((IMutableEntityType)season.FindHistoryEntityType()!);
            season.RemoveProperty(PostgresHistory.PeriodStart);
            season.RemoveProperty(PostgresHistory.PeriodEnd);
            season.RemoveTrigger(SqliteHistory.UpdateTrigger("Seasons"));
            season.RemoveTrigger(SqliteHistory.DeleteTrigger("Seasons"));
        }
    }

    /// <summary>GrifballContext, with Seasons' table called Leagues2.</summary>
    private sealed class SeasonsRenamedContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var season = modelBuilder.Model.FindEntityType(typeof(Season))!;
            modelBuilder.Model.RemoveEntityType((IMutableEntityType)season.FindHistoryEntityType()!);
            season.RemoveTrigger(SqliteHistory.UpdateTrigger("Seasons"));
            season.RemoveTrigger(SqliteHistory.DeleteTrigger("Seasons"));
            season.RemoveProperty(PostgresHistory.PeriodStart);
            season.RemoveProperty(PostgresHistory.PeriodEnd);
            modelBuilder.Entity<Season>().ToTable("Leagues2", "Event");
            modelBuilder.AddSqliteHistoryTables([season]);
        }
    }

    /// <summary>GrifballContext without Seasons' history table.</summary>
    private sealed class NoSeasonsContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var season = modelBuilder.Model.FindEntityType(typeof(Season))!;
            modelBuilder.Model.RemoveEntityType((IMutableEntityType)season.FindHistoryEntityType()!);
        }
    }
}
