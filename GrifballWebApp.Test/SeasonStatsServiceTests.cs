using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Leaderboards;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// Season stats come from one place, the season's games where it has them, its stored totals otherwise: a season run
/// in the app has stats on every page with no import involved, and no player's phase of a season counts twice.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonStatsServiceTests
{
    private GrifballContext _context;
    private SeasonStatsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SeasonStatsService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>A season run in the app: its match reported through SeasonMatchService, as the match page does.</summary>
    private async Task<SeasonMatchTestData.Seeded> PlayALiveSeason()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddMinutes(-20),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context));
        await new SeasonMatchService(_context, Substitute.For<IDataPullService>(), bracketService).ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);
        _context.ChangeTracker.Clear();
        return seeded;
    }

    [Test]
    public async Task ALiveSeason_HasStatsOnItsPages_WithNoImportInvolved()
    {
        var seeded = await PlayALiveSeason();
        var seasons = new SeasonService(_context);

        var stats = await seasons.GetSeasonPlayerStats(seeded.Season.SeasonID);
        var summary = (await seasons.GetSeasonSummary(seeded.Season.SeasonID))!;
        var team = (await new TeamController(_context).Team(seeded.Home.TeamID, CancellationToken.None))!;
        var boards = await new LeaderboardsController(_context).GetLeaderboards(CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.PlayerSeasonStats.AnyAsync(), Is.False, "Nothing stored: the games are the stats");
            Assert.That(stats, Has.Count.EqualTo(8));
            Assert.That(stats.Select(s => s.Source), Is.All.EqualTo("Matches"));
            var top = stats[0];
            Assert.That((top.Gamertag, top.Goals, top.GamesPlayed, top.Wins, top.TeamID, top.TeamName), Is.EqualTo(("GT104", 4, 1, 1, (int?)seeded.Home.TeamID, "Home Team")));
            Assert.That(stats.Where(s => s.TeamID == seeded.Away.TeamID).Select(s => (s.Wins, s.Losses)), Is.All.EqualTo((0, 1)));
            Assert.That(summary.PlayerStatCount, Is.EqualTo(8));
            Assert.That(team.Stats.Select(s => s.Gamertag), Is.EquivalentTo(new[] { "GT101", "GT102", "GT103", "GT104" }));
            Assert.That(boards.GoalLeaders.Select(g => g.Gamertag).First(), Is.EqualTo("GT104"));
            Assert.That(boards.KillLeaders, Has.Count.EqualTo(8));
        });
    }

    [Test]
    public async Task APlayersPhaseWithGames_IsNeverAlsoCountedFromStoredTotals()
    {
        var seeded = await PlayALiveSeason();
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 101);
        _context.PlayerSeasonStats.AddRange(
            // The same phase as the games: not counted
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 6, Gamertag = "Old", Goals = 99, GamesPlayed = 9 },
            // The playoffs, which have no games: counted
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 6, Gamertag = "Old", Goals = 7, GamesPlayed = 2, IsPlayoffs = true });
        await _context.SaveChangesAsync();

        var lines = (await _service.GetAsync(userID: user.Id)).Select(l => (l.IsPlayoffs, l.FromMatches, l.Goals, l.GamesPlayed));

        Assert.That(lines, Is.EquivalentTo(new[] { (false, true, 1, 1), (true, false, 7, 2) }));
    }

    [Test]
    public async Task APlayerWithoutAnAccount_AndOldWaypointTotals_AreLeftOut()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        // 301 is an Xbox user nobody's account is linked to
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [101, 301], Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.MatchLinks.Add(new MatchLink { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 102);
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 6, Gamertag = "x", Source = StatSource.Waypoint, IsPlayoffs = true });
        await _context.SaveChangesAsync();

        var lines = await _service.GetAsync(seasonID: seeded.Season.SeasonID);

        Assert.That(lines.Select(l => l.Gamertag), Is.EquivalentTo(new[] { "GT101", "GT201", "GT202", "GT203", "GT204" }));
    }
}
