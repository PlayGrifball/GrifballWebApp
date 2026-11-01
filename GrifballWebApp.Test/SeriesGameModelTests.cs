using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// Event.SeriesGames, the one table of a season match's games: each row is a Waypoint match or a recorded game, never
/// both or neither (a check constraint on both databases), and a game is in one series at most.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeriesGameModelTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<(SeasonMatchTestData.Seeded Seeded, Match Match, RecordedGame Recorded)> Seed()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        var recorded = new RecordedGame { SourceKey = "mcc/1", GameVersionID = 1 };
        _context.RecordedGames.Add(recorded);
        await _context.SaveChangesAsync();
        return (seeded, match, recorded);
    }

    [Test]
    public async Task AWaypointGame_AndARecordedGame_AreGamesOfOneSeries()
    {
        var (seeded, match, recorded) = await Seed();
        _context.SeriesGames.AddRange(
            new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID },
            new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2, RecordedGameID = recorded.RecordedGameID });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var series = await _context.SeasonMatches.Include(m => m.SeriesGames).ThenInclude(g => g.Match).Include(m => m.SeriesGames).ThenInclude(g => g.RecordedGame)
            .SingleAsync(m => m.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        var linkedMatch = await _context.Matches.Include(m => m.SeriesGame).SingleAsync(m => m.MatchID == match.MatchID);

        Assert.Multiple(() =>
        {
            Assert.That(series.SeriesGames.OrderBy(g => g.GameNumber).Select(g => (g.GameNumber, g.Match?.MatchID, g.RecordedGame?.SourceKey)),
                Is.EqualTo(new[] { (1, (Guid?)match.MatchID, (string?)null), (2, (Guid?)null, (string?)"mcc/1") }));
            Assert.That(linkedMatch.SeriesGame.SeasonMatchID, Is.EqualTo(seeded.SeasonMatch.SeasonMatchID));
        });
    }

    [Test]
    public async Task ARowWithBothGames_IsRefused()
    {
        var (seeded, match, recorded) = await Seed();
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID, RecordedGameID = recorded.RecordedGameID });

        await Assert.ThatAsync(() => _context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task ARowWithNeitherGame_IsRefused()
    {
        var (seeded, _, _) = await Seed();
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 });

        await Assert.ThatAsync(() => _context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task AGameInTwoSeries_OrTwoGame1s_AreRefused()
    {
        var (seeded, match, recorded) = await Seed();
        var other = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(other);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGameID = recorded.RecordedGameID });
        await _context.SaveChangesAsync();

        _context.SeriesGames.Add(new SeriesGame { SeasonMatch = other, GameNumber = 1, RecordedGameID = recorded.RecordedGameID });
        await Assert.ThatAsync(() => _context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>(), "One recorded game, two series");
        _context.ChangeTracker.Clear();

        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID });
        await Assert.ThatAsync(() => _context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>(), "Two game 1s in one series");
    }

    [Test]
    public async Task DeletingARecordedGame_TakesItOutOfItsSeries()
    {
        var (seeded, _, recorded) = await Seed();
        recorded.Teams.Add(new RecordedGameTeam { Side = 1, TeamID = seeded.Home.TeamID, Outcome = Outcomes.Won });
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGame = recorded });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _context.RecordedGames.Where(g => g.RecordedGameID == recorded.RecordedGameID).ExecuteDeleteAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.SeriesGames.AnyAsync(), Is.False, "The database cascades to the series game");
            Assert.That(await _context.RecordedGameTeams.AnyAsync(), Is.False);
            Assert.That(await _context.SeasonMatches.AnyAsync(m => m.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID), Is.True, "The series stays");
        });
    }

    [Test]
    public async Task TheMatchPage_LinksTheSeriesWaypointGames_InGameOrder()
    {
        var (seeded, match, recorded) = await Seed();
        _context.SeriesGames.AddRange(
            new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2, MatchID = match.MatchID },
            new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGameID = recorded.RecordedGameID });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var championships = new GrifballWebApp.Server.Championships.ChampionshipService(_context);
        var brackets = new GrifballWebApp.Server.Brackets.BracketService(_context, new GrifballWebApp.Server.TeamStandings.TeamStandingsService(_context), championships);
        var service = new GrifballWebApp.Server.SeasonMatchPage.SeasonMatchService(_context, NSubstitute.Substitute.For<GrifballWebApp.Server.Services.IDataPullService>(), brackets, championships);

        var page = (await service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!;

        Assert.That(page.ReportedGames.Select(g => (g.MatchNumber, g.MatchID)), Is.EqualTo(new[] { (2, match.MatchID) }), "A recorded game has no Waypoint page to link to");
    }
}
