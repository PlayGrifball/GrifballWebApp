using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
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
            Assert.That(game.SourceKey, Does.StartWith(SeasonMatchService.ManualSourceKeyPrefix));
            Assert.That(game.SeasonID, Is.EqualTo(seeded.Season.SeasonID));
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
    public async Task DeleteManualGame_RemovesIt_RenumbersTheRest_AndRescores()
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

        await _service.DeleteManualGame(seeded.SeasonMatch.SeasonMatchID, first);

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
    public async Task DeleteManualGame_TheLastGame_LeavesNoScore_AndReopensTheMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 1));
        _context.ChangeTracker.Clear();

        await _service.DeleteManualGame(seeded.SeasonMatch.SeasonMatchID, id);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That((sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult, sm.AwayTeamResult, sm.SeriesGames.Count),
            Is.EqualTo(((int?)null, (int?)null, (SeasonMatchResult?)null, (SeasonMatchResult?)null, 0)));
    }

    [Test]
    public async Task UndoResult_DeletesTheGamesEnteredByHand()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 1));
        _context.ChangeTracker.Clear();

        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(await _context.RecordedGames.AnyAsync(g => g.RecordedGameID == id), Is.False);
    }

    [Test]
    public async Task GetSeasonMatchPage_ListsTheRecordedGames()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        var away = await Players(seeded.Away);
        var playedAt = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        _context.ChangeTracker.Clear();
        var id = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3, playedAt,
            new ManualGamePlayerRequestDto { UserID = away[0], IsHomeTeam = false, Kills = 2, Deaths = 3, Assists = 1, Score = 4 },
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 6 }));
        _context.ChangeTracker.Clear();

        var page = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);

        var game = page!.RecordedGames.Single();
        Assert.Multiple(() =>
        {
            Assert.That(page.IsImported, Is.False);
            Assert.That(page.ReportedGames, Is.Empty);
            Assert.That((game.RecordedGameID, game.GameNumber, game.GameVersionID, game.GameVersionName, game.Map, game.Gametype, game.PlayedAt),
                Is.EqualTo((id, 1, Halo3, "Halo 3", (string?)"Foundry", (string?)"Grifball", (DateTime?)playedAt)));
            Assert.That((game.HomeTeamScore, game.AwayTeamScore, game.HomeTeamWon, game.AwayTeamWon, game.IsManual), Is.EqualTo(((int?)5, (int?)3, true, false, true)));
            // The home side's players first
            Assert.That(game.Players.Select(p => (p.UserID, p.IsHomeTeam, p.Kills)), Is.EqualTo(new[] { (home[0], true, 6), (away[0], false, 2) }));
            Assert.That((game.Players[1].Deaths, game.Players[1].Assists, game.Players[1].Score, game.Players[1].Gamertag), Is.EqualTo((3, 1, (int?)4, $"GT{SeasonMatchTestData.AwayXbox[0]}")));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_AnImportedGame_IsNotManual_AndFindsItsSidesByTeam()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        seeded.Season.HistoricalSourceID = "archive/Total Stats";
        // The import's sides can come in either order: the away team is side 1 here
        var game = new RecordedGame
        {
            SourceKey = "archive/game-1", GameVersionID = Halo3, SeasonID = seeded.Season.SeasonID,
            Teams =
            {
                new RecordedGameTeam { Side = 1, TeamID = seeded.Away.TeamID, Score = 2, Outcome = Outcomes.Lost },
                new RecordedGameTeam { Side = 2, TeamID = seeded.Home.TeamID, Score = 6, Outcome = Outcomes.Won },
            },
        };
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGame = game });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var page = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);

        var dto = page!.RecordedGames.Single();
        Assert.Multiple(() =>
        {
            Assert.That(page.IsImported, Is.True);
            Assert.That((dto.HomeTeamScore, dto.AwayTeamScore, dto.HomeTeamWon, dto.IsManual), Is.EqualTo(((int?)6, (int?)2, true, false)));
        });
    }

    [Test]
    public async Task ManualGamePlayers_CountInTheSeasonStats()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = await Players(seeded.Home);
        _context.ChangeTracker.Clear();
        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Game(5, 3, null,
            new ManualGamePlayerRequestDto { UserID = home[0], IsHomeTeam = true, Kills = 9, Deaths = 2 }));

        var line = await PlayerGameStatsQuery.Query(_context).Where(s => s.UserID == home[0]).SingleAsync();

        Assert.That((line.SeasonID, line.SeasonMatchID, line.TeamID, line.Won, line.Kills, line.Deaths, line.GameVersionID),
            Is.EqualTo((seeded.Season.SeasonID, (int?)seeded.SeasonMatch.SeasonMatchID, (int?)seeded.Home.TeamID, true, 9, 2, Halo3)));
    }

    // ---------- What is refused ----------

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
            Assert.That(await Refused(() => _service.AddManualGame(id, Game(5, 3, null, player with { UserID = 999999 }))), Does.Contain("not a user"));
        });
        Assert.That(await _context.RecordedGames.AnyAsync(), Is.False, "Nothing was saved");
    }

    [Test]
    public async Task AddManualGame_IsRefused_ForAMatchMissingATeam_AnImportedSeason_ADecidedMatch_OrAFullSeries()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var tbd = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(tbd);
        var other = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        other.Season.HistoricalSourceID = "archive/Total Stats";
        var forfeited = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 3, HomeTeamResult = SeasonMatchResult.Forfeit, AwayTeamResult = SeasonMatchResult.Won };
        var empty = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 0 };
        _context.SeasonMatches.AddRange(forfeited, empty);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused(() => _service.AddManualGame(tbd.SeasonMatchID, Game(5, 3))), Does.Contain("both teams"));
            Assert.That(await Refused(() => _service.AddManualGame(other.SeasonMatch.SeasonMatchID, Game(5, 3))), Does.Contain("historical archive"));
            Assert.That(await Refused(() => _service.AddManualGame(forfeited.SeasonMatchID, Game(5, 3))), Is.EqualTo("Results for this match have already been decided"));
            Assert.That(await Refused(() => _service.AddManualGame(empty.SeasonMatchID, Game(5, 3))), Does.Contain("already has all 0"));
        });
    }

    [Test]
    public async Task EditAndDelete_OnlyTakeThisMatchsGamesEnteredByHand()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var imported = new RecordedGame { SourceKey = "archive/game-1", GameVersionID = Halo3, SeasonID = seeded.Season.SeasonID };
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, RecordedGame = imported });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var id = seeded.SeasonMatch.SeasonMatchID;

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused(() => _service.EditManualGame(id, imported.RecordedGameID, Game(5, 3))), Does.Contain("entered by hand"));
            Assert.That(await Refused(() => _service.DeleteManualGame(id, imported.RecordedGameID)), Does.Contain("entered by hand"));
            Assert.That(await Refused(() => _service.DeleteManualGame(id, 999999)), Does.Contain("not one of this match's games"));
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
}
