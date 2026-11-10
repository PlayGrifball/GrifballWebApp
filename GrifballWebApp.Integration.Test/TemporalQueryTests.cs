using System.Collections.Concurrent;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Extensions.Caching.Memory;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// EF's temporal queries (TemporalAsOf, TemporalAll, TemporalFromTo, TemporalBetween,
/// TemporalContainedIn), the same calls and the same results on both databases: SQL Server's FOR
/// SYSTEM_TIME, Postgres's row history (PostgresTemporalQueries). The databases stamp the periods
/// with their own clocks, so each test reads them back between saves and asks about those times.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TemporalQueryTests
{
    private static readonly string[] expected1 = ["First", "Second", "Third"];
    private static readonly string[] expected2 = ["First", "Second"];
    private static readonly string[] expected3 = ["Second", "Third"];
    private static readonly string[] expected4 = ["Second"];
    private static readonly string[] expected5 = ["First", "Second"];
    private static readonly string[] expected6 = ["Second"];
    private static readonly string[] expected7 = ["First", "Second", "Third"];
    private static readonly string[] expected8 = ["Before", "Between", "After"];
    private static readonly string[] expected9 = ["Before", "After"];
    private static readonly int[] expected10 = [3];
    private static readonly int[] expected11 = [5, 7];
    private static readonly string[] expected12 = ["First", "First"];
    private static readonly string[] expected13 = ["First", "Third"];
    private static readonly string[] expected14 = ["First", "Second"];
    private static readonly string[] expected15 = ["First", "First", "Second", "Second", "Third"];
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
    }

    // Not after SetUp ignored the test (SQLite), which made no database
    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    private const string PeriodStart = "PeriodStart";
    private const string PeriodEnd = "PeriodEnd";

    // Postgres keeps microseconds, SQL Server 100ns: a period read back from one is exact, the other's
    // within a microsecond of a time the test computes.
    private static readonly TimeSpan Microsecond = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond);
    private static readonly string[] expected = new[] { "First" };
    private static readonly string[] expectedArray = new[] { "First" };
    private static readonly string[] expectedArray0 = new[] { "Second" };
    private static readonly string[] expectedArray1 = new[] { "Third" };
    private static readonly string[] expectedArray2 = new[] { "First", "Second", "Third" };
    private static readonly string[] expectedArray3 = new[] { "Second", "Third" };
    private static readonly string[] expectedArray4 = new[] { "Third", "Second", "First" };
    private static readonly string[] expectedArray5 = new[] { "Third" };

    private static Season NewSeason(string name) => Season.Seed(seasonName: name).WithDates(publicAt: new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), signupsOpen: new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), signupsClose: new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc), draftStart: new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc), seasonStart: new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2026, 12, 10, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>The row's current PeriodStart, as the database stamped it.</summary>
    private Task<DateTime> PeriodStartOf(Season season)
    {
        return _context.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => EF.Property<DateTime>(s, PeriodStart)).SingleAsync();
    }

    /// <summary>Saves, and the time the database gave the change, a little apart from the next.</summary>
    private async Task<DateTime> Save(Season season)
    {
        await _context.SaveChangesAsync();
        var time = await PeriodStartOf(season);
        await Task.Delay(20);
        return time;
    }

    private record Version(string Name, DateTime Start, DateTime End);

    /// <summary>A season named First, then Second, then Third: the times each took effect.</summary>
    private async Task<(Season Season, DateTime First, DateTime Second, DateTime Third)> ThreeVersions()
    {
        var season = NewSeason("First");
        _context.Seasons.Add(season);
        var first = await Save(season);
        season.Edited(seasonName: "Second");
        var second = await Save(season);
        season.Edited(seasonName: "Third");
        var third = await Save(season);
        return (season, first, second, third);
    }

    private static DateTime Between(DateTime a, DateTime b) => a + (b - a) / 2;

    private static Task<List<Version>> Versions(IQueryable<Season> query, int seasonID)
    {
        return query.Where(s => s.SeasonID == seasonID)
            .OrderBy(s => EF.Property<DateTime>(s, PeriodStart))
            .Select(s => new Version(s.SeasonName, EF.Property<DateTime>(s, PeriodStart), EF.Property<DateTime>(s, PeriodEnd)))
            .ToListAsync();
    }

    private static string[] Names(List<Version> versions) => [.. versions.Select(v => v.Name)];

    [Test]
    public async Task AsOf_FindsTheVersionInEffectThen()
    {
        var (season, first, second, third) = await ThreeVersions();

        async Task<string[]> At(DateTime time) => Names(await Versions(_context.Seasons.TemporalAsOf(time), season.SeasonID));

        var before = await At(first - TimeSpan.FromMilliseconds(5));
        var atFirst = await At(first);
        var afterFirst = await At(Between(first, second));
        var justBeforeSecond = await At(second - TimeSpan.FromTicks(1));
        var atSecond = await At(second);
        var atThird = await At(third);
        var now = await At(DateTime.UtcNow.AddMinutes(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.Empty);
            Assert.That(atFirst, Is.EqualTo(expected));
            Assert.That(afterFirst, Is.EqualTo(expected));
            Assert.That(justBeforeSecond, Is.EqualTo(expectedArray));
            Assert.That(atSecond, Is.EqualTo(expectedArray0));
            Assert.That(atThird, Is.EqualTo(expectedArray1));
            Assert.That(now, Is.EqualTo(expectedArray1));
        }
    }

    [Test]
    public async Task AsOf_HasThePeriodsOfTheVersion_TheCurrentOneEndingAtTheEndOfTime()
    {
        var (season, first, second, third) = await ThreeVersions();

        var old = (await Versions(_context.Seasons.TemporalAsOf(Between(first, second)), season.SeasonID)).Single();
        var current = (await Versions(_context.Seasons.TemporalAsOf(DateTime.UtcNow.AddMinutes(1)), season.SeasonID)).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(old.Start, Is.EqualTo(first).Within(Microsecond));
            Assert.That(old.End, Is.EqualTo(second).Within(Microsecond));
            Assert.That(current.Start, Is.EqualTo(third).Within(Microsecond));
            // SQL Server's 9999-12-31 23:59:59.9999999; Postgres's infinity.
            Assert.That(current.End, Is.EqualTo(DateTime.MaxValue));
            Assert.That(old.Start.Kind, Is.EqualTo(current.End.Kind));
        }
    }

    [Test]
    public async Task All_HasEveryVersion_OrderedAndFilteredByThePeriods()
    {
        var (season, first, second, third) = await ThreeVersions();

        var all = await Versions(_context.Seasons.TemporalAll(), season.SeasonID);
        var since = Names(await Versions(_context.Seasons.TemporalAll().Where(s => EF.Property<DateTime>(s, PeriodStart) > Between(first, second)), season.SeasonID));
        var newestFirst = await _context.Seasons.TemporalAll().Where(s => s.SeasonID == season.SeasonID)
            .OrderByDescending(s => EF.Property<DateTime>(s, PeriodEnd)).Select(s => s.SeasonName).ToListAsync();
        var ended = await _context.Seasons.TemporalAll().CountAsync(s => s.SeasonID == season.SeasonID && EF.Property<DateTime>(s, PeriodEnd) < DateTime.MaxValue);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Names(all), Is.EqualTo(expectedArray2));
            Assert.That(all.Select(v => v.Start), Is.EqualTo(new[] { first, second, third }).Using<DateTime>((a, b) => (a - b).Duration() < Microsecond));
            Assert.That(all.Select(v => v.End).Take(2), Is.EqualTo(new[] { second, third }).Using<DateTime>((a, b) => (a - b).Duration() < Microsecond));
            Assert.That(all[2].End, Is.EqualTo(DateTime.MaxValue));
            Assert.That(since, Is.EqualTo(expectedArray3));
            Assert.That(newestFirst, Is.EqualTo(expectedArray4));
            Assert.That(ended, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task ATemporalQuery_IsNeverAPlainOneCachedBefore()
    {
        // EF makes a temporal query no-tracking (TemporalAll().AsNoTracking()), and its cache found the plain no-tracking
        // query of the same shape compiled before it, and ran that: the current row, no history (TemporalQueryCacheKeys).
        // Each temporal query here compiles after its plain no-tracking twin.
        var (season, first, _, _) = await ThreeVersions();

        var current = Names(await Versions(_context.Seasons.AsNoTracking(), season.SeasonID));
        var all = Names(await Versions(_context.Seasons.TemporalAll(), season.SeasonID));
        var plainAt = await _context.Seasons.AsNoTracking().Where(s => s.SeasonID == season.SeasonID).Select(s => s.SeasonName).SingleAsync();
        var asOf = await _context.Seasons.TemporalAsOf(first).Where(s => s.SeasonID == season.SeasonID).Select(s => s.SeasonName).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(current, Is.EqualTo(expectedArray5));
            Assert.That(all, Is.EqualTo(expectedArray2));
            Assert.That(plainAt, Is.EqualTo("Third"));
            Assert.That(asOf, Is.EqualTo("First"));
        }
    }

    [Test]
    public async Task ATableWithoutHistory_RefusesItsTemporalQuery_EvenAfterAPlainOne()
    {
        // The plain no-tracking query cached first, the shape EF gives the temporal one; that is still refused, not run as it
        await _context.Outbox.AsNoTracking().ToListAsync();

        await Assert.CatchAsync(() => _context.Outbox.TemporalAll().ToListAsync());
    }

    [Test]
    public async Task All_HasADeletedRowsVersions()
    {
        var (season, _, _, _) = await ThreeVersions();
        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync();

        var current = await _context.Seasons.CountAsync(s => s.SeasonID == season.SeasonID);
        var all = Names(await Versions(_context.Seasons.TemporalAll(), season.SeasonID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(current, Is.Zero);
            Assert.That(all, Is.EqualTo(expected1));
        }
    }

    [Test]
    public async Task FromTo_Between_ContainedIn_TakeTheirBoundsAsSqlServerDoes()
    {
        var (season, first, second, third) = await ThreeVersions();
        var tick = TimeSpan.FromTicks(1);

        async Task<string[]> Of(IQueryable<Season> query) => Names(await Versions(query, season.SeasonID));
        var seasons = _context.Seasons;

        // FROM a TO b: PeriodStart < b AND PeriodEnd > a.
        var fromTo = await Of(seasons.TemporalFromTo(second, third));
        var fromToJustPast = await Of(seasons.TemporalFromTo(second, third + tick));
        var fromToInside = await Of(seasons.TemporalFromTo(Between(first, second), Between(second, third)));
        // BETWEEN a AND b: PeriodStart <= b AND PeriodEnd > a.
        var between = await Of(seasons.TemporalBetween(second, third));
        var betweenJustBefore = await Of(seasons.TemporalBetween(second, third - tick));
        // CONTAINED IN (a, b): PeriodStart >= a AND PeriodEnd <= b.
        var containedIn = await Of(seasons.TemporalContainedIn(first, third));
        var containedInJustPast = await Of(seasons.TemporalContainedIn(first + tick, third));
        var containedInForever = await Of(seasons.TemporalContainedIn(DateTime.MinValue, DateTime.MaxValue));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fromTo, Is.EqualTo(expectedArray0));
            Assert.That(fromToJustPast, Is.EqualTo(expectedArray3));
            Assert.That(fromToInside, Is.EqualTo(expected2));
            Assert.That(between, Is.EqualTo(expected3));
            Assert.That(betweenJustBefore, Is.EqualTo(expected4));
            Assert.That(containedIn, Is.EqualTo(expected5));
            Assert.That(containedInJustPast, Is.EqualTo(expected6));
            Assert.That(containedInForever, Is.EqualTo(expected7));
        }
    }

    [Test]
    public async Task Temporal_LeavesOutVersionsThatLastedNoTime()
    {
        var season = NewSeason("Before");
        _context.Seasons.Add(season);
        await Save(season);

        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
        {
            // SQLite has no transaction time: each change is stamped when its statement runs (SqliteHistory), so a
            // version a transaction made and replaced is kept unless it lasted under a millisecond. A version that
            // lasted no time, written here, is still left out.
            await _context.StartTransactionAsync();
            season.Edited(seasonName: "Between");
            await _context.SaveChangesAsync();
            await Task.Delay(5);
            season.Edited(seasonName: "After");
            await _context.SaveChangesAsync();
            await _context.CommitTransactionAsync();
            await _context.Database.ExecuteSqlRawAsync("""
                INSERT INTO "SeasonsHistory" ("SeasonID", "SeasonName", "PeriodStart", "PeriodEnd", "PublicAt", "SignupsOpen", "SignupsClose",
                    "DraftStart", "SeasonStart", "SeasonEnd", "CaptainsLocked", "CreatedAt", "ModifiedAt")
                SELECT "SeasonID", 'No time', '2026-01-01 00:00:00', '2026-01-01 00:00:00', "PublicAt", "SignupsOpen", "SignupsClose",
                    "DraftStart", "SeasonStart", "SeasonEnd", "CaptainsLocked", "CreatedAt", "ModifiedAt"
                FROM "Seasons" WHERE "SeasonID" = {0}
                """, season.SeasonID);

            var kept = Names(await Versions(_context.Seasons.TemporalAll(), season.SeasonID));
            Assert.That(kept, Is.EqualTo(expected8));
            return;
        }

        // Two changes in one transaction: the version between them lasted no time.
        await _context.StartTransactionAsync();
        season.Edited(seasonName: "Between");
        await _context.SaveChangesAsync();
        season.Edited(seasonName: "After");
        await _context.SaveChangesAsync();
        await _context.CommitTransactionAsync();

        var all = Names(await Versions(_context.Seasons.TemporalAll(), season.SeasonID));
        Assert.That(all, Is.EqualTo(expected9));
    }

    [Test]
    public async Task PeriodEnd_IsTheEndOfTime_InOrdinaryQueries()
    {
        var season = NewSeason("Current");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var end = await _context.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => EF.Property<DateTime>(s, PeriodEnd)).SingleAsync();
        var current = await _context.Seasons.CountAsync(s => EF.Property<DateTime>(s, PeriodEnd) == DateTime.MaxValue);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(end, Is.EqualTo(DateTime.MaxValue));
            Assert.That(current, Is.EqualTo(1));
        }
    }

    /// <summary>A season and one of its matches, then both changed, then another match added.</summary>
    private async Task<(Season Season, DateTime Before)> SeasonWithMatches()
    {
        var season = NewSeason("Old name");
        var match = SeasonMatch.Seed(season, null, null, bestOf: 3);
        _context.SeasonMatches.Add(match);
        var created = await Save(season);

        season.Edited(seasonName: "New name");
        // A column no method changes, changed for the history
        _context.Entry(match).Property(m => m.BestOf).CurrentValue = 5;
        var changed = await Save(season);

        _context.SeasonMatches.Add(SeasonMatch.Seed(season, null, null, bestOf: 7));
        await Save(season);

        return (season, Between(created, changed));
    }

    [Test]
    public async Task AsOf_IncludesAndNavigationsAreAsOfTheSameTime()
    {
        var (season, before) = await SeasonWithMatches();

        var matches = await _context.SeasonMatches.TemporalAsOf(before).Include(m => m.Season).Where(m => m.SeasonID == season.SeasonID).ToListAsync();
        var seasons = await _context.Seasons.TemporalAsOf(before).Include(s => s.SeasonMatches).Where(s => s.SeasonID == season.SeasonID).ToListAsync();
        var byOldName = await _context.SeasonMatches.TemporalAsOf(before).CountAsync(m => m.Season.SeasonName == "Old name");
        var byNewName = await _context.SeasonMatches.TemporalAsOf(before).CountAsync(m => m.Season.SeasonName == "New name");
        var projected = await _context.SeasonMatches.TemporalAsOf(before).Select(m => new { m.BestOf, m.Season.SeasonName }).SingleAsync();
        var now = await _context.Seasons.TemporalAsOf(DateTime.UtcNow.AddMinutes(1)).Include(s => s.SeasonMatches).SingleAsync(s => s.SeasonID == season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches.Select(m => (m.BestOf, m.Season.SeasonName)), Is.EqualTo(new[] { (3, "Old name") }));
            Assert.That(seasons.Single().SeasonName, Is.EqualTo("Old name"));
            Assert.That(seasons.Single().SeasonMatches.Select(m => m.BestOf), Is.EqualTo(expected10));
            Assert.That(byOldName, Is.EqualTo(1));
            Assert.That(byNewName, Is.Zero);
            Assert.That(projected, Is.EqualTo(new { BestOf = 3, SeasonName = "Old name" }));
            Assert.That(now.SeasonName, Is.EqualTo("New name"));
            Assert.That(now.SeasonMatches.Select(m => m.BestOf).Order(), Is.EqualTo(expected11));
        }
    }

    [Test]
    public async Task Concat_OfTwoAsOfQueries_AtTheSameTime()
    {
        var (season, first, second, _) = await ThreeVersions();
        var at = Between(first, second);

        var both = await _context.Seasons.TemporalAsOf(at).Where(s => s.SeasonID == season.SeasonID)
            .Concat(_context.Seasons.TemporalAsOf(at).Where(s => s.SeasonID == season.SeasonID))
            .Select(s => s.SeasonName).ToListAsync();

        Assert.That(both, Is.EqualTo(expected12));
    }

    [Test]
    public async Task Navigations_UnderAnythingButAsOf_AreRefused()
    {
        var all = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Seasons.TemporalAll().Include(s => s.SeasonMatches).ToListAsync());
        var between = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _context.SeasonMatches.TemporalBetween(DateTime.MinValue, DateTime.MaxValue).Select(m => m.Season.SeasonName).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all!.Message, Is.EqualTo(SqlServerStrings.TemporalNavigationExpansionOnlySupportedForAsOf("AsOf")));
            Assert.That(between!.Message, Is.EqualTo(SqlServerStrings.TemporalNavigationExpansionOnlySupportedForAsOf("AsOf")));
        }
    }

    [Test]
    public async Task ANavigation_ToATableWithHistory_IsAsOfTheSameTime()
    {
        // A match's reschedule requests keep a history too (#297): every table a temporal one leads to has one
        var s = await RescheduleTestData.SeedAsync(_context);
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        var before = DateTime.UtcNow.AddMinutes(-5);
        var now = DateTime.UtcNow.AddMinutes(5);

        var then = await _context.SeasonMatches.TemporalAsOf(before).Include(m => m.Reschedules).ToListAsync();
        var current = await _context.SeasonMatches.TemporalAsOf(now).Include(m => m.Reschedules).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(then, Is.Empty);
            Assert.That(current.Reschedules, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task ATableWithoutHistory_HasNoTemporalQueries()
    {
        // The outbox keeps no history. SQL Server refuses the query (FOR SYSTEM_TIME on a table that isn't
        // temporal), Postgres and SQLite before sending it.
        await Assert.CatchAsync(() => _context.Outbox.TemporalAll().ToListAsync());
    }

    [Test]
    public async Task SetOperations_OfDifferentTimes_AreRefused()
    {
        var concat = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Seasons.TemporalAsOf(DateTime.UtcNow)
            .Concat(_context.Seasons.TemporalAsOf(DateTime.UtcNow.AddDays(-1))).ToListAsync());
        var withCurrent = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Seasons.TemporalAll()
            .Union(_context.Seasons).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(concat!.Message, Is.EqualTo(SqlServerStrings.TemporalSetOperationOnMismatchedSources(nameof(Season))));
            Assert.That(withCurrent!.Message, Is.EqualTo(SqlServerStrings.TemporalSetOperationOnMismatchedSources(nameof(Season))));
        }
    }

    [Test]
    public async Task SetOperations_OfTheSameOperation_AreAllowed()
    {
        var (season, first, second, third) = await ThreeVersions();

        var all = await _context.Seasons.TemporalAll().Where(s => s.SeasonName == "First")
            .Union(_context.Seasons.TemporalAll().Where(s => s.SeasonName == "Third")).Where(s => s.SeasonID == season.SeasonID)
            .Select(s => s.SeasonName).OrderBy(n => n).ToListAsync();
        var range = await _context.Seasons.TemporalBetween(first, second).Where(s => s.SeasonName == "First")
            .Concat(_context.Seasons.TemporalBetween(first, second).Where(s => s.SeasonName == "Second")).Where(s => s.SeasonID == season.SeasonID)
            .Select(s => s.SeasonName).OrderBy(n => n).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Is.EqualTo(expected13));
            Assert.That(range, Is.EqualTo(expected14));
        }
    }

    // EF compiles a query once and caches it, the values it captures its parameters. On Postgres the
    // times of temporal queries are parameters too (PostgresTemporalQueries): an AsOf is the same query at
    // any time. SQL Server's FOR SYSTEM_TIME has its time as a literal, a query of its own for each.
    [Test]
    public async Task AsOf_AtAnyTime_IsCompiledOnce_OnPostgres()
    {
        var (season, first, second, third) = await ThreeVersions();
        var compiled = 0;
        // A query cache of its own: the shared one is compacted under the other tests running in parallel, which
        // recompiled the query now and then (2 or 3 compilations seen in CI)
        var options = TestDatabase.Options(_context.Database.GetConnectionString()!)
            .UseMemoryCache(new MemoryCache(new MemoryCacheOptions()))
            .LogTo(_ => Interlocked.Increment(ref compiled), [CoreEventId.QueryCompilationStarting]).Options;

        var names = new List<string>();
        foreach (var time in new[] { first, Between(first, second), second, Between(second, third), third })
        {
            await using var context = new GrifballContext(options);
            names.Add(await context.Seasons.TemporalAsOf(time).Where(s => s.SeasonID == season.SeasonID).Select(s => s.SeasonName).SingleAsync());
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Is.EqualTo(expected15));
            // Postgres and SQLite take the times out into parameters (PostgresTemporalQueryCompiler); SQL Server compiles each
            Assert.That(compiled, Is.EqualTo(TestDatabase.Provider == DatabaseProvider.SqlServer ? 5 : 1));
        }
    }

    // Each execution of a cached query has its own parameters: AsOf queries at different times, at once,
    // each find their own time's version.
    [Test]
    public async Task AsOf_AtDifferentTimesAtOnce_EachFindsItsOwnTimesVersion()
    {
        var (season, first, second, third) = await ThreeVersions();
        (DateTime Time, string Name)[] cases = [(Between(first, second), "First"), (Between(second, third), "Second"), (third, "Third")];
        var options = TestDatabase.Options(_context.Database.GetConnectionString()!).Options;

        var wrong = new ConcurrentBag<string>();
        var queries = 0;
        await Task.WhenAll(Enumerable.Range(0, 12).Select(worker => Task.Run(async () =>
        {
            for (var i = 0; i < 20; i++)
            {
                var (time, expected) = cases[(worker + i) % cases.Length];
                await using var context = new GrifballContext(options);
                var name = await context.Seasons.TemporalAsOf(time).Where(s => s.SeasonID == season.SeasonID).Select(s => s.SeasonName).SingleAsync();
                Interlocked.Increment(ref queries);
                if (name != expected)
                    wrong.Add($"{time:O}: {name}, not {expected}");
            }
        })));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queries, Is.EqualTo(240));
            Assert.That(wrong, Is.Empty);
        }
    }

    // EF.CompileQuery's parameters are its arguments alone: there the root keeps its time.
    [Test]
    public async Task AsOf_InACompiledQuery()
    {
        var (season, first, second, _) = await ThreeVersions();
        var at = Between(first, second);
        var query = EF.CompileAsyncQuery((GrifballContext context, int id) =>
            context.Seasons.TemporalAsOf(at).Where(s => s.SeasonID == id).Select(s => s.SeasonName).Single());

        Assert.That(await query(_context, season.SeasonID), Is.EqualTo("First"));
    }
}
