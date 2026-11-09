using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// Manual match entry (issue #9): a commissioner enters a series game by hand as a recorded game, edits and deletes it,
/// and the series is scored from all its games.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchServiceManualGamesTests
{
    private GrifballContext _context;
    private SeasonMatchService _service;

    /// <summary>Halo 3 (seeded).</summary>
    private const int Halo3 = 1;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var championships = new GrifballWebApp.Server.Championships.ChampionshipService(_context);
        _service = new SeasonMatchService(_context, Substitute.For<IDataPullService>(),
            new BracketService(_context, new TeamStandingsService(_context), championships), championships);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<SeasonMatch> Reload(int id)
    {
        await using var ctx = _context.NewContextLike();
        return await ctx.SeasonMatches
            .Include(x => x.SeriesGames).ThenInclude(g => g.RecordedGame!.Teams)
            .Include(x => x.SeriesGames).ThenInclude(g => g.RecordedGame!.Participants)
            .AsNoTracking().SingleAsync(x => x.SeasonMatchID == id);
    }

    /// <summary>The series game a recorded game, or a Waypoint match, is.</summary>
    private async Task<int> SeriesGameOf(int recordedGameID)
        => await _context.SeriesGames.Where(g => g.RecordedGameID == recordedGameID).Select(g => g.SeriesGameID).SingleAsync();
    private async Task<int> SeriesGameOf(Guid matchID)
        => await _context.SeriesGames.Where(g => g.MatchID == matchID).Select(g => g.SeriesGameID).SingleAsync();

    private async Task<int[]> Players(Team team)
        => await _context.TeamPlayers.Where(tp => tp.TeamID == team.TeamID).OrderBy(tp => tp.UserID).Select(tp => tp.UserID).ToArrayAsync();

    private static ManualGameRequestDto Game(int home, int away, DateTime? playedAt = null, params ManualGamePlayerRequestDto[] players) => new()
    {
        GameVersionID = Halo3,
        Map = " Foundry ",
        Gametype = "Grifball",
        PlayedAt = playedAt,
        HomeTeamScore = home,
        AwayTeamScore = away,
        Players = players.ToList(),
    };

    [Test]
    public async Task AddManualGame_FirstGameOfBestOf3_RecordsTheGame_AndScoresWithoutDeciding()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var away = await Players(seeded.Away);
        _context.ChangeTracker.Clear();

        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3, null,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 10, Deaths = 4, Assists = 2, Score = 3 },
            new ManualGamePlayerRequestDto { UserID = away[0], IsHomeTeam = false, Kills = 4, Deaths = 10 }));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        var game = sm.SeriesGames.Single().RecordedGame!;
        Assert.Multiple(() =>
        {
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(sm.SeriesGames.Single().GameNumber, Is.EqualTo(1));
            Assert.That(game.RecordedGameID, Is.EqualTo(id));
            Assert.That(game.Origin, Is.EqualTo(RecordedGameOrigin.Manual));
            Assert.That(game.SeasonID, Is.Null, "It counts for its season through its series game only");
            Assert.That((game.GameVersionID, game.Map, game.Gametype, game.IsPlayoffs), Is.EqualTo((Halo3, "Foundry", "Grifball", false)));
            Assert.That(game.Teams.OrderBy(t => t.Side).Select(t => (t.Side, t.TeamID, t.Score, t.Outcome)),
                Is.EqualTo(new[] { (1, (int?)seeded.Home.TeamID, (int?)5, Outcomes.Won), (2, (int?)seeded.Away.TeamID, (int?)3, Outcomes.Lost) }));
            var homeLine = game.Participants.Single(p => p.UserID == home[0]);
            Assert.That((homeLine.Kills, homeLine.Deaths, homeLine.Assists, homeLine.Score, homeLine.Gamertag), Is.EqualTo((10, 4, 2, (int?)3, $"GT{SeasonMatchTestData.HomeXbox[0]}")));
            Assert.That(homeLine.RecordedGameTeamID, Is.EqualTo(game.Teams.Single(t => t.Side == 1).RecordedGameTeamID));
            Assert.That(game.Participants.Single(p => p.UserID == away[0]).RecordedGameTeamID, Is.EqualTo(game.Teams.Single(t => t.Side == 2).RecordedGameTeamID));
        });
    }

    [Test]
    public async Task AddManualGame_TheDecidingGame_DecidesTheMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        _context.ChangeTracker.Clear();

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(2, 5));
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(1, 5));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)0, (int?)2)));
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That(sm.SeriesGames.Select(g => g.GameNumber).Order(), Is.EqualTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public async Task ManualAndWaypointGames_AreOneSeries_NumberedByWhenTheyWerePlayed()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(1), SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, waypoint.MatchID);
        _context.ChangeTracker.Clear();
        // Played before the Waypoint game: it becomes game 1 and the Waypoint game moves to 2
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(1, 5, start));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.SeriesGames.Single(g => g.RecordedGameID == id).GameNumber, Is.EqualTo(1));
            Assert.That(sm.SeriesGames.Single(g => g.MatchID == waypoint.MatchID).GameNumber, Is.EqualTo(2));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult), Is.EqualTo(((int?)1, (int?)1, (SeasonMatchResult?)null)));
        });
    }

    [Test]
    public async Task EditManualGame_ChangesTheWinner_OfADecidedMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var home = await Players(seeded.Home);
        var away = await Players(seeded.Away);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 2, null,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 1 }));
        _context.ChangeTracker.Clear();

        await _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, id, Game(2, 5, null,
            new ManualGamePlayerRequestDto { UserID = away[1], IsHomeTeam = false, Kills = 7, Deaths = 1 }));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        var game = sm.SeriesGames.Single().RecordedGame!;
        Assert.Multiple(() =>
        {
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)0, (int?)1)));
            Assert.That(game.RecordedGameID, Is.EqualTo(id));
            Assert.That(game.Teams.Single(t => t.Side == 2).Outcome, Is.EqualTo(Outcomes.Won));
            Assert.That(game.Participants.Select(p => (p.UserID, p.Kills)), Is.EqualTo(new[] { (away[1], 7) }), "The players' lines are replaced");
        });
    }

    [Test]
    public async Task RemoveGame_EnteredByHand_DeletesIt_RenumbersTheRest_AndRescores()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        _context.ChangeTracker.Clear();
        var first = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 1, start,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true }));
        _context.ChangeTracker.Clear();
        var second = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(1, 5, start.AddHours(1)));
        _context.ChangeTracker.Clear();

        await _service.RemoveGame(seeded.SeasonMatch.SeasonMatchID, await SeriesGameOf(first));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.SeriesGames.Select(g => (g.RecordedGameID, g.GameNumber)), Is.EqualTo(new[] { ((int?)second, 1) }));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)0, (int?)1)));
            Assert.That(_context.RecordedGames.Any(g => g.RecordedGameID == first), Is.False);
            Assert.That(_context.RecordedGameParticipants.Any(p => p.RecordedGameID == first), Is.False);
            Assert.That(_context.RecordedGameTeams.Any(t => t.RecordedGameID == first), Is.False);
        });
    }

    [Test]
    public async Task RemoveGame_TheLastGame_LeavesNoScore_AndReopensTheMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 1));
        _context.ChangeTracker.Clear();

        await _service.RemoveGame(seeded.SeasonMatch.SeasonMatchID, await SeriesGameOf(id));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That((sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult, sm.AwayTeamResult, sm.SeriesGames.Count),
            Is.EqualTo(((int?)null, (int?)null, (SeasonMatchResult?)null, (SeasonMatchResult?)null, 0)));
    }

    [Test]
    public async Task UndoResult_DeletesTheGamesEnteredByHand()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, waypoint.MatchID);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 1));
        _context.ChangeTracker.Clear();

        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);

        Assert.Multiple(() =>
        {
            Assert.That(_context.RecordedGames.Any(g => g.RecordedGameID == id), Is.False);
            // The Waypoint match is only freed, to be reported again
            Assert.That(_context.Matches.Any(m => m.MatchID == waypoint.MatchID), Is.True);
            Assert.That(_context.SeriesGames.Any(), Is.False);
        });
    }

    [Test]
    public async Task AddManualGame_WithoutPlayers_OrAnyOptionalField()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        _context.ChangeTracker.Clear();

        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID,
            new ManualGameRequestDto { GameVersionID = Halo3, HomeTeamScore = 0, AwayTeamScore = 1, Players = null!, Map = "  " });

        var game = (await Reload(seeded.SeasonMatch.SeasonMatchID)).SeriesGames.Single().RecordedGame!;
        Assert.That((game.RecordedGameID, game.Map, game.Gametype, game.PlayedAt, game.Participants.Count), Is.EqualTo((id, (string?)null, (string?)null, (DateTime?)null, 0)));
    }

    [Test]
    public async Task AManualPlayer_WithoutAGamertag_IsRecordedByTheirDisplayName_NeverTheirLoginName()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var shown = User.Seed(userName: "login-one", displayName: "Shown Name");
        var unnamed = User.Seed(userName: "login-two");
        _context.TeamPlayers.AddRange(TeamPlayer.Seed(teamID: seeded.Home.TeamID, user: shown), TeamPlayer.Seed(teamID: seeded.Home.TeamID, user: unnamed));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 1, null,
            new ManualGamePlayerRequestDto { UserID = shown.Id, IsHomeTeam = true }, new ManualGamePlayerRequestDto { UserID = unnamed.Id, IsHomeTeam = true }));

        Assert.That(_context.RecordedGameParticipants.OrderBy(p => p.UserID).Select(p => p.Gamertag).ToList(), Is.EqualTo(new[] { "Shown Name", "" }),
            "A login name is private");
    }

    [Test]
    public async Task GetSeasonMatchPage_ShowsAGameEnteredByHand_InTheBoxScore_WithWhatItsEditStartsFrom()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var away = await Players(seeded.Away);
        var playedAt = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3, playedAt,
            new ManualGamePlayerRequestDto { UserID = away[0], IsHomeTeam = false, Kills = 2, Deaths = 3, Assists = 1, Goals = 1, Score = 4 },
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 6, Goals = 3 }));
        _context.ChangeTracker.Clear();

        var page = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);

        // The box score (#252) is the one place a game shows: a recorded one too, once
        var game = page!.ReportedGames.Single();
        Assert.Multiple(() =>
        {
            Assert.That(page.IsImported, Is.False);
            Assert.That((game.MatchID, game.RecordedGameID, game.MatchNumber, game.HomeScore, game.AwayScore, game.IsManual),
                Is.EqualTo(((Guid?)null, (int?)id, 1, (int?)5, (int?)3, true)));
            Assert.That((game.GameVersionID, game.GameVersionName, game.Map, game.Gametype, game.PlayedAt),
                Is.EqualTo(((int?)Halo3, (string?)"Halo 3", (string?)"Foundry", (string?)"Grifball", (DateTime?)playedAt)));
            Assert.That(game.HomePlayers.Select(p => (p.UserID, p.Kills, p.BombScores)), Is.EqualTo(new[] { ((int?)home[0], 6, 3) }));
            Assert.That(game.AwayPlayers.Select(p => (p.UserID, p.Kills, p.Deaths, p.Assists, p.BombScores, p.Score, p.Gamertag)),
                Is.EqualTo(new[] { ((int?)away[0], 2, 3, 1, 1, (int?)4, $"GT{SeasonMatchTestData.AwayXbox[0]}") }));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_AnImportedGame_IsNotManual_AndAWaypointGameHasNoRecordedDetails()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        _context.MarkImported(seeded.Season, "archive/Total Stats");
        // The import's sides can come in either order: the away team is side 1 here
        // An archive source named "manual": its key looks like a game entered by hand's, but Origin says what it is
        var game = RecordedGame.Seed("manual/game-1", Halo3, seeded.Season.SeasonID);
        game.SeedSide(1, seeded.Away.TeamID, Outcomes.Lost, 2);
        game.SeedSide(2, seeded.Home.TeamID, Outcomes.Won, 6);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.SeriesGames.AddRange(
            new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGame = game },
            new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2, MatchID = waypoint.MatchID });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var page = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);

        var games = page!.ReportedGames;
        Assert.Multiple(() =>
        {
            Assert.That(page.IsImported, Is.True);
            Assert.That(games.Select(g => (g.MatchNumber, g.HomeScore, g.AwayScore, g.IsManual, g.GameVersionName)), Is.EqualTo(new[]
            {
                (1, (int?)6, (int?)2, false, (string?)"Halo 3"),
                (2, (int?)5, (int?)3, false, (string?)null),
            }));
            Assert.That((games[1].GameVersionID, games[1].Map, games[1].Gametype, games[1].PlayedAt), Is.EqualTo(((int?)null, (string?)null, (string?)null, (DateTime?)null)));
        });
    }

    [Test]
    public async Task ManualGamePlayers_CountInTheSeasonStats()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3, null,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 9, Deaths = 2, Goals = 4 }));

        var line = await PlayerGameStatsQuery.Query(_context).Where(s => s.UserID == home[0]).SingleAsync();

        Assert.That((line.SeasonID, line.SeasonMatchID, line.TeamID, line.Won, line.Kills, line.Deaths, line.GameVersionID, line.Goals),
            Is.EqualTo((seeded.Season.SeasonID, (int?)seeded.SeasonMatch.SeasonMatchID, (int?)seeded.Home.TeamID, true, 9, 2, Halo3, (int?)4)));
    }

    [Test]
    public async Task RegeneratingTheSchedule_DeletesItsGamesEnteredByHand_SoTheyStopCounting()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3, null,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 9 }));
        _context.ChangeTracker.Clear();

        await new GrifballWebApp.Server.MatchPlanner.MatchPlannerService(_context).CreateSeasonMatches(seeded.Season.SeasonID, 1, 3);

        Assert.Multiple(() =>
        {
            Assert.That(_context.RecordedGames.Any(), Is.False);
            Assert.That(_context.RecordedGameTeams.Any(), Is.False);
            Assert.That(_context.RecordedGameParticipants.Any(), Is.False);
            Assert.That(PlayerGameStatsQuery.Query(_context).Any(), Is.False);
        });
    }

    [Test]
    public async Task AWaypointGame_WhoseRostersChangedSince_StillCounts_WhenAGameIsEnteredByHand()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, start, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, waypoint.MatchID);
        // A home player is traded to the away team: the Waypoint game's home side now has an away player
        var traded = await _context.TeamPlayers.FirstAsync(tp => tp.TeamID == seeded.Home.TeamID);
        _context.TeamPlayers.Remove(traded);
        _context.TeamPlayers.Add(TeamPlayer.Seed(teamID: seeded.Away.TeamID, userID: traded.UserID));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(1, 5, start.AddHours(1)));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That((sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult), Is.EqualTo(((int?)1, (int?)1, (SeasonMatchResult?)null)));
    }

    [Test]
    public async Task AWaypointGame_NoLongerTiedToEitherTeam_RefusesTheEdit_WithAReason()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, waypoint.MatchID);
        // Both rosters emptied: nothing ties the Waypoint game's sides to the teams any more
        _context.TeamPlayers.RemoveRange(await _context.TeamPlayers.Where(tp => tp.TeamID == seeded.Home.TeamID || tp.TeamID == seeded.Away.TeamID).ToListAsync());
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.That(await Refused(() => _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3))), Does.Contain("no longer matches the teams' rosters"));
    }

    [Test]
    public async Task ReportMatch_ReplacingAGameEnteredByHand_TakesItsPlace_AndDeletesIt()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 5);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        // The real record of game 1: the away team won it, not the home team as it was typed in
        var real = await SeasonMatchTestData.AddMatchAsync(_context, start, SeasonMatchTestData.HomeXbox, Outcomes.Lost, SeasonMatchTestData.AwayXbox, Outcomes.Won);
        _context.ChangeTracker.Clear();
        var typed = await _service.AddManualGame(smID, Game(5, 3) with { GameVersionID = PlayerGameStatsQuery.InfiniteGameVersionID });
        _context.ChangeTracker.Clear();
        var second = await _service.AddManualGame(smID, Game(5, 2));
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(smID, real.MatchID, CancellationToken.None, await SeriesGameOf(typed));

        var sm = await Reload(smID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.SeriesGames.OrderBy(g => g.GameNumber).Select(g => (g.MatchID, g.RecordedGameID, g.GameNumber)),
                Is.EqualTo(new[] { ((Guid?)real.MatchID, (int?)null, 1), ((Guid?)null, (int?)second, 2) }), "In game 1's place");
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)1, (int?)1)));
            Assert.That(_context.RecordedGames.Any(g => g.RecordedGameID == typed), Is.False, "Typed in, so nothing to go back to");
        });
    }

    [Test]
    public async Task ANewGame_IsNeverTakenForOneAlreadyReported_ByItsTime()
    {
        // Without being told it replaces a game, a Waypoint game at the time a game was typed in is a new game
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 5);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, start.AddMinutes(5), SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(smID, Game(5, 3, start) with { GameVersionID = PlayerGameStatsQuery.InfiniteGameVersionID });
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(smID, waypoint.MatchID);

        var sm = await Reload(smID);
        Assert.That((sm.SeriesGames.Count, sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo((2, (int?)2, (int?)0)));
    }

    [Test]
    public async Task ADecidedSeries_TakesAReplacement_ThatCanChangeItsWinner_ButNoNewGame()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        _context.ChangeTracker.Clear();
        var first = await _service.AddManualGame(smID, Game(5, 3));
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(smID, Game(1, 5));
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(smID, Game(5, 4));
        _context.ChangeTracker.Clear();
        var real = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Lost, SeasonMatchTestData.AwayXbox, Outcomes.Won);
        _context.ChangeTracker.Clear();

        Assert.That(await Refused(() => _service.AddManualGame(smID, Game(5, 3))), Does.StartWith("Results for this match have already been decided"));
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(smID, real.MatchID, CancellationToken.None, await SeriesGameOf(first));

        var sm = await Reload(smID);
        Assert.Multiple(() =>
        {
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)1, (int?)2)));
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That(sm.SeriesGames.Single(g => g.GameNumber == 1).MatchID, Is.EqualTo(real.MatchID));
        });
    }

    [Test]
    public async Task AddManualGame_ReplacingAWaypointGame_FreesItToBeReportedAgain()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(smID, waypoint.MatchID);
        _context.ChangeTracker.Clear();

        var typed = await _service.AddManualGame(smID, Game(2, 5) with { ReplacesSeriesGameID = await SeriesGameOf(waypoint.MatchID) });

        var sm = await Reload(smID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.SeriesGames.Select(g => (g.MatchID, g.RecordedGameID, g.GameNumber)), Is.EqualTo(new[] { ((Guid?)null, (int?)typed, 1) }));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)0, (int?)1)));
            Assert.That(_context.Matches.Any(m => m.MatchID == waypoint.MatchID), Is.True, "The Waypoint game stays, to be reported again");
        });
    }

    [Test]
    public async Task RemoveGame_AWaypointGame_MidSeries_UnreportsIt_SoItCanBeReportedAgain()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 5);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        var first = await SeasonMatchTestData.AddMatchAsync(_context, start, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        var second = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(1), SeasonMatchTestData.HomeXbox, Outcomes.Lost, SeasonMatchTestData.AwayXbox, Outcomes.Won);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(smID, first.MatchID);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(smID, second.MatchID);
        _context.ChangeTracker.Clear();

        await _service.RemoveGame(smID, await SeriesGameOf(first.MatchID));
        _context.ChangeTracker.Clear();

        var sm = await Reload(smID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.SeriesGames.Select(g => (g.MatchID, g.GameNumber)), Is.EqualTo(new[] { ((Guid?)second.MatchID, 1) }));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult), Is.EqualTo(((int?)0, (int?)1, (SeasonMatchResult?)null)));
        });
        await _service.ReportMatch(smID, first.MatchID);
        Assert.That((await Reload(smID)).SeriesGames, Has.Count.EqualTo(2), "Reported again");
    }

    // ---------- What is refused ----------

    [Test]
    public async Task EditManualGame_OfAGameWithTwoLinesForOnePlayer_LeavesThemOne()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(smID, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 1 }));
        // A second line for the same user, as a merge of two of their accounts that both played it left the game
        var game = await _context.RecordedGames.Include(g => g.Teams).SingleAsync(g => g.RecordedGameID == id);
        game.SeedPlayer(new RecordedPlayerLine(home[0], 1, "alt", 9, 9, 9), game.Teams.Single(t => t.Side == 1));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.EditManualGame(smID, id, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 6 }));

        _context.ChangeTracker.Clear();
        Assert.That(await _context.RecordedGameParticipants.Where(p => p.RecordedGameID == id).Select(p => new { p.UserID, p.Kills }).ToListAsync(),
            Is.EqualTo(new[] { new { UserID = home[0], Kills = 6 } }), "The edit says what the game is: one line for the player, counted once");
    }

    [Test]
    public async Task APlayer_IsRefused_OnTheOtherTeamsSide_OrOffBothRosters_UnlessAlreadyOnTheGame()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var away = await Players(seeded.Away);
        var sub = User.Seed(userName: "sub");
        var other = User.Seed(userName: "other");
        _context.Users.AddRange(sub, other);
        await _context.SaveChangesAsync();
        var smID = seeded.SeasonMatch.SeasonMatchID;
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(smID, Game(5, 3));
        // A sub already on the game (one who left a roster, say)
        var game = await _context.RecordedGames.Include(g => g.Teams).SingleAsync(g => g.RecordedGameID == id);
        game.SeedPlayer(new RecordedPlayerLine(sub.Id, 1, "sub", 0, 0, 0), game.Teams.Single(t => t.Side == 1));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused(() => _service.AddManualGame(smID, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = false }))),
                Is.EqualTo($"GT{SeasonMatchTestData.HomeXbox[0]} is on Home Team's roster: they can't play for Away Team"));
            Assert.That(await Refused(() => _service.AddManualGame(smID, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = away[0], IsHomeTeam = true }))),
                Is.EqualTo($"GT{SeasonMatchTestData.AwayXbox[0]} is on Away Team's roster: they can't play for Home Team"));
            Assert.That(await Refused(() => _service.AddManualGame(smID, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = sub.Id, IsHomeTeam = true }))),
                Is.EqualTo("sub is on neither team's roster"));
            Assert.That(await Refused(() => _service.EditManualGame(smID, id, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = other.Id, IsHomeTeam = true }))),
                Is.EqualTo("other is on neither team's roster"));
        });

        _context.ChangeTracker.Clear();
        await _service.EditManualGame(smID, id, Game(5, 3, null, new ManualGamePlayerRequestDto { UserID = sub.Id, IsHomeTeam = false, Kills = 4 }));

        Assert.That(_context.RecordedGameParticipants.Single(p => p.RecordedGameID == id).UserID, Is.EqualTo(sub.Id));
    }

    private async Task<string> Refused(Func<Task> action)
        => (await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(async () => await action()))!.Message;

    [Test]
    public async Task AddManualGame_RefusesWhatItCannotTake()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var id = seeded.SeasonMatch.SeasonMatchID;
        _context.ChangeTracker.Clear();
        var player = new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true };

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused(() => _service.AddManualGame(99999, Game(5, 3))), Is.EqualTo("Season match does not exist"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(3, 3))), Does.Contain("needs a winner"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(-1, 3))), Does.Contain("can't be negative"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3) with { GameVersionID = 999 })), Is.EqualTo("Unknown game version"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3) with { Map = new string('m', 101) })), Does.Contain("at most 100"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player, player))), Does.Contain("only be entered once"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player with { Kills = -1 }))), Does.Contain("can't be negative"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player with { Deaths = -1 }))), Does.Contain("can't be negative"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player with { Assists = -1 }))), Does.Contain("can't be negative"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player with { Goals = -1 }))), Does.Contain("goals can't be negative"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, -3))), Does.Contain("can't be negative"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3) with { Gametype = new string('g', 101) })), Does.Contain("at most 100"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player with { UserID = 999999 }))), Does.Contain("not a user"));
        });
        Assert.That(await _context.RecordedGames.AnyAsync(), Is.False, "Nothing was saved");
    }

    [Test]
    public async Task AddManualGame_IsRefused_ForAMatchMissingATeam_AnImportedSeason_ADecidedMatch_OrAFullSeries()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var tbd = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, null, bestOf: 1);
        _context.SeasonMatches.Add(tbd);
        // An imported season's match (its teams are another season's here: the season is refused before they matter)
        var importedSeason = Season.Seed(seasonName: "Archive Season", historicalSourceID: "archive/Total Stats").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        _context.Seasons.Add(importedSeason);
        await _context.SaveChangesAsync();
        var imported = SeasonMatch.Seed(importedSeason.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID, bestOf: 1);
        var forfeited = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID, bestOf: 3).Forfeited(Forfeiter.Home);
        var empty = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID, bestOf: 0);
        _context.SeasonMatches.AddRange(imported, forfeited, empty);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused(() => _service.AddManualGame(tbd.SeasonMatchID, Game(5, 3))), Does.Contain("both teams"));
            Assert.That(await Refused(() => _service.AddManualGame(imported.SeasonMatchID, Game(5, 3))), Does.Contain("historical archive"));
            Assert.That(await Refused(() => _service.AddManualGame(forfeited.SeasonMatchID, Game(5, 3))), Does.StartWith("Results for this match have already been decided"));
            Assert.That(await Refused(() => _service.AddManualGame(empty.SeasonMatchID, Game(5, 3))), Does.Contain("already has all 0"));
        });
    }

    [Test]
    public async Task Edit_OnlyTakesAGameEnteredByHand_AndRemoveOrReplace_OnlyThisMatchsGames()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var imported = RecordedGame.Seed("archive/game-1", Halo3, seeded.Season.SeasonID);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGame = imported });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var id = seeded.SeasonMatch.SeasonMatchID;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused(() => _service.EditManualGame(id, imported.RecordedGameID, Game(5, 3))), Does.Contain("entered by hand"));
            Assert.That(await Refused(() => _service.RemoveGame(id, 999999)), Does.Contain("not one of this match's games"));
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3) with { ReplacesSeriesGameID = 999999 })), Does.Contain("not one of this match's games"));
        });
    }

    [Test]
    public async Task EditManualGame_IsRefused_OnceTheMatchWasForfeited()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3));
        _context.ChangeTracker.Clear();
        await _service.AwayForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();

        Assert.That(await Refused(() => _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, id, Game(3, 5))), Does.Contain("forfeit"));
    }

    [Test]
    public async Task EditManualGame_ThatKeepsTheWinner_OfAForfeitedMatch_OnlyRewritesTheGame()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3));
        _context.ChangeTracker.Clear();
        await _service.AwayForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();

        // A typo fixed: the home team still won the game, so the forfeit stays as it was
        await _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, id, Game(6, 3, null,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 8 }));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        var game = sm.SeriesGames.Single().RecordedGame!;
        Assert.Multiple(() =>
        {
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Forfeit)));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)1, (int?)0)));
            Assert.That(game.Teams.Single(t => t.Side == 1).Score, Is.EqualTo(6));
            Assert.That(game.Participants.Select(p => (p.UserID, p.Kills)), Is.EqualTo(new[] { (home[0], 8) }));
        });
    }

    [Test]
    public async Task UndoResult_OfAForfeit_KeepsTheGamesPlayedBeforeIt()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3));
        _context.ChangeTracker.Clear();
        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();

        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)1, (int?)0)), "The series goes on from its games");
            Assert.That(sm.SeriesGames.Select(g => (g.RecordedGameID, g.GameNumber)), Is.EqualTo(new[] { ((int?)id, 1) }));
        });
    }

    [Test]
    public async Task AGameEnteredWithoutATime_KeepsItsPlace_WhenAWaypointGameIsReportedAfterIt()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var waypoint = await SeasonMatchTestData.AddMatchAsync(_context, new DateTime(2026, 1, 10, 21, 0, 0, DateTimeKind.Utc),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        // The stat bot missed game 1: entered by hand, without a time
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(1, 5));
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, waypoint.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.SeriesGames.Single(g => g.RecordedGameID == id).GameNumber, Is.EqualTo(1));
            Assert.That(sm.SeriesGames.Single(g => g.MatchID == waypoint.MatchID).GameNumber, Is.EqualTo(2));
            Assert.That((sm.HomeTeamScore, sm.AwayTeamScore), Is.EqualTo(((int?)1, (int?)1)));
        });
    }

    [Test]
    public async Task EditManualGame_KeepsTheGamesPlace_UnlessGivenANewTime()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 5);
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        _context.ChangeTracker.Clear();
        var first = await _service.AddManualGame(smID, Game(5, 1, start));
        _context.ChangeTracker.Clear();
        var second = await _service.AddManualGame(smID, Game(1, 5, start.AddHours(1)));
        _context.ChangeTracker.Clear();

        // Its time taken off: it stays game 2
        await _service.EditManualGame(smID, second, Game(1, 5));
        _context.ChangeTracker.Clear();
        var kept = (await Reload(smID)).SeriesGames.OrderBy(g => g.GameNumber).Select(g => g.RecordedGameID).ToArray();
        // Game 1 given a time after game 2's: it moves behind it
        await _service.EditManualGame(smID, first, Game(5, 1, start.AddHours(2)));
        _context.ChangeTracker.Clear();
        var moved = (await Reload(smID)).SeriesGames.OrderBy(g => g.GameNumber).Select(g => g.RecordedGameID).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(kept, Is.EqualTo(new int?[] { first, second }));
            Assert.That(moved, Is.EqualTo(new int?[] { second, first }));
        });
    }

    /// <summary>Another commissioner's game saved first: the series' unique game numbers refuse this save.</summary>
    private sealed class AnotherAddGotInFirst : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<SeriesGame>().Any(e => e.State == EntityState.Added))
                throw new DbUpdateException("Cannot insert duplicate key row in object 'Event.SeriesGames' with unique index 'IX_SeriesGames_SeasonMatchID_GameNumber'");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Test]
    public async Task AddManualGame_BeatenToItsGameNumber_ByAnotherAdd_IsRefused_NotAServerError()
    {
        var context = await SetUpFixture.NewGrifballContext(new AnotherAddGotInFirst());
        try
        {
            var seeded = await SeasonMatchTestData.SeedAsync(context, bestOf: 3);
            context.ChangeTracker.Clear();
            var championships = new GrifballWebApp.Server.Championships.ChampionshipService(context);
            var service = new SeasonMatchService(context, Substitute.For<IDataPullService>(),
                new BracketService(context, new TeamStandingsService(context), championships), championships);

            var message = await Refused(() => service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3)));

            Assert.Multiple(() =>
            {
                Assert.That(message, Is.EqualTo(SeasonMatchService.SeriesChangedMessage));
                Assert.That(context.NewContextLike().RecordedGames.Any(), Is.False, "Nothing was saved");
            });
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
