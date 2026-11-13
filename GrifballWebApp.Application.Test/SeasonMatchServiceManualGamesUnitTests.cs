using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using static GrifballWebApp.Application.Test.SeasonMatchUnitTestData;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// SeasonMatchService's manual match entry (SeasonMatchService.ManualGames.cs) against the fake IApplicationDbContext:
/// adding, editing and removing a game entered by hand, what each refuses, how the series is numbered and scored from
/// its Waypoint and recorded games alike, and what a decided bracket match sends on. The Halo pull (IDataPullService) is
/// substituted; the bracket and championship services are the real ones. A save another commissioner's change got in
/// ahead of is made to fail by a SaveChanges interceptor on a context of its own.
/// </summary>
[TestFixture]
internal sealed class SeasonMatchServiceManualGamesUnitTests : ApplicationDbContextTests
{
    private static readonly int[] FirstTwoGames = [1, 2];
    private static readonly int?[] SevenToTwo = [7, 2];

    private IDataPullService _dataPull = null!;
    private BracketService _bracketService = null!;
    private SeasonMatchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _dataPull = Substitute.For<IDataPullService>();
        var championships = new ChampionshipService(Context);
        _bracketService = new BracketService(Context, new TeamStandingsService(Context), championships);
        _service = new SeasonMatchService(Context, _dataPull, _bracketService, championships);
    }

    /// <summary>A game as a commissioner enters it: Halo 3 on " Foundry " (trimmed when saved), home and away's scores.</summary>
    private static ManualGameRequestDto Entry(int home, int away, DateTime? playedAt = null, params ManualGamePlayerRequestDto[] players) => new()
    {
        GameVersionID = Halo3,
        Map = " Foundry ",
        Gametype = "Grifball",
        PlayedAt = playedAt,
        HomeTeamScore = home,
        AwayTeamScore = away,
        Players = players.ToList(),
    };

    private static ManualGamePlayerRequestDto Line(int userID, bool isHomeTeam) => new() { UserID = userID, IsHomeTeam = isHomeTeam };

    private static async Task<string> Refused(Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => action());
        return ex!.Message;
    }

    /// <summary>The season match as saved, with its games.</summary>
    private async Task<SeasonMatch> Reload(int seasonMatchID)
    {
        Context.ChangeTracker.Clear();
        return await Context.SeasonMatches.Include(m => m.SeriesGames).AsNoTracking().SingleAsync(m => m.SeasonMatchID == seasonMatchID);
    }

    /// <summary>A recorded game as saved, with its sides and players' lines.</summary>
    private async Task<RecordedGame> ReloadGame(int recordedGameID)
    {
        Context.ChangeTracker.Clear();
        return await Context.RecordedGames.Include(g => g.Teams).Include(g => g.Participants).AsSplitQuery().AsNoTracking()
            .SingleAsync(g => g.RecordedGameID == recordedGameID);
    }

    private async Task<int> SeriesGameOf(int recordedGameID)
        => await Context.Set<SeriesGame>().Where(g => g.RecordedGameID == recordedGameID).Select(g => g.SeriesGameID).SingleAsync();

    private async Task<int> Add(int seasonMatchID, ManualGameRequestDto dto)
    {
        Context.ChangeTracker.Clear();
        return await _service.AddManualGame(seasonMatchID, dto);
    }

    private static (int?, int?, SeasonMatchResult?, SeasonMatchResult?) ScoreAndResults(SeasonMatch sm) =>
        (sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult, sm.AwayTeamResult);

    private static IEnumerable<int?> RecordedGamesInOrder(SeasonMatch sm) => sm.SeriesGames.OrderBy(g => g.GameNumber).Select(g => g.RecordedGameID);

    #region AddManualGame

    [Test]
    public async Task AddManualGame_TheFirstGameOfABestOf3_RecordsIt_AndScoresWithoutDeciding()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;

        var id = await Add(smID, Entry(5, 3, InSeason,
            Line(seeded.HomeUsers[0].Id, true) with { Kills = 10, Deaths = 4, Assists = 2, Goals = 3, Score = 7 },
            Line(seeded.AwayUsers[0].Id, false) with { Kills = 4, Deaths = 10 }));

        var game = await ReloadGame(id);
        var sm = await Reload(smID);
        var homeSide = game.Teams.Single(t => t.Side == 1);
        var awaySide = game.Teams.Single(t => t.Side == 2);
        var homeLine = game.Participants.Single(p => p.UserID == seeded.HomeUsers[0].Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.Origin, Is.EqualTo(RecordedGameOrigin.Manual));
            Assert.That(game.SeasonID, Is.Null, "It counts for its season through its series game only");
            Assert.That((game.GameVersionID, game.Map, game.Gametype, game.PlayedAt, game.IsPlayoffs), Is.EqualTo((Halo3, "Foundry", "Grifball", (DateTime?)InSeason, false)));
            Assert.That((homeSide.TeamID, homeSide.Name, homeSide.Outcome, homeSide.Score), Is.EqualTo(((int?)seeded.Home.TeamID, "Home Team", Outcomes.Won, (int?)5)));
            Assert.That((awaySide.TeamID, awaySide.Name, awaySide.Outcome, awaySide.Score), Is.EqualTo(((int?)seeded.Away.TeamID, "Away Team", Outcomes.Lost, (int?)3)));
            Assert.That((homeLine.Gamertag, homeLine.Kills, homeLine.Deaths, homeLine.Assists, homeLine.Goals, homeLine.Score),
                Is.EqualTo(("GT101", 10, 4, 2, (int?)3, (int?)7)));
            Assert.That(homeLine.RecordedGameTeamID, Is.EqualTo(homeSide.RecordedGameTeamID));
            Assert.That(game.Participants.Single(p => p.UserID == seeded.AwayUsers[0].Id).RecordedGameTeamID, Is.EqualTo(awaySide.RecordedGameTeamID));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(sm.SeriesGames.Select(g => (g.RecordedGameID, g.GameNumber)), Is.EqualTo(new[] { ((int?)id, 1) }));
        }
    }

    [Test]
    public async Task AddManualGame_TheDecidingGame_DecidesTheMatch()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;

        await Add(smID, Entry(2, 5));
        await Add(smID, Entry(1, 5));

        var sm = await Reload(smID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)0, (int?)2, (SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That(sm.SeriesGames.Select(g => g.GameNumber).Order(), Is.EqualTo(FirstTwoGames));
        }
    }

    [Test]
    public async Task AddManualGame_WithItsScoreAlone_LeavesTheOptionalFieldsEmpty()
    {
        var seeded = await SeedAsync(Context);

        var id = await Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3) with { Map = null, Gametype = "   ", Players = null! });

        var game = await ReloadGame(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((game.Map, game.Gametype, game.PlayedAt), Is.EqualTo(((string?)null, (string?)null, (DateTime?)null)));
            Assert.That(game.Participants, Is.Empty);
            Assert.That(game.Teams, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task AddManualGame_APlayerWithoutAGamertag_IsRecordedByTheirDisplayName_NeverTheirLoginName()
    {
        var seeded = await SeedAsync(Context);
        var noXbox = User.Seed(userName: "secret-login", displayName: "Shown Name");
        seeded.Home.SeedPlayer(user: noXbox);
        await Context.SaveChangesAsync();

        var id = await Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3, null, Line(noXbox.Id, true)));

        var line = await Context.RecordedGameLines.AsNoTracking().SingleAsync(l => l.RecordedGameID == id);
        Assert.That(line.Gamertag, Is.EqualTo("Shown Name"));
    }

    [Test]
    public async Task AddManualGame_ForASeasonMatchThatIsNotThere_IsRefused()
    {
        var message = await Refused(() => _service.AddManualGame(987654, Entry(5, 3)));

        Assert.That(message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task AddManualGame_ForASeasonMatchWithoutBothTeams_IsRefused()
    {
        var seeded = await SeedAsync(Context, withAwayTeam: false);

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("Games can only be entered for a season match that has both teams assigned"));
            Assert.That(await Context.RecordedGames.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task AddManualGame_InASeasonImportedFromTheArchive_IsRefused()
    {
        var seeded = await SeedAsync(Context, historicalSourceID: "unit/archive");

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("This season was imported from the historical archive: correct its games in its spreadsheet"));
            Assert.That(await Context.RecordedGames.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task AddManualGame_ANewGameOfADecidedSeries_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("Results for this match have already been decided: to correct a game, replace it"));
            Assert.That(await Context.RecordedGames.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task AddManualGame_ANewGameOfASeriesWithAllItsGames_IsRefused()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        // Inconsistent data: a full series left undecided
        var byHand = ManualGame(seeded, "unit/full", 5, 2);
        Context.RecordedGames.Add(byHand);
        seeded.SeasonMatch.Game(1, recorded: byHand);
        await Context.SaveChangesAsync();

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("This series already has all 1 of its games"));
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(1));
        }
    }

    [TestCase(-1, 3, 7, 8, Halo3, "A team's score can't be negative")]
    [TestCase(3, -1, 7, 8, Halo3, "A team's score can't be negative")]
    [TestCase(4, 4, 7, 8, Halo3, "A game needs a winner: the two scores can't be equal")]
    [TestCase(5, 3, 101, 8, Halo3, "The map and gametype are at most 100 characters")]
    [TestCase(5, 3, 7, 101, Halo3, "The map and gametype are at most 100 characters")]
    [TestCase(5, 3, 7, 8, 999, "Unknown game version")]
    public async Task AddManualGame_RefusesAnEntryItCannotTake(int home, int away, int mapLength, int gametypeLength, int gameVersionID, string expected)
    {
        var seeded = await SeedAsync(Context);
        var entry = Entry(home, away) with { GameVersionID = gameVersionID, Map = new string('m', mapLength), Gametype = new string('g', gametypeLength) };

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, entry));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo(expected));
            Assert.That(await Context.RecordedGames.AnyAsync(), Is.False);
        }
    }

    public enum PlayerProblem
    {
        Twice,
        NegativeKills,
        NegativeGoals,
        NotAUser,
        HomePlayerForAway,
        AwayPlayerForHome,
        OnNeitherRoster,
    }

    [TestCase(PlayerProblem.Twice, "A player can only be entered once per game")]
    [TestCase(PlayerProblem.NegativeKills, "Kills, deaths, assists and goals can't be negative")]
    [TestCase(PlayerProblem.NegativeGoals, "Kills, deaths, assists and goals can't be negative")]
    [TestCase(PlayerProblem.NotAUser, "A player entered is not a user of the site")]
    [TestCase(PlayerProblem.HomePlayerForAway, "GT101 is on Home Team's roster: they can't play for Away Team")]
    [TestCase(PlayerProblem.AwayPlayerForHome, "GT201 is on Away Team's roster: they can't play for Home Team")]
    [TestCase(PlayerProblem.OnNeitherRoster, "Outsider is on neither team's roster")]
    public async Task AddManualGame_RefusesAPlayerItCannotTake(PlayerProblem problem, string expected)
    {
        var seeded = await SeedAsync(Context);
        var outsider = User.Seed(userName: "outsider", displayName: "Outsider");
        Context.Users.Add(outsider);
        await Context.SaveChangesAsync();
        var (home, away) = (seeded.HomeUsers[0].Id, seeded.AwayUsers[0].Id);
        ManualGamePlayerRequestDto[] players = problem switch
        {
            PlayerProblem.Twice => [Line(home, true), Line(home, true)],
            PlayerProblem.NegativeKills => [Line(home, true) with { Kills = -1 }],
            PlayerProblem.NegativeGoals => [Line(away, false) with { Goals = -2 }],
            PlayerProblem.NotAUser => [Line(987654, true)],
            PlayerProblem.HomePlayerForAway => [Line(home, false)],
            PlayerProblem.AwayPlayerForHome => [Line(away, true)],
            _ => [Line(outsider.Id, true)],
        };

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3, null, players)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo(expected));
            Assert.That(await Context.RecordedGames.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task AddManualGame_ReplacingAWaypointGame_TakesItsNumber_AndFreesItToBeReportedAgain()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        var replaced = seeded.SeasonMatch.Game(1, halo: waypoint);
        seeded.SeasonMatch.Scored(1, 0);
        await Context.SaveChangesAsync();
        var waypointID = MatchID(1);

        var id = await Add(seeded.SeasonMatch.SeasonMatchID, Entry(2, 5) with { ReplacesSeriesGameID = replaced.SeriesGameID });

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sm.SeriesGames.Select(g => (g.MatchID, g.RecordedGameID, g.GameNumber)), Is.EqualTo(new[] { ((Guid?)null, (int?)id, 1) }));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)0, (int?)1, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(await Context.Matches.AnyAsync(m => m.MatchID == waypointID), Is.True);
        }
    }

    [Test]
    public async Task AddManualGame_ReplacingTheGameThatDecidedTheSeries_CanChangeItsWinner_AndDeletesTheGameReplaced()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        var first = ManualGame(seeded, "unit/first", 5, 2);
        Context.RecordedGames.Add(first);
        var replaced = seeded.SeasonMatch.Game(1, recorded: first);
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();

        var id = await Add(seeded.SeasonMatch.SeasonMatchID, Entry(2, 5) with { ReplacesSeriesGameID = replaced.SeriesGameID });

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)0, (int?)1, (SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That(RecordedGamesInOrder(sm), Is.EqualTo(new[] { (int?)id }));
            Assert.That(await Context.RecordedGames.AnyAsync(g => g.RecordedGameID == first.RecordedGameID), Is.False);
        }
    }

    [Test]
    public async Task AddManualGame_ReplacingAGameOfAForfeitedMatch_IsRefused()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var first = ManualGame(seeded, "unit/forfeited", 5, 2);
        Context.RecordedGames.Add(first);
        var replaced = seeded.SeasonMatch.Game(1, recorded: first);
        seeded.SeasonMatch.Scored(1, 0);
        seeded.SeasonMatch.Forfeited(Forfeiter.Home);
        await Context.SaveChangesAsync();

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(2, 5) with { ReplacesSeriesGameID = replaced.SeriesGameID }));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("This match was decided by a forfeit or a BYE, not its games: undo its result first"));
            Assert.That(RecordedGamesInOrder(sm), Is.EqualTo(new[] { (int?)first.RecordedGameID }));
        }
    }

    [Test]
    public async Task AddManualGame_WhenAWaypointGameOfTheSeriesNoLongerMatchesTheRosters_IsRefused_NotGuessed()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var strangers = await AddMatchAsync(Context, 1, InSeason, OtherFour, Outcomes.Won, OtherFourMore, Outcomes.Lost);
        seeded.SeasonMatch.Game(1, halo: strangers);
        seeded.SeasonMatch.Scored(1, 0);
        await Context.SaveChangesAsync();

        var message = await Refused(() => Add(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("Game 1, from Waypoint, no longer matches the teams' rosters: undo the result and report the series again"));
            Assert.That(await Context.RecordedGames.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task AddManualGame_ASaveAnotherChangeGotInAheadOf_IsRefused_AndSavesNothing()
    {
        var saves = new FailingSaves();
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new FakeApplicationDbContext(new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(connection).AddInterceptors(saves).Options);
        await context.Database.EnsureCreatedAsync();
        var seeded = await SeedAsync(context, bestOf: 3);
        context.ChangeTracker.Clear();
        var service = NewService(context, _dataPull);
        saves.Failure = new DbUpdateException("Another game took game number 1 first");

        var message = await Refused(() => service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        saves.Failure = null;
        context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo(SeasonMatchService.SeriesChangedMessage));
            Assert.That(await context.RecordedGames.AnyAsync(), Is.False, "Nothing was saved");
        }
    }

    [Test]
    public async Task AddManualGame_AConcurrencyFailure_IsNotTakenForAnotherChangeToTheSeries()
    {
        var saves = new FailingSaves();
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new FakeApplicationDbContext(new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(connection).AddInterceptors(saves).Options);
        await context.Database.EnsureCreatedAsync();
        var seeded = await SeedAsync(context, bestOf: 3);
        context.ChangeTracker.Clear();
        var service = NewService(context, _dataPull);
        saves.Failure = new DbUpdateConcurrencyException("The row changed");

        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Entry(5, 3)));

        Assert.That(ex!.Message, Is.EqualTo("The row changed"));
    }

    #endregion

    #region EditManualGame

    [Test]
    public async Task EditManualGame_OnlyTakesAGameOfThisMatch_EnteredByHand()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var imported = RecordedGame.Seed("unit/imported", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        imported.SeedSide(1, seeded.Home.TeamID, Outcomes.Won, 5);
        imported.SeedSide(2, seeded.Away.TeamID, Outcomes.Lost, 1);
        Context.RecordedGames.Add(imported);
        seeded.SeasonMatch.Game(1, recorded: imported);
        seeded.SeasonMatch.Scored(1, 0);
        await Context.SaveChangesAsync();
        var smID = seeded.SeasonMatch.SeasonMatchID;
        Context.ChangeTracker.Clear();

        var notOfThisMatch = await Refused(() => _service.EditManualGame(smID, 987654, Entry(5, 3)));
        Context.ChangeTracker.Clear();
        var notByHand = await Refused(() => _service.EditManualGame(smID, imported.RecordedGameID, Entry(5, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notOfThisMatch, Is.EqualTo("That game is not one of this match's games"));
            Assert.That(notByHand, Is.EqualTo("Only a game entered by hand can be changed here"));
        }
    }

    [Test]
    public async Task EditManualGame_ThatKeepsTheWinner_OnlyRewritesTheGame()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var (first, second) = (seeded.HomeUsers[0].Id, seeded.HomeUsers[1].Id);
        var id = await Add(smID, Entry(5, 3, null, Line(first, true) with { Kills = 1 }));
        Context.ChangeTracker.Clear();

        await _service.EditManualGame(smID, id, Entry(7, 2, null, Line(second, true) with { Kills = 9 }) with { Map = "Lockout" });

        var sm = await Reload(smID);
        var game = await ReloadGame(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss)));
            Assert.That(game.Map, Is.EqualTo("Lockout"));
            Assert.That(game.Teams.OrderBy(t => t.Side).Select(t => t.Score), Is.EqualTo(SevenToTwo));
            Assert.That(game.Participants.Select(p => (p.UserID, p.Kills)), Is.EqualTo(new[] { (second, 9) }), "The players' lines are replaced");
        }
    }

    [Test]
    public async Task EditManualGame_ThatChangesTheWinner_OfADecidedMatch_ReopensAndScoresItAgain()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var id = await Add(smID, Entry(5, 2));
        Context.ChangeTracker.Clear();

        await _service.EditManualGame(smID, id, Entry(2, 5));

        var sm = await Reload(smID);
        var game = await ReloadGame(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)0, (int?)1, (SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That(game.Teams.Single(t => t.Side == 2).Outcome, Is.EqualTo(Outcomes.Won));
        }
    }

    [Test]
    public async Task EditManualGame_OfAForfeitedMatch_MayKeepTheWinner_ButNotChangeIt()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var id = await Add(smID, Entry(5, 3));
        Context.ChangeTracker.Clear();
        await _service.HomeForfeit(smID);
        Context.ChangeTracker.Clear();

        var changingTheWinner = await Refused(() => _service.EditManualGame(smID, id, Entry(2, 5)));
        Context.ChangeTracker.Clear();
        await _service.EditManualGame(smID, id, Entry(6, 1) with { Map = "Guardian" });

        var sm = await Reload(smID);
        var game = await ReloadGame(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changingTheWinner, Is.EqualTo("This match was decided by a forfeit or a BYE, not its games: undo its result first"));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Forfeit, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That(game.Map, Is.EqualTo("Guardian"));
        }
    }

    [Test]
    public async Task EditManualGame_WithANewTime_PlacesTheGameAgainAmongTheOthers()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var first = await Add(smID, Entry(5, 1, InSeason));
        var second = await Add(smID, Entry(1, 5, InSeason.AddHours(1)));
        Context.ChangeTracker.Clear();

        // Played an hour before the first after all
        await _service.EditManualGame(smID, second, Entry(2, 5, InSeason.AddHours(-1)));

        var sm = await Reload(smID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RecordedGamesInOrder(sm), Is.EqualTo(new[] { (int?)second, first }));
            Assert.That(sm.SeriesGames.Select(g => g.GameNumber).Order(), Is.EqualTo(FirstTwoGames));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)1, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        }
    }

    [Test]
    public async Task EditManualGame_KeepsAPlayerOnNeitherRoster_WhoWasAlreadyOnTheGame()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var former = User.Seed(userName: "former", displayName: "Former Player");
        Context.Users.Add(former);
        await Context.SaveChangesAsync();
        var game = ManualGame(seeded, "unit/former", 5, 2);
        game.SeedPlayer(new RecordedPlayerLine(former.Id, 1, "Former Player", 1, 1, 0), game.Teams.Single(t => t.Side == 1));
        Context.RecordedGames.Add(game);
        seeded.SeasonMatch.Game(1, recorded: game);
        seeded.SeasonMatch.Scored(1, 0);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, game.RecordedGameID, Entry(5, 2, null, Line(former.Id, true) with { Kills = 6 }));

        var line = await Context.RecordedGameLines.AsNoTracking().SingleAsync(l => l.RecordedGameID == game.RecordedGameID);
        Assert.That((line.UserID, line.Kills, line.Gamertag), Is.EqualTo((former.Id, 6, "Former Player")));
    }

    #endregion

    #region RemoveGame

    [Test]
    public async Task RemoveGame_NotOneOfThisMatchsGames_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        Context.ChangeTracker.Clear();

        var message = await Refused(() => _service.RemoveGame(seeded.SeasonMatch.SeasonMatchID, 987654));

        Assert.That(message, Is.EqualTo("That game is not one of this match's games"));
    }

    [Test]
    public async Task RemoveGame_EnteredByHand_DeletesIt_RenumbersTheRest_AndScoresAgain()
    {
        var seeded = await SeedAsync(Context, bestOf: 5);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var first = await Add(smID, Entry(5, 1, InSeason, Line(seeded.HomeUsers[0].Id, true)));
        var second = await Add(smID, Entry(1, 5, InSeason.AddHours(1)));
        var third = await Add(smID, Entry(5, 2, InSeason.AddHours(2)));
        var firstGame = await SeriesGameOf(first);
        Context.ChangeTracker.Clear();

        await _service.RemoveGame(smID, firstGame);

        var sm = await Reload(smID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sm.SeriesGames.OrderBy(g => g.GameNumber).Select(g => (g.RecordedGameID, g.GameNumber)), Is.EqualTo(new[] { ((int?)second, 1), ((int?)third, 2) }));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)1, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(await Context.RecordedGames.AnyAsync(g => g.RecordedGameID == first), Is.False);
            Assert.That(await Context.RecordedGameLines.AnyAsync(l => l.RecordedGameID == first), Is.False);
        }
    }

    [Test]
    public async Task RemoveGame_TheWaypointGameThatDecidedTheMatch_ReopensIt_AndFreesTheGame()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        var waypointID = MatchID(1);
        Context.ChangeTracker.Clear();
        await _service.ReportMatch(smID, waypointID);
        var reported = await Context.Set<SeriesGame>().Where(g => g.MatchID == waypointID).Select(g => g.SeriesGameID).SingleAsync();
        Context.ChangeTracker.Clear();

        await _service.RemoveGame(smID, reported);

        var sm = await Reload(smID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)null, (int?)null, (SeasonMatchResult?)null, (SeasonMatchResult?)null)), "No games left: no score");
            Assert.That(sm.SeriesGames, Is.Empty);
            Assert.That(await Context.Matches.AnyAsync(m => m.MatchID == waypointID), Is.True);
        }
    }

    [Test]
    public async Task RemoveGame_OfAForfeitedMatch_IsRefused()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var smID = seeded.SeasonMatch.SeasonMatchID;
        var id = await Add(smID, Entry(5, 3));
        var game = await SeriesGameOf(id);
        Context.ChangeTracker.Clear();
        await _service.AwayForfeit(smID);
        Context.ChangeTracker.Clear();

        var message = await Refused(() => _service.RemoveGame(smID, game));

        var sm = await Reload(smID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.EqualTo("This match was decided by a forfeit or a BYE, not its games: undo its result first"));
            Assert.That(RecordedGamesInOrder(sm), Is.EqualTo(new[] { (int?)id }));
        }
    }

    #endregion

    #region In the bracket

    [Test]
    public async Task AddManualGame_TheDecidingGameInTheWinnerBracket_SendsTheTeamsOn_AsAPlayoffGame()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var seeds = b.SeedTeamIDs;

        var id = await Add(b.Sm(Bracket.Winner, 1), Entry(5, 3));

        var w1 = await Reload(b.Sm(Bracket.Winner, 1));
        var w3 = await Reload(b.Sm(Bracket.Winner, 3));
        var l1 = await Reload(b.Sm(Bracket.Loser, 1));
        var game = await ReloadGame(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Slots(w1), Is.EqualTo(Expect(seeds[0], SeasonMatchResult.Won, seeds[3], SeasonMatchResult.Loss)));
            Assert.That(w3.HomeTeamID, Is.EqualTo(seeds[0]));
            Assert.That(l1.HomeTeamID, Is.EqualTo(seeds[3]));
            Assert.That(game.IsPlayoffs, Is.True);
        }
    }

    [Test]
    public async Task RemoveGame_ThatDecidedABracketMatch_TakesBackWhatItsResultSentOn()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var seeds = b.SeedTeamIDs;
        var w1ID = b.Sm(Bracket.Winner, 1);
        var id = await Add(w1ID, Entry(5, 3));
        var game = await SeriesGameOf(id);
        Context.ChangeTracker.Clear();

        await _service.RemoveGame(w1ID, game);

        var w1 = await Reload(w1ID);
        var w3 = await Reload(b.Sm(Bracket.Winner, 3));
        var l1 = await Reload(b.Sm(Bracket.Loser, 1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Slots(w1), Is.EqualTo(Expect(seeds[0], null, seeds[3], null)));
            Assert.That(w1.SeriesGames, Is.Empty);
            Assert.That(w3.HomeTeamID, Is.Null);
            Assert.That(l1.HomeTeamID, Is.Null);
            Assert.That(await Context.RecordedGames.AnyAsync(g => g.RecordedGameID == id), Is.False);
        }
    }

    /// <summary>The grand final, with the first two seeds placed in it (as the matches before it would).</summary>
    private async Task<int> GrandFinalOfTheFirstTwoSeeds(Bracket4 b)
    {
        var id = b.Sm(Bracket.GrandFinal);
        Context.ChangeTracker.Clear();
        var grandFinal = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == id);
        grandFinal.PlaceTeam(SeasonMatchSide.Home, b.SeedTeamIDs[0]);
        grandFinal.PlaceTeam(SeasonMatchSide.Away, b.SeedTeamIDs[1]);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return id;
    }

    [Test]
    public async Task AddManualGame_TheHomeTeamWinningTheGrandFinal_LeavesTheSuddenDeathEmpty()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var grandFinalID = await GrandFinalOfTheFirstTwoSeeds(b);

        await Add(grandFinalID, Entry(5, 3));

        var grandFinal = await Reload(grandFinalID);
        var suddenDeath = await Reload(b.Sm(Bracket.GrandFinalSuddenDeath));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Slots(grandFinal), Is.EqualTo(Expect(b.SeedTeamIDs[0], SeasonMatchResult.Won, b.SeedTeamIDs[1], SeasonMatchResult.Loss)));
            Assert.That(Slots(suddenDeath), Is.EqualTo(Expect(null, null, null, null)));
        }
    }

    [Test]
    public async Task AddManualGame_TheAwayTeamWinningTheGrandFinal_SendsBothTeamsToTheSuddenDeath()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var grandFinalID = await GrandFinalOfTheFirstTwoSeeds(b);

        await Add(grandFinalID, Entry(3, 5));

        var suddenDeath = await Reload(b.Sm(Bracket.GrandFinalSuddenDeath));
        Assert.That(Slots(suddenDeath), Is.EqualTo(Expect(b.SeedTeamIDs[1], null, b.SeedTeamIDs[0], null)));
    }

    #endregion

    private static SeasonMatchService NewService(FakeApplicationDbContext context, IDataPullService dataPull)
    {
        var championships = new ChampionshipService(context);
        return new SeasonMatchService(context, dataPull, new BracketService(context, new TeamStandingsService(context), championships), championships);
    }

    /// <summary>Fails every save with <see cref="Failure"/> while it is set: a save another change got in ahead of.</summary>
    private sealed class FailingSaves : SaveChangesInterceptor
    {
        public Exception? Failure { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Failure is { } failure ? throw failure : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
