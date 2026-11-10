using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Integration.Test;

/// <summary>What the database holds a season to, and how its parts are read (#323).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheDatabase_RefusesASourceLinkOnAnAppSeason()
    {
        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        await using var ctx = _context.NewContextLike();
        var entry = ctx.Seasons.Add(Season.Organized("Spring 2026", League.DefaultLeagueID, null));
        entry.Property(s => s.HistoricalSourceUrl).CurrentValue = "https://sheets/x";

        await Assert.ThatAsync(() => ctx.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task ImportedFromArchive_TheQuery_AgreesWithIsImported()
    {
        _context.Seasons.AddRange(
            Season.Imported("wb/Stats", "Summer League 2018", League.DefaultLeagueID, null, null, "https://sheets/wb", SeasonFormat.LeagueSeason),
            Season.Organized("Spring 2026", League.DefaultLeagueID, null));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var byQuery = await _context.Seasons.AsNoTracking().Where(Season.ImportedFromArchive).Select(s => s.SeasonName).ToListAsync();
        var loaded = await _context.Seasons.AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(byQuery, Is.EqualTo(new[] { "Summer League 2018" }));
            Assert.That(loaded.Where(s => s.IsImported).Select(s => s.SeasonName), Is.EquivalentTo(byQuery), "The bool and the query are one rule");
            Assert.That(loaded.Count(s => !s.IsImported), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnImportedSeason_KeepsItsSourceLink()
    {
        _context.Seasons.Add(Season.Imported("wb/Stats", "Summer League 2018", League.DefaultLeagueID, null, null, "https://sheets/wb", SeasonFormat.LeagueSeason));
        await _context.SaveChangesAsync();

        Assert.That(await _context.Seasons.Select(s => s.HistoricalSourceUrl).SingleAsync(), Is.EqualTo("https://sheets/wb"));
    }

    [Test]
    public async Task ATitleAndATimeslot_AreSavedAndRead_ThroughTheSeason()
    {
        var season = await _context.AddSeason("Season 9");
        var team = await _context.AddTeam(season, "A", await _context.AddUser("cap"), 1);
        var option = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));
        _context.Availability.Add(option);
        season.SetAvailability([option]);
        season.RecordChampions(team, [new ChampionMember(team.TeamPlayers.Single().UserID, "Cap", true)]);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.Seasons.WithChampionships().SingleAsync(s => s.SeasonID == season.SeasonID);
        Assert.Multiple(async () =>
        {
            Assert.That(loaded.PlayerChampionships.Select(c => (c.TeamID, c.Tier)), Is.EqualTo(new[] { (team.TeamID, Season.DefaultChampionTier) }));
            Assert.That(await _context.Seasons.Championships().Select(c => c.SeasonID).ToListAsync(), Is.EqualTo(new[] { season.SeasonID }));
            Assert.That(await _context.Seasons.Timeslots().Select(t => t.AvailabilityOptionID).ToListAsync(), Is.EqualTo(new[] { option.AvailabilityOptionID }));
            Assert.That(_context.ChangeTracker.Entries<PlayerChampionship>().Count(), Is.EqualTo(1), "Only the season's own, loaded with it, are tracked: the reads are not");
        });
    }

    [Test]
    public async Task ATitleTakenAway_IsDeleted()
    {
        var season = await _context.AddSeason("Season 9");
        var team = await _context.AddTeam(season, "A", await _context.AddUser("cap"), 1);
        season.RecordChampions(team, [new ChampionMember(team.TeamPlayers.Single().UserID, "Cap", true)]);
        await _context.SaveChangesAsync();

        season.RecordChampions(null!, []);
        await _context.SaveChangesAsync();

        Assert.That(await _context.Seasons.Championships().AnyAsync(), Is.False);
    }
}
