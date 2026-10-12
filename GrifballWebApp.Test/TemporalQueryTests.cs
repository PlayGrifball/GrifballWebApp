using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Internal;

namespace GrifballWebApp.Test;

/// <summary>
/// EF's temporal queries (TemporalAsOf, TemporalAll, TemporalFromTo, TemporalBetween,
/// TemporalContainedIn), the same calls and the same results on both databases: SQL Server's FOR
/// SYSTEM_TIME, Postgres's row history (PostgresTemporalQueries). The databases stamp the periods
/// with their own clocks, so each test reads them back between saves and asks about those times.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TemporalQueryTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private const string PeriodStart = "PeriodStart";
    private const string PeriodEnd = "PeriodEnd";

    // Postgres keeps microseconds, SQL Server 100ns: a period read back from one is exact, the other's
    // within a microsecond of a time the test computes.
    private static readonly TimeSpan Microsecond = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond);

    private static Season NewSeason(string name) => new()
    {
        SeasonName = name,
        PublicAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
        SignupsOpen = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
        SignupsClose = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc),
        DraftStart = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc),
        SeasonStart = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc),
        SeasonEnd = new DateTime(2026, 12, 10, 12, 0, 0, DateTimeKind.Utc),
    };

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
        season.SeasonName = "Second";
        var second = await Save(season);
        season.SeasonName = "Third";
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
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Empty);
            Assert.That(atFirst, Is.EqualTo(new[] { "First" }));
            Assert.That(afterFirst, Is.EqualTo(new[] { "First" }));
            Assert.That(justBeforeSecond, Is.EqualTo(new[] { "First" }));
            Assert.That(atSecond, Is.EqualTo(new[] { "Second" }));
            Assert.That(atThird, Is.EqualTo(new[] { "Third" }));
            Assert.That(now, Is.EqualTo(new[] { "Third" }));
        });
    }

    [Test]
    public async Task AsOf_HasThePeriodsOfTheVersion_TheCurrentOneEndingAtTheEndOfTime()
    {
        var (season, first, second, third) = await ThreeVersions();

        var old = (await Versions(_context.Seasons.TemporalAsOf(Between(first, second)), season.SeasonID)).Single();
        var current = (await Versions(_context.Seasons.TemporalAsOf(DateTime.UtcNow.AddMinutes(1)), season.SeasonID)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(old.Start, Is.EqualTo(first).Within(Microsecond));
            Assert.That(old.End, Is.EqualTo(second).Within(Microsecond));
            Assert.That(current.Start, Is.EqualTo(third).Within(Microsecond));
            // SQL Server's 9999-12-31 23:59:59.9999999; Postgres's infinity.
            Assert.That(current.End, Is.EqualTo(DateTime.MaxValue));
            Assert.That(old.Start.Kind, Is.EqualTo(current.End.Kind));
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(Names(all), Is.EqualTo(new[] { "First", "Second", "Third" }));
            Assert.That(all.Select(v => v.Start), Is.EqualTo(new[] { first, second, third }).Using<DateTime>((a, b) => (a - b).Duration() < Microsecond));
            Assert.That(all.Select(v => v.End).Take(2), Is.EqualTo(new[] { second, third }).Using<DateTime>((a, b) => (a - b).Duration() < Microsecond));
            Assert.That(all[2].End, Is.EqualTo(DateTime.MaxValue));
            Assert.That(since, Is.EqualTo(new[] { "Second", "Third" }));
            Assert.That(newestFirst, Is.EqualTo(new[] { "Third", "Second", "First" }));
            Assert.That(ended, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task All_HasADeletedRowsVersions()
    {
        var (season, _, _, _) = await ThreeVersions();
        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync();

        var current = await _context.Seasons.CountAsync(s => s.SeasonID == season.SeasonID);
        var all = Names(await Versions(_context.Seasons.TemporalAll(), season.SeasonID));
        Assert.Multiple(() =>
        {
            Assert.That(current, Is.Zero);
            Assert.That(all, Is.EqualTo(new[] { "First", "Second", "Third" }));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(fromTo, Is.EqualTo(new[] { "Second" }));
            Assert.That(fromToJustPast, Is.EqualTo(new[] { "Second", "Third" }));
            Assert.That(fromToInside, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(between, Is.EqualTo(new[] { "Second", "Third" }));
            Assert.That(betweenJustBefore, Is.EqualTo(new[] { "Second" }));
            Assert.That(containedIn, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(containedInJustPast, Is.EqualTo(new[] { "Second" }));
            Assert.That(containedInForever, Is.EqualTo(new[] { "First", "Second", "Third" }));
        });
    }

    [Test]
    public async Task Temporal_LeavesOutVersionsThatLastedNoTime()
    {
        var season = NewSeason("Before");
        _context.Seasons.Add(season);
        await Save(season);

        // Two changes in one transaction. SQL Server stamps both with the transaction's start, so the
        // version between them lasted no time. Postgres's history is stamped by the app at each
        // SaveChanges (RowHistoryInterceptor), and its WITHOUT OVERLAPS constraint takes no empty
        // range, so every version it keeps lasted some time: that one too.
        await _context.StartTransactionAsync();
        season.SeasonName = "Between";
        await _context.SaveChangesAsync();
        season.SeasonName = "After";
        await _context.SaveChangesAsync();
        await _context.CommitTransactionAsync();

        var all = Names(await Versions(_context.Seasons.TemporalAll(), season.SeasonID));
        Assert.That(all, Is.EqualTo(TestDatabase.Provider == DatabaseProvider.Postgres
            ? new[] { "Before", "Between", "After" }
            : new[] { "Before", "After" }));
    }

    [Test]
    public async Task PeriodEnd_IsTheEndOfTime_InOrdinaryQueries()
    {
        var season = NewSeason("Current");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var end = await _context.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => EF.Property<DateTime>(s, PeriodEnd)).SingleAsync();
        var current = await _context.Seasons.CountAsync(s => EF.Property<DateTime>(s, PeriodEnd) == DateTime.MaxValue);
        Assert.Multiple(() =>
        {
            Assert.That(end, Is.EqualTo(DateTime.MaxValue));
            Assert.That(current, Is.EqualTo(1));
        });
    }

    /// <summary>A season and one of its matches, then both changed, then another match added.</summary>
    private async Task<(Season Season, DateTime Before)> SeasonWithMatches()
    {
        var season = NewSeason("Old name");
        var match = new SeasonMatch { Season = season, BestOf = 3 };
        _context.SeasonMatches.Add(match);
        var created = await Save(season);

        season.SeasonName = "New name";
        match.BestOf = 5;
        var changed = await Save(season);

        _context.SeasonMatches.Add(new SeasonMatch { Season = season, BestOf = 7 });
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

        Assert.Multiple(() =>
        {
            Assert.That(matches.Select(m => (m.BestOf, m.Season.SeasonName)), Is.EqualTo(new[] { (3, "Old name") }));
            Assert.That(seasons.Single().SeasonName, Is.EqualTo("Old name"));
            Assert.That(seasons.Single().SeasonMatches.Select(m => m.BestOf), Is.EqualTo(new[] { 3 }));
            Assert.That(byOldName, Is.EqualTo(1));
            Assert.That(byNewName, Is.Zero);
            Assert.That(projected, Is.EqualTo(new { BestOf = 3, SeasonName = "Old name" }));
            Assert.That(now.SeasonName, Is.EqualTo("New name"));
            Assert.That(now.SeasonMatches.Select(m => m.BestOf).Order(), Is.EqualTo(new[] { 5, 7 }));
        });
    }

    [Test]
    public async Task Concat_OfTwoAsOfQueries_AtTheSameTime()
    {
        var (season, first, second, _) = await ThreeVersions();
        var at = Between(first, second);

        var both = await _context.Seasons.TemporalAsOf(at).Where(s => s.SeasonID == season.SeasonID)
            .Concat(_context.Seasons.TemporalAsOf(at).Where(s => s.SeasonID == season.SeasonID))
            .Select(s => s.SeasonName).ToListAsync();

        Assert.That(both, Is.EqualTo(new[] { "First", "First" }));
    }

    [Test]
    public void Navigations_UnderAnythingButAsOf_AreRefused()
    {
        var all = Assert.ThrowsAsync<InvalidOperationException>(() => _context.Seasons.TemporalAll().Include(s => s.SeasonMatches).ToListAsync());
        var between = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _context.SeasonMatches.TemporalBetween(DateTime.MinValue, DateTime.MaxValue).Select(m => m.Season.SeasonName).ToListAsync());

        Assert.Multiple(() =>
        {
            Assert.That(all!.Message, Is.EqualTo(SqlServerStrings.TemporalNavigationExpansionOnlySupportedForAsOf("AsOf")));
            Assert.That(between!.Message, Is.EqualTo(SqlServerStrings.TemporalNavigationExpansionOnlySupportedForAsOf("AsOf")));
        });
    }

    [Test]
    public void Navigations_ToATableWithoutHistory_AreRefused()
    {
        var include = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _context.SeasonMatches.TemporalAsOf(DateTime.UtcNow).Include(m => m.ActiveRescheduleRequest).ToListAsync());

        Assert.That(include!.Message, Is.EqualTo(SqlServerStrings.TemporalNavigationExpansionBetweenTemporalAndNonTemporal(nameof(MatchReschedule))));
    }

    [Test]
    public void ATableWithoutHistory_HasNoTemporalQueries()
    {
        // SQL Server refuses the query (FOR SYSTEM_TIME on a table that isn't temporal), Postgres before
        // sending it.
        Assert.CatchAsync(() => _context.MatchReschedules.TemporalAll().ToListAsync());
    }

    [Test]
    public void SetOperations_OfDifferentTimes_AreRefused()
    {
        var concat = Assert.ThrowsAsync<InvalidOperationException>(() => _context.Seasons.TemporalAsOf(DateTime.UtcNow)
            .Concat(_context.Seasons.TemporalAsOf(DateTime.UtcNow.AddDays(-1))).ToListAsync());
        var withCurrent = Assert.ThrowsAsync<InvalidOperationException>(() => _context.Seasons.TemporalAll()
            .Union(_context.Seasons).ToListAsync());

        Assert.Multiple(() =>
        {
            Assert.That(concat!.Message, Is.EqualTo(SqlServerStrings.TemporalSetOperationOnMismatchedSources(nameof(Season))));
            Assert.That(withCurrent!.Message, Is.EqualTo(SqlServerStrings.TemporalSetOperationOnMismatchedSources(nameof(Season))));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(all, Is.EqualTo(new[] { "First", "Third" }));
            Assert.That(range, Is.EqualTo(new[] { "First", "Second" }));
        });
    }
}
