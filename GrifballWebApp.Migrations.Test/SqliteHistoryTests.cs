using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// Row history on SQLite (SqliteHistory), on a migrated database, for what needs no migrations assembly: writing and
/// reading it, and queries. SQLite needs no server, so these run on every leg of the suite, each test on its own copy
/// of a file migrated once. The tests that scaffold a migration (SqliteHistoryMigrationsModelDiffer, from
/// GrifballWebApp.Migrations.Sqlite) are GrifballWebApp.Migrations.Test's SqliteHistoryMigrationsTests.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SqliteHistoryTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), $"grif-sqlite-history-{Environment.ProcessId}");
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

    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] expected = new[] { "Late League" };

    private static Season NewSeason(string name) => Season.Seed(seasonName: name).WithDates(publicAt: Now, signupsOpen: Now, signupsClose: Now.AddDays(7), draftStart: Now.AddDays(8), seasonStart: Now.AddDays(14), seasonEnd: Now.AddDays(60));

    private record Version(string SeasonName, DateTime PeriodStart, DateTime PeriodEnd);

    private Task<List<Version>> History(int seasonID) => _context.Database.SqlQueryRaw<Version>(
        "SELECT \"SeasonName\", \"PeriodStart\", \"PeriodEnd\" FROM \"SeasonsHistory\" WHERE \"SeasonID\" = {0} ORDER BY \"PeriodEnd\"", seasonID).ToListAsync();

    private DateTime PeriodStart(object entity) => (DateTime)_context.Entry(entity).Property(PostgresHistory.PeriodStart).CurrentValue!;

    // New files, which start in SQLite's default journal (delete): WAL is the connection setup's.
    [Test]
    public async Task EveryConnection_PutsItsFileInWalMode()
    {
        await using var fresh = NewContext(Path.Combine(Folder, $"{Guid.NewGuid():N}.db"));
        await fresh.Database.OpenConnectionAsync();
        await using var asyncCommand = fresh.Database.GetDbConnection().CreateCommand();
        asyncCommand.CommandText = "PRAGMA journal_mode";
        var journal = (string)(await asyncCommand.ExecuteScalarAsync())!;

        // A read-only connection is left alone: it can't change the journal (EF opens one to see whether a file exists)
        await using var readOnly = new GrifballContext(Options(Path.Combine(Folder, $"{Guid.NewGuid():N}.db")));
        await readOnly.Database.OpenConnectionAsync();
        await readOnly.Database.CloseConnectionAsync();
        await using var reader = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={readOnly.Database.GetDbConnection().DataSource};Mode=ReadOnly").Options);
        await reader.Database.OpenConnectionAsync();
        await using var readOnlyCommand = reader.Database.GetDbConnection().CreateCommand();
        readOnlyCommand.CommandText = "PRAGMA journal_mode";
        var readOnlyJournal = (string)(await readOnlyCommand.ExecuteScalarAsync())!;

        // The sync path too: a connection EF opens without await.
        await using var other = NewContext(Path.Combine(Folder, $"{Guid.NewGuid():N}.db"));
#pragma warning disable VSTHRD103 // The sync path is under test
        other.Database.OpenConnection();
        using var command = other.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA journal_mode";
        var syncJournal = (string)command.ExecuteScalar()!;
#pragma warning restore VSTHRD103

        using (Assert.EnterMultipleScope())
        {
            Assert.That(journal, Is.EqualTo("wal"));
            Assert.That(syncJournal, Is.EqualTo("wal"));
            Assert.That(readOnlyJournal, Is.EqualTo("wal"), "the file's, set by the connection before");
        }
    }

    [Test]
    public async Task AnUpdate_KeepsTheRowAsItWas_AndReadsBackItsNewPeriodStart()
    {
        var season = NewSeason("First");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var first = PeriodStart(season);
        await Task.Delay(5);

        season.Edited(seasonName: "Second");
        await _context.SaveChangesAsync();
        var second = PeriodStart(season);

        var stored = await _context.Seasons.AsNoTracking().Where(s => s.SeasonID == season.SeasonID)
            .Select(s => new { Start = EF.Property<DateTime>(s, PostgresHistory.PeriodStart), End = EF.Property<DateTime>(s, PostgresHistory.PeriodEnd) })
            .SingleAsync();
        var history = await History(season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.GreaterThan(first), "the trigger's stamp, read back after the update (no RETURNING)");
            Assert.That(stored.Start, Is.EqualTo(second));
            Assert.That(stored.End, Is.EqualTo(DateTime.MaxValue));
            Assert.That(history, Is.EqualTo(new[] { new Version("First", first, second) }));
        }
    }

    [Test]
    public async Task ADelete_KeepsTheLastVersion()
    {
        var season = NewSeason("Only");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var start = PeriodStart(season);

        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync();

        var history = await History(season.SeasonID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].SeasonName, Is.EqualTo("Only"));
            Assert.That(history[0].PeriodStart, Is.EqualTo(start));
            Assert.That(history[0].PeriodEnd, Is.GreaterThanOrEqualTo(start).And.LessThan(DateTime.MaxValue));
        }
    }

    [Test]
    public async Task Text_ComparesIgnoringCase()
    {
        _context.Seasons.Add(NewSeason("Late League"));
        await _context.SaveChangesAsync();

        Assert.That(await _context.Seasons.CountAsync(s => s.SeasonName == "LATE league"), Is.EqualTo(1));
    }

    // string.Contains too, which EF translates to instr(), blind to the collation (SqliteMethodCallTranslatorProviderWithContains)
    [Test]
    public async Task Contains_IgnoresCase_AndAnEmptySearchMatchesEverything()
    {
        _context.Seasons.AddRange(NewSeason("Late League"), NewSeason("Winter League"));
        await _context.SaveChangesAsync();
        var empty = "";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Seasons.Where(s => s.SeasonName.Contains("late")).Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(expected));
            Assert.That(await _context.Seasons.CountAsync(s => s.SeasonName.Contains("LEAGUE")), Is.EqualTo(2));
            Assert.That(await _context.Seasons.CountAsync(s => s.SeasonName.Contains(empty)), Is.EqualTo(2));
            Assert.That(await _context.Seasons.CountAsync(s => s.SeasonName.Contains("summer")), Is.Zero);
        }
    }
}
