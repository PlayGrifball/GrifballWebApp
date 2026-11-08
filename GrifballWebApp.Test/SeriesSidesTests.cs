using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.MatchDetails;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// The one rule for a Waypoint game's home and away sides (<see cref="SeriesSides"/>), over the one read of the rosters
/// (<see cref="SeriesRosters"/>): the box score, the match details page and scoring the series decide alike.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeriesSidesTests
{
    private GrifballContext _context;
    private SeasonMatchService _seasonMatches;
    private MatchDetailsService _matchDetails;

    /// <summary>Halo 3 (seeded).</summary>
    private const int Halo3 = 1;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var championships = new ChampionshipService(_context);
        _seasonMatches = new SeasonMatchService(_context, Substitute.For<IDataPullService>(),
            new BracketService(_context, new TeamStandingsService(_context), championships), championships);
        _matchDetails = new MatchDetailsService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void Of_PutsTheTeamLeaningMoreHome_Home_WhicheverComesFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SeriesSides.Of(new[] { "a", "b" }, t => t == "a" ? 2 : -4), Is.EqualTo(("a", "b")));
            Assert.That(SeriesSides.Of(new[] { "a", "b" }, t => t == "a" ? -1 : 3), Is.EqualTo(("b", "a")));
            // Both lean home: the one leaning less is still the other side
            Assert.That(SeriesSides.Of(new[] { "a", "b" }, t => t == "a" ? 1 : 2), Is.EqualTo(("b", "a")));
        });
    }

    [Test]
    public void Of_CantDecide_ATieOrAGameNotOfTwoTeams()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SeriesSides.Of(new[] { "a", "b" }, _ => 0), Is.Null, "Nobody rostered");
            Assert.That(SeriesSides.Of(new[] { "a", "b" }, _ => -1), Is.Null, "Both lean away alike");
            Assert.That(SeriesSides.Of(new[] { "a" }, _ => 4), Is.Null);
            Assert.That(SeriesSides.Of(new[] { "a", "b", "c" }, t => t == "a" ? 4 : 0), Is.Null);
        });
    }

    [Test]
    public void Lean_IsHomePlayersLessAwayPlayers()
    {
        var rosters = new SeriesRosters(new HashSet<long> { 1, 2, 3 }, new HashSet<long> { 7, 8 });

        Assert.That(rosters.Lean([1, 2, 7, 99]), Is.EqualTo(1), "Two home, one away, a ringer on neither");
    }

    [Test]
    public async Task Rosters_AreTheTeamsPlayersWithAnXboxAccount_AndNoneForNoTeams()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);

        var rosters = await SeriesRosters.LoadAsync(_context, seeded.SeasonMatch.SeasonMatchID);
        var noMatch = await SeriesRosters.LoadAsync(_context, -1);
        var noTeams = await SeriesRosters.LoadAsync(_context, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(rosters.Home, Is.EquivalentTo(SeasonMatchTestData.HomeXbox));
            Assert.That(rosters.Away, Is.EquivalentTo(SeasonMatchTestData.AwayXbox));
            Assert.That(noMatch, Is.SameAs(SeriesRosters.None), "No such match");
            Assert.That(noTeams, Is.SameAs(SeriesRosters.None), "Teams not decided");
        });
    }

    /// <summary>A Waypoint game of the series, reported when its players matched the rosters exactly: the away team was
    /// in-game team 0 and lost, the home team in-game team 1 and won.</summary>
    private async Task<(SeasonMatchTestData.Seeded Seeded, Match Match)> SeedReportedGame()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow,
            SeasonMatchTestData.AwayXbox, Outcomes.Lost, SeasonMatchTestData.HomeXbox, Outcomes.Won, team0Score: 3, team1Score: 5);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID });
        await _context.SaveChangesAsync();
        return (seeded, match);
    }

    /// <summary>Moves the players with these Xbox accounts onto a team's roster, or off every roster (null).</summary>
    private async Task MoveAsync(Team? to, params long[] xboxUserIDs)
    {
        var moving = await _context.Users.Where(u => u.XboxUserID != null && xboxUserIDs.Contains(u.XboxUserID.Value)).Select(u => u.Id).ToListAsync();
        foreach (var team in await _context.Teams.WithRoster().Where(t => t.TeamPlayers.Any(tp => moving.Contains(tp.UserID))).ToListAsync())
        {
            foreach (var userID in moving)
                team.RemovePlayer(userID);
        }
        await _context.SaveChangesAsync();
        if (to is not null)
            await _context.AddPlayers(await _context.Teams.WithRoster().SingleAsync(t => t.TeamID == to.TeamID), [.. moving]);
        _context.ChangeTracker.Clear();
    }

    private static ManualGameRequestDto HomeWin() => new() { GameVersionID = Halo3, HomeTeamScore = 5, AwayTeamScore = 3 };

    [Test]
    public async Task ATradedPlayer_LeavesTheBoxScoreTheMatchPageAndTheSeriesScoreAgreeing()
    {
        var (seeded, match) = await SeedReportedGame();
        // Since the game, a home player has been traded to the away team: the home side is still the one leaning home
        await MoveAsync(seeded.Away, SeasonMatchTestData.HomeXbox[3]);

        var game = (await _seasonMatches.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!.ReportedGames.Single();
        var details = (await _matchDetails.GetAsync(match.MatchID))!;
        // Scoring the series again (a game entered by hand) scores the Waypoint game for the side it was
        await _seasonMatches.AddManualGame(seeded.SeasonMatch.SeasonMatchID, HomeWin());
        var scored = await _context.SeasonMatches.AsNoTracking().SingleAsync(sm => sm.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);

        Assert.Multiple(() =>
        {
            Assert.That((game.HomeScore, game.AwayScore), Is.EqualTo(((int?)5, (int?)3)), "Box score");
            Assert.That(game.HomePlayers.Select(p => p.Gamertag), Is.EquivalentTo(SeasonMatchTestData.HomeXbox.Select(x => $"GT{x}")));
            Assert.That(details.Teams.Single(t => t.TeamID == 1).SeasonTeamID, Is.EqualTo(seeded.Home.TeamID), "Match page");
            Assert.That(details.Teams.Single(t => t.TeamID == 0).SeasonTeamID, Is.EqualTo(seeded.Away.TeamID));
            Assert.That((scored.HomeTeamScore, scored.AwayTeamScore, scored.HomeTeamResult), Is.EqualTo(((int?)2, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Won)), "Series");
        });
    }

    [Test]
    public async Task AGameThatCantBeDecidedAnyMore_IsRefusedByScoring_AndLeftUnnamedOnTheMatchPage()
    {
        var (seeded, match) = await SeedReportedGame();
        // Since the game, both rosters have been released: neither team leans either way
        await MoveAsync(null, [.. SeasonMatchTestData.HomeXbox, .. SeasonMatchTestData.AwayXbox]);

        var game = (await _seasonMatches.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!.ReportedGames.Single();
        var details = (await _matchDetails.GetAsync(match.MatchID))!;
        var refusal = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _seasonMatches.AddManualGame(seeded.SeasonMatch.SeasonMatchID, HomeWin()));

        Assert.Multiple(() =>
        {
            Assert.That(refusal!.Message, Does.Contain("no longer matches the teams' rosters"));
            Assert.That(details.Teams.All(t => t.SeasonTeamID == null), Is.True);
            // The box score still lays the game out, by in-game team
            Assert.That((game.HomeScore, game.AwayScore), Is.EqualTo(((int?)3, (int?)5)));
        });
    }
}
