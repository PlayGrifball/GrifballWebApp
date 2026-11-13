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
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using static GrifballWebApp.Application.Test.SeasonMatchUnitTestData;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// SeasonMatchService against the fake IApplicationDbContext: the match page (its result, bracket slot, reschedule state
/// and box score), the possible matches, the season window, reporting a Waypoint game, forfeits and Undo Result. The Halo
/// pull (IDataPullService) is substituted; the bracket and championship services are the real ones, on the same context.
/// Games entered by hand (SeasonMatchService.ManualGames.cs) are SeasonMatchServiceManualGamesUnitTests'.
/// Not reachable here: MapToResult's out-of-range arm (the database's LegalResults check allows no other result), GetTeams'
/// second null (a game whose teams each hold one roster's players always leans apart), and the "Missing winning/losing
/// team" throws (a decided match always has both).
/// </summary>
[TestFixture]
internal sealed class SeasonMatchServiceUnitTests : ApplicationDbContextTests
{
    private static readonly (long, bool)[] HomeThreeAndRingerOnTeam = [(101, true), (102, true), (103, true), (301, false)];
    private static readonly string[] OthersFirstFour = ["GT304", "GT303", "GT302", "GT301"];
    private static readonly string[] OthersLastFour = ["GT308", "GT307", "GT306", "GT305"];
    private static readonly string[] HomeOnSideTwo = ["HomeOnSideTwo"];
    private static readonly string[] AwayOnSideOne = ["AwayOnSideOne"];
    private static readonly string[] HomeKnown = ["HomeKnown"];
    private static readonly string[] OnUnknownSide = ["OnUnknownSide"];
    private static readonly string[] HomeOnly = ["HomeOnly"];

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

    /// <summary>The season match as saved, with its games.</summary>
    private async Task<SeasonMatch> Reload(int seasonMatchID)
    {
        Context.ChangeTracker.Clear();
        return await Context.SeasonMatches.Include(m => m.SeriesGames).AsNoTracking().SingleAsync(m => m.SeasonMatchID == seasonMatchID);
    }

    private async Task<string> ReportRefused(int seasonMatchID, Guid matchID, int? replacesSeriesGameID = null)
    {
        Context.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(seasonMatchID, matchID, replacesSeriesGameID));
        return ex!.Message;
    }

    private static (int?, int?, SeasonMatchResult?, SeasonMatchResult?) ScoreAndResults(SeasonMatch sm) =>
        (sm.HomeTeamScore, sm.AwayTeamScore, sm.HomeTeamResult, sm.AwayTeamResult);

    private static IEnumerable<(Guid?, int?, int)> GamesInOrder(SeasonMatch sm) =>
        sm.SeriesGames.OrderBy(g => g.GameNumber).Select(g => (g.MatchID, g.RecordedGameID, g.GameNumber));

    #region GetSeasonMatchPage

    [Test]
    public async Task GetSeasonMatchPage_NoSuchMatch_ReturnsNull()
    {
        var page = await _service.GetSeasonMatchPage(987654);

        Assert.That(page, Is.Null);
    }

    [Test]
    public async Task GetSeasonMatchPage_AnUndecidedMatch_ShowsItsTeams_AndOnlyARosteredPlayerMayRequestAReschedule()
    {
        var seeded = await SeedAsync(Context, bestOf: 3, scheduledTime: InSeason);
        var id = seeded.SeasonMatch.SeasonMatchID;

        var forPlayer = (await _service.GetSeasonMatchPage(id, seeded.HomeUsers[0].Id, CancellationToken.None))!;
        var forVisitor = (await _service.GetSeasonMatchPage(id))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((forPlayer.SeasonID, forPlayer.SeasonName, forPlayer.IsPlayoff), Is.EqualTo((seeded.Season.SeasonID, "Unit Season", false)));
            Assert.That((forPlayer.HomeTeamName, forPlayer.HomeTeamID, forPlayer.HomeTeamScore, forPlayer.HomeTeamResult),
                Is.EqualTo(("Home Team", (int?)seeded.Home.TeamID, (int?)null, SeasonMatchResultDto.TBD)));
            Assert.That((forPlayer.AwayTeamName, forPlayer.AwayTeamID, forPlayer.AwayTeamScore, forPlayer.AwayTeamResult),
                Is.EqualTo(("Away Team", (int?)seeded.Away.TeamID, (int?)null, SeasonMatchResultDto.TBD)));
            Assert.That(forPlayer.DecidedBy, Is.Null);
            Assert.That(forPlayer.CanUndo, Is.False, "No result and no games: nothing to undo");
            Assert.That((forPlayer.ScheduledTime, forPlayer.BestOf), Is.EqualTo(((DateTime?)InSeason, 3)));
            Assert.That(forPlayer.ReportedGames, Is.Empty);
            Assert.That(forPlayer.SeriesTotals, Is.Null);
            Assert.That(forPlayer.IsImported, Is.False);
            Assert.That(forPlayer.BracketInfo, Is.Null);
            Assert.That(forPlayer.ActiveRescheduleRequestId, Is.Null);
            Assert.That(forPlayer.CanRequestReschedule, Is.True);
            Assert.That(forVisitor.CanRequestReschedule, Is.False);
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_DecidedByItsGames_InAnImportedSeason()
    {
        var seeded = await SeedAsync(Context, bestOf: 3, historicalSourceID: "unit/imported");
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Away);
        await Context.SaveChangesAsync();

        var page = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID, seeded.HomeUsers[0].Id, CancellationToken.None))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((page.HomeTeamScore, page.AwayTeamScore), Is.EqualTo(((int?)0, (int?)2)));
            Assert.That((page.HomeTeamResult, page.AwayTeamResult), Is.EqualTo((SeasonMatchResultDto.Loss, SeasonMatchResultDto.Won)));
            Assert.That(page.DecidedBy, Is.EqualTo(SeasonMatchDecisionDto.Games));
            Assert.That(page.CanUndo, Is.True, "A regular-season result has no bracket to ask");
            Assert.That(page.IsImported, Is.True);
            Assert.That(page.CanRequestReschedule, Is.False, "A decided match keeps its time");
        }
    }

    [TestCase(Forfeiter.Home, SeasonMatchResultDto.Forfeit, SeasonMatchResultDto.Won)]
    [TestCase(Forfeiter.Away, SeasonMatchResultDto.Won, SeasonMatchResultDto.Forfeit)]
    [TestCase(Forfeiter.Both, SeasonMatchResultDto.Forfeit, SeasonMatchResultDto.Forfeit)]
    public async Task GetSeasonMatchPage_AForfeit_ShowsEachSidesResult(Forfeiter forfeiter, SeasonMatchResultDto home, SeasonMatchResultDto away)
    {
        var seeded = await SeedAsync(Context);
        seeded.SeasonMatch.Forfeited(forfeiter);
        await Context.SaveChangesAsync();

        var page = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((page.HomeTeamResult, page.AwayTeamResult), Is.EqualTo((home, away)));
            Assert.That(page.DecidedBy, Is.EqualTo(SeasonMatchDecisionDto.Forfeit));
            Assert.That(page.CanUndo, Is.True);
            Assert.That(page.HomeTeamScore, Is.Null, "No games were played");
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_AWinOnABye_IsTheByes_AndCannotBeUndone()
    {
        var seeded = await SeedAsync(Context, withAwayTeam: false);
        seeded.SeasonMatch.WonOnBye(SeasonMatchSide.Away);
        await Context.SaveChangesAsync();

        var page = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID, seeded.HomeUsers[0].Id, CancellationToken.None))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((page.HomeTeamResult, page.AwayTeamResult), Is.EqualTo((SeasonMatchResultDto.Won, SeasonMatchResultDto.Bye)));
            Assert.That(page.DecidedBy, Is.EqualTo(SeasonMatchDecisionDto.Bye));
            Assert.That(page.CanUndo, Is.False);
            Assert.That((page.AwayTeamName, page.AwayTeamID), Is.EqualTo(((string?)null, (int?)null)));
            Assert.That(page.CanRequestReschedule, Is.False);
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_APendingRescheduleRequest_IsTheActiveOne_AndNoOtherCanBeRequested()
    {
        var seeded = await SeedAsync(Context, scheduledTime: InSeason);
        var reschedule = MatchReschedule.Request(seeded.SeasonMatch, InSeason.AddDays(2), "The away team can't make it",
            seeded.AwayUsers[0].Id, InSeason.AddDays(-1)).Reschedule!;
        await Context.SaveChangesAsync();

        var page = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID, seeded.HomeUsers[0].Id, CancellationToken.None))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.ActiveRescheduleRequestId, Is.EqualTo(reschedule.MatchRescheduleID));
            Assert.That(page.CanRequestReschedule, Is.False);
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_BoxScore_PlacesWaypointTeamsByRoster_AndTotalsTheSeriesOneRowAPlayer()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var (home, away) = (seeded.HomeUsers, seeded.AwayUsers);
        // The home team played as in-game team 1, with a ringer (an Xbox account with no user)
        var waypoint = HaloMatch.Seed(MatchID(1), startTime: InSeason, endTime: InSeason.AddMinutes(10), duration: TimeSpan.FromMinutes(10));
        waypoint.AddTeam(0, 3, Outcomes.Lost);
        waypoint.AddTeam(1, 5, Outcomes.Won);
        waypoint.AddParticipant(1, 101, new MatchParticipantStats { Kills = 10, Deaths = 2, Assists = 3, Score = 2, PersonalScore = 300 });
        waypoint.AddParticipant(1, 102, new MatchParticipantStats { Kills = 12, Deaths = 4, Assists = 1, Score = 2, PersonalScore = 250 });
        waypoint.AddParticipant(1, 301, new MatchParticipantStats { Kills = 5, Deaths = 0, Score = 3, PersonalScore = 100 });
        waypoint.AddParticipant(0, 201, new MatchParticipantStats { Kills = 6, Deaths = 6, Score = 1, PersonalScore = 150 });
        waypoint.AddParticipant(0, 202, new MatchParticipantStats { Kills = 4, Deaths = 8, Score = 0, PersonalScore = 50 });
        Context.Matches.Add(waypoint);
        // An imported game: its players under the gamertags it recorded, no goals for some, no score for any
        var recorded = RecordedGame.Seed("unit/box-score", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        var homeSide = recorded.SeedSide(1, seeded.Home.TeamID, Outcomes.Won, 5);
        var awaySide = recorded.SeedSide(2, seeded.Away.TeamID, Outcomes.Lost, 2);
        recorded.SeedPlayer(new RecordedPlayerLine(home[0].Id, 1, "OldTag101", 3, 1, 0) { Goals = 1 }, homeSide);
        recorded.SeedPlayer(new RecordedPlayerLine(away[0].Id, 2, "Away201", 1, 3, 0), awaySide);
        recorded.SeedPlayer(new RecordedPlayerLine(away[2].Id, 2, "Away203", 2, 2, 1), awaySide);
        Context.RecordedGames.Add(recorded);
        var first = seeded.SeasonMatch.Game(1, halo: waypoint);
        var second = seeded.SeasonMatch.Game(2, recorded: recorded);
        await Context.SaveChangesAsync();

        var page = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!;

        Assert.That(page.ReportedGames, Has.Length.EqualTo(2));
        var (g1, g2) = (page.ReportedGames[0], page.ReportedGames[1]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((g1.SeriesGameID, g1.MatchID, g1.RecordedGameID, g1.MatchNumber), Is.EqualTo((first.SeriesGameID, (Guid?)MatchID(1), (int?)null, 1)));
            Assert.That((g1.HomeScore, g1.AwayScore, g1.IsManual), Is.EqualTo(((int?)5, (int?)3, false)));
            Assert.That((g1.GameVersionID, g1.GameVersionName, g1.Map, g1.Gametype, g1.PlayedAt), Is.EqualTo(((int?)null, (string?)null, (string?)null, (string?)null, (DateTime?)null)));
            Assert.That(g1.HomePlayers, Is.EqualTo(new[]
            {
                new SeasonMatchGameParticipantDto { UserID = null, Gamertag = "GT301", Kills = 5, Deaths = 0, Assists = 0, BombScores = 3, Score = 100, KdRatio = null },
                new SeasonMatchGameParticipantDto { UserID = home[1].Id, Gamertag = "GT102", Kills = 12, Deaths = 4, Assists = 1, BombScores = 2, Score = 250, KdRatio = 3m },
                new SeasonMatchGameParticipantDto { UserID = home[0].Id, Gamertag = "GT101", Kills = 10, Deaths = 2, Assists = 3, BombScores = 2, Score = 300, KdRatio = 5m },
            }));
            Assert.That(g1.AwayPlayers, Is.EqualTo(new[]
            {
                new SeasonMatchGameParticipantDto { UserID = away[0].Id, Gamertag = "GT201", Kills = 6, Deaths = 6, BombScores = 1, Score = 150, KdRatio = 1m },
                new SeasonMatchGameParticipantDto { UserID = away[1].Id, Gamertag = "GT202", Kills = 4, Deaths = 8, BombScores = 0, Score = 50, KdRatio = 0.5m },
            }));

            Assert.That((g2.SeriesGameID, g2.MatchID, g2.RecordedGameID, g2.MatchNumber), Is.EqualTo((second.SeriesGameID, (Guid?)null, (int?)recorded.RecordedGameID, 2)));
            Assert.That((g2.HomeScore, g2.AwayScore, g2.IsManual, g2.GameVersionID, g2.GameVersionName), Is.EqualTo(((int?)5, (int?)2, false, (int?)Halo3, "Halo 3")));
            Assert.That(g2.HomePlayers, Is.EqualTo(new[]
            {
                new SeasonMatchGameParticipantDto { UserID = home[0].Id, Gamertag = "OldTag101", Kills = 3, Deaths = 1, BombScores = 1, Score = null, KdRatio = 3m },
            }));
            Assert.That(g2.AwayPlayers, Is.EqualTo(new[]
            {
                new SeasonMatchGameParticipantDto { UserID = away[2].Id, Gamertag = "Away203", Kills = 2, Deaths = 2, Assists = 1, BombScores = 0, Score = null, KdRatio = 1m },
                new SeasonMatchGameParticipantDto { UserID = away[0].Id, Gamertag = "Away201", Kills = 1, Deaths = 3, BombScores = 0, Score = null, KdRatio = 0.33m },
            }));

            // One row a player over the series, under the gamertag of their latest game; a ringer by their Xbox account
            Assert.That(page.SeriesTotals!.HomePlayers, Is.EqualTo(new[]
            {
                new SeasonMatchGameParticipantDto { UserID = home[0].Id, Gamertag = "OldTag101", Kills = 13, Deaths = 3, Assists = 3, BombScores = 3, Score = 300, KdRatio = 4.33m },
                new SeasonMatchGameParticipantDto { UserID = null, Gamertag = "GT301", Kills = 5, Deaths = 0, Assists = 0, BombScores = 3, Score = 100, KdRatio = null },
                new SeasonMatchGameParticipantDto { UserID = home[1].Id, Gamertag = "GT102", Kills = 12, Deaths = 4, Assists = 1, BombScores = 2, Score = 250, KdRatio = 3m },
            }));
            Assert.That(page.SeriesTotals.AwayPlayers, Is.EqualTo(new[]
            {
                new SeasonMatchGameParticipantDto { UserID = away[0].Id, Gamertag = "Away201", Kills = 7, Deaths = 9, BombScores = 1, Score = 150, KdRatio = 0.78m },
                new SeasonMatchGameParticipantDto { UserID = away[1].Id, Gamertag = "GT202", Kills = 4, Deaths = 8, BombScores = 0, Score = 50, KdRatio = 0.5m },
                new SeasonMatchGameParticipantDto { UserID = away[2].Id, Gamertag = "Away203", Kills = 2, Deaths = 2, Assists = 1, BombScores = 0, Score = null, KdRatio = 1m },
            }));
            Assert.That(page.CanUndo, Is.True, "An undecided series with games: Undo Result clears them");
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_AWaypointGameOfNobodyRostered_IsLaidOutByInGameTeam()
    {
        var seeded = await SeedAsync(Context);
        var strangers = await AddMatchAsync(Context, 1, InSeason, OtherFour, Outcomes.Lost, OtherFourMore, Outcomes.Won, team0Score: 2, team1Score: 7);
        seeded.SeasonMatch.Game(1, halo: strangers);
        await Context.SaveChangesAsync();

        var game = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!.ReportedGames.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((game.HomeScore, game.AwayScore), Is.EqualTo(((int?)2, (int?)7)), "In-game team 0 shows as home");
            Assert.That(game.HomePlayers.Select(p => p.Gamertag), Is.EqualTo(OthersFirstFour));
            Assert.That(game.AwayPlayers.Select(p => p.Gamertag), Is.EqualTo(OthersLastFour));
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_RecordedGames_AreSidedByTheirTeams_ElseTheOtherSide_ElseBySideNumber()
    {
        var seeded = await SeedAsync(Context, bestOf: 5);
        var (home, away) = (seeded.HomeUsers, seeded.AwayUsers);
        // Entered by hand, the away team on side 1
        var byHand = RecordedGame.Seed("unit/sides-1", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual, playedAt: InSeason, map: "Foundry", gametype: "Grifball");
        var byHandAway = byHand.SeedSide(1, seeded.Away.TeamID, Outcomes.Lost, 3);
        var byHandHome = byHand.SeedSide(2, seeded.Home.TeamID, Outcomes.Won, 5);
        byHand.SeedPlayer(new RecordedPlayerLine(away[0].Id, 1, "AwayOnSideOne", 1, 1, 0), byHandAway);
        byHand.SeedPlayer(new RecordedPlayerLine(home[0].Id, 2, "HomeOnSideTwo", 2, 1, 0), byHandHome);
        // Only the home team known: the one other side is away, whatever its number
        var oneKnown = RecordedGame.Seed("unit/sides-2", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        var oneKnownHome = oneKnown.SeedSide(1, seeded.Home.TeamID, Outcomes.Won, 5);
        var oneKnownOther = oneKnown.SeedSide(3, null, Outcomes.Lost, 1);
        oneKnown.SeedPlayer(new RecordedPlayerLine(home[1].Id, 1, "HomeKnown", 2, 1, 0), oneKnownHome);
        oneKnown.SeedPlayer(new RecordedPlayerLine(away[1].Id, 3, "OnUnknownSide", 1, 2, 0), oneKnownOther);
        // Neither team known: side 1 is home, side 2 away
        var noneKnown = RecordedGame.Seed("unit/sides-3", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        noneKnown.SeedSide(1, null, Outcomes.Won, 4);
        noneKnown.SeedSide(2, null, Outcomes.Lost, 2);
        // The home team known and two other sides: which one was away can't be told
        var twoOthers = RecordedGame.Seed("unit/sides-4", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        var twoOthersHome = twoOthers.SeedSide(1, seeded.Home.TeamID, Outcomes.Won, 5);
        twoOthers.SeedSide(2, null, Outcomes.Lost, 1);
        twoOthers.SeedSide(3, null, Outcomes.Lost, 0);
        twoOthers.SeedPlayer(new RecordedPlayerLine(home[2].Id, 1, "HomeOnly", 1, 1, 0), twoOthersHome);
        Context.RecordedGames.AddRange(byHand, oneKnown, noneKnown, twoOthers);
        seeded.SeasonMatch.Game(1, recorded: byHand);
        seeded.SeasonMatch.Game(2, recorded: oneKnown);
        seeded.SeasonMatch.Game(3, recorded: noneKnown);
        seeded.SeasonMatch.Game(4, recorded: twoOthers);
        await Context.SaveChangesAsync();

        var games = (await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID))!.ReportedGames;

        Assert.That(games, Has.Length.EqualTo(4));
        using (Assert.EnterMultipleScope())
        {
            Assert.That((games[0].HomeScore, games[0].AwayScore), Is.EqualTo(((int?)5, (int?)3)));
            Assert.That(games[0].HomePlayers.Select(p => p.Gamertag), Is.EqualTo(HomeOnSideTwo));
            Assert.That(games[0].AwayPlayers.Select(p => p.Gamertag), Is.EqualTo(AwayOnSideOne));
            Assert.That((games[0].IsManual, games[0].RecordedGameID, games[0].GameVersionID, games[0].GameVersionName),
                Is.EqualTo((true, (int?)byHand.RecordedGameID, (int?)Halo3, "Halo 3")));
            Assert.That((games[0].Map, games[0].Gametype, games[0].PlayedAt), Is.EqualTo(("Foundry", "Grifball", (DateTime?)InSeason)));

            Assert.That((games[1].HomeScore, games[1].AwayScore, games[1].IsManual), Is.EqualTo(((int?)5, (int?)1, false)));
            Assert.That(games[1].HomePlayers.Select(p => p.Gamertag), Is.EqualTo(HomeKnown));
            Assert.That(games[1].AwayPlayers.Select(p => p.Gamertag), Is.EqualTo(OnUnknownSide));

            Assert.That((games[2].HomeScore, games[2].AwayScore), Is.EqualTo(((int?)4, (int?)2)));
            Assert.That(games[2].HomePlayers, Is.Empty);

            Assert.That((games[3].HomeScore, games[3].AwayScore), Is.EqualTo(((int?)5, (int?)null)));
            Assert.That(games[3].HomePlayers.Select(p => p.Gamertag), Is.EqualTo(HomeOnly));
            Assert.That(games[3].AwayPlayers, Is.Empty);
        }
    }

    [Test]
    public async Task GetSeasonMatchPage_ABracketMatch_ShowsItsSlot_AndOffersUndoOnlyWhileTheBracketWouldTakeItBack()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var (w1, w2, w3) = (b.Sm(Bracket.Winner, 1), b.Sm(Bracket.Winner, 2), b.Sm(Bracket.Winner, 3));
        await _service.AwayForfeit(w1);
        Context.ChangeTracker.Clear();

        var whileNextIsOpen = (await _service.GetSeasonMatchPage(w1))!;
        Context.ChangeTracker.Clear();
        await _service.AwayForfeit(w2);
        Context.ChangeTracker.Clear();
        await _service.AwayForfeit(w3);
        Context.ChangeTracker.Clear();
        var oncePlayed = (await _service.GetSeasonMatchPage(w1))!;
        var later = (await _service.GetSeasonMatchPage(w3))!;
        var grandFinal = (await _service.GetSeasonMatchPage(b.Sm(Bracket.GrandFinal)))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileNextIsOpen.IsPlayoff, Is.True);
            Assert.That((whileNextIsOpen.HomeTeamID, whileNextIsOpen.HomeTeamResult, whileNextIsOpen.AwayTeamID, whileNextIsOpen.AwayTeamResult),
                Is.EqualTo(((int?)b.SeedTeamIDs[0], SeasonMatchResultDto.Won, (int?)b.SeedTeamIDs[3], SeasonMatchResultDto.Forfeit)));
            Assert.That(whileNextIsOpen.BracketInfo, Is.EqualTo(new BracketInfoDto
            {
                HomeTeamSeed = 1,
                AwayTeamSeed = 4,
                HomeTeamPreviousMatchID = null,
                AwayTeamPreviousMatchID = null,
                WinnerNextMatchID = w3,
                LoserNextMatchID = b.Sm(Bracket.Loser, 1),
                IsGrandFinal = false,
            }));
            Assert.That(whileNextIsOpen.CanUndo, Is.True);
            Assert.That(oncePlayed.CanUndo, Is.False, "The match it fed has a result of its own");
            Assert.That(later.BracketInfo, Is.EqualTo(new BracketInfoDto
            {
                HomeTeamSeed = null,
                AwayTeamSeed = null,
                HomeTeamPreviousMatchID = w1,
                AwayTeamPreviousMatchID = w2,
                WinnerNextMatchID = b.Sm(Bracket.GrandFinal),
                LoserNextMatchID = b.Sm(Bracket.Loser, 2),
                IsGrandFinal = false,
            }));
            Assert.That(grandFinal.BracketInfo!.IsGrandFinal, Is.True);
            Assert.That((grandFinal.BracketInfo.WinnerNextMatchID, grandFinal.BracketInfo.LoserNextMatchID),
                Is.EqualTo(((int?)b.Sm(Bracket.GrandFinalSuddenDeath), (int?)b.Sm(Bracket.GrandFinalSuddenDeath))));
        }
    }

    #endregion

    #region Exists, PlayerXboxIDs, SeasonWindow

    [Test]
    public async Task Exists_OnlyForASeasonMatchThatIsThere()
    {
        var seeded = await SeedAsync(Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _service.Exists(seeded.SeasonMatch.SeasonMatchID), Is.True);
            Assert.That(await _service.Exists(987654), Is.False);
        }
    }

    [Test]
    public async Task PlayerXboxIDs_AreBothRostersXboxAccounts_NoneForAMatchThatIsNotThere()
    {
        var seeded = await SeedAsync(Context);
        // A rostered player without an Xbox account can't have played a Waypoint game: left out
        seeded.Home.SeedPlayer(user: User.Seed(userName: "noxbox", displayName: "No Xbox"));
        await Context.SaveChangesAsync();

        var ids = await _service.PlayerXboxIDs(seeded.SeasonMatch.SeasonMatchID);
        var none = await _service.PlayerXboxIDs(987654);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Is.EquivalentTo(HomeXbox.Concat(AwayXbox)));
            Assert.That(none, Is.Empty);
        }
    }

    [Test]
    public async Task SeasonWindow_NoSuchMatch_IsNull()
    {
        var window = await _service.SeasonWindow(987654);

        Assert.That(window, Is.Null);
    }

    [Test]
    public async Task SeasonWindow_RunsFromADayBeforeTheSeason_ToAWeekAfterIt_OrADayAfterALaterScheduledTime()
    {
        var seeded = await SeedAsync(Context);
        var withinTheWeek = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID, scheduledTime: SeasonEnd.AddDays(3));
        var pastTheWeek = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID, scheduledTime: SeasonEnd.AddDays(10));
        Context.SeasonMatches.AddRange(withinTheWeek, pastTheWeek);
        await Context.SaveChangesAsync();
        var seasonWindow = (SeasonStart.AddDays(-1), SeasonEnd.AddDays(7));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _service.SeasonWindow(seeded.SeasonMatch.SeasonMatchID), Is.EqualTo(seasonWindow), "Not scheduled");
            Assert.That(await _service.SeasonWindow(withinTheWeek.SeasonMatchID), Is.EqualTo(seasonWindow));
            Assert.That(await _service.SeasonWindow(pastTheWeek.SeasonMatchID), Is.EqualTo((SeasonStart.AddDays(-1), SeasonEnd.AddDays(11))));
        }
    }

    #endregion

    #region GetPossibleMatches

    [Test]
    public async Task GetPossibleMatches_AMatchWithoutBothTeams_HasNone()
    {
        var seeded = await SeedAsync(Context, withAwayTeam: false);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        var possible = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(possible, Is.Empty);
    }

    [Test]
    public async Task GetPossibleMatches_AreTheSeasonsDecidedGamesBetweenTheTwoRosters_NewestFirst()
    {
        var seeded = await SeedAsync(Context);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        // The home team as in-game team 1, with a ringer, beaten
        await AddMatchAsync(Context, 2, InSeason.AddHours(1), AwayXbox, Outcomes.Won, HomeThreeAndRinger, Outcomes.Lost, team0Score: 4, team1Score: 2);
        // Within the week after the season
        await AddMatchAsync(Context, 3, SeasonEnd.AddDays(6), HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        // Left out: before the day before the season, and after the week after it
        await AddMatchAsync(Context, 4, SeasonStart.AddDays(-2), HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        await AddMatchAsync(Context, 5, SeasonEnd.AddDays(8), HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        // Left out: nobody won, a team short of four, no away player
        await AddMatchAsync(Context, 6, InSeason, HomeXbox, Outcomes.Tie, AwayXbox, Outcomes.Tie);
        await AddMatchAsync(Context, 7, InSeason, HomeThree, Outcomes.Won, AwayXbox, Outcomes.Lost);
        await AddMatchAsync(Context, 8, InSeason, HomeXbox, Outcomes.Won, OtherFour, Outcomes.Lost);
        // Left out: each team mixes both rosters (a candidate of the query, but not a game between the two teams)
        await AddMatchAsync(Context, 9, InSeason, MixedFirst, Outcomes.Won, MixedLast, Outcomes.Lost);
        // Left out: already a game of a series
        var reported = await AddMatchAsync(Context, 10, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.Game(1, halo: reported);
        await Context.SaveChangesAsync();

        var possible = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(possible.Select(p => p.MatchID), Is.EqualTo(new[] { MatchID(3), MatchID(2), MatchID(1) }));
        var beaten = possible[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((beaten.HomeTeam.TeamID, beaten.HomeTeam.Score, beaten.HomeTeam.Outcome), Is.EqualTo((1, 2, Outcomes.Lost)));
            Assert.That(beaten.HomeTeam.Players.Select(p => (p.XboxUserID, p.IsOnTeam)), Is.EquivalentTo(HomeThreeAndRingerOnTeam));
            Assert.That((beaten.AwayTeam.TeamID, beaten.AwayTeam.Score, beaten.AwayTeam.Outcome), Is.EqualTo((0, 4, Outcomes.Won)));
            Assert.That(beaten.AwayTeam.Players.Single(p => p.XboxUserID == 201),
                Is.EqualTo(new PossiblePlayerDto { XboxUserID = 201, Gamertag = "GT201", Score = 1, Kills = 1, Deaths = 1, IsOnTeam = true }));
            Assert.That(beaten.AwayTeam.Players.Select(p => p.XboxUserID), Is.EquivalentTo(AwayXbox));
        }
    }

    #endregion

    #region ReportMatch

    [Test]
    public async Task ReportMatch_AMatchThePullFoundNothingOf_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, MatchID(1)));

        Assert.That(ex!.Message, Is.EqualTo("Match does not exist"));
        await _dataPull.Received(1).GetAndSaveMatch(MatchID(1));
    }

    [Test]
    public async Task ReportMatch_AMatchAlreadyInASeries_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        var match = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.Game(1, halo: match);
        await Context.SaveChangesAsync();

        var message = await ReportRefused(seeded.SeasonMatch.SeasonMatchID, MatchID(1));

        Assert.That(message, Is.EqualTo("Match is already associated with a season match"));
    }

    [Test]
    public async Task ReportMatch_AMatchNobodyWon_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Lost, AwayXbox, Outcomes.Lost);

        var message = await ReportRefused(seeded.SeasonMatch.SeasonMatchID, MatchID(1));

        Assert.That(message, Is.EqualTo("There must be a winner for the match you are reporting"));
    }

    [Test]
    public async Task ReportMatch_ForASeasonMatchThatIsNotThere_IsRefused()
    {
        await SeedAsync(Context);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        var message = await ReportRefused(987654, MatchID(1));

        Assert.That(message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task ReportMatch_ForASeasonMatchWithoutBothTeams_IsRefused()
    {
        var seeded = await SeedAsync(Context, withAwayTeam: false);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        var message = await ReportRefused(seeded.SeasonMatch.SeasonMatchID, MatchID(1));

        Assert.That(message, Is.EqualTo("You cannot report a match for a season match that does not have both teams assigned"));
    }

    [Test]
    public async Task ReportMatch_ANewGameOfADecidedSeries_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Home);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        var message = await ReportRefused(seeded.SeasonMatch.SeasonMatchID, MatchID(1));

        Assert.That(message, Is.EqualTo("Results for this match have already been decided: to correct a game, replace it"));
    }

    [Test]
    public async Task ReportMatch_ANewGameOfASeriesWithAllItsGames_IsRefused()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        var earlier = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        // Inconsistent data: a full series left undecided
        seeded.SeasonMatch.Game(1, halo: earlier);
        await AddMatchAsync(Context, 2, InSeason.AddHours(1), HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        var message = await ReportRefused(seeded.SeasonMatch.SeasonMatchID, MatchID(2));

        Assert.That(message, Is.EqualTo("This series already has all 1 of its games"));
    }

    [Test]
    public async Task ReportMatch_ReplacingAGameNotOfThisSeries_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        var message = await ReportRefused(seeded.SeasonMatch.SeasonMatchID, MatchID(1), replacesSeriesGameID: 987654);

        Assert.That(message, Is.EqualTo("That game is not one of this match's games"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ReportMatch_AGameNotBetweenTheTwoRosters_IsRefused(bool mixedTeams)
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        // Each team with players of both rosters, or both teams of home players
        await AddMatchAsync(Context, 1, InSeason, mixedTeams ? MixedFirst : HomeFirstTwo, Outcomes.Won, mixedTeams ? MixedLast : HomeLastTwo, Outcomes.Lost);
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, MatchID(1)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
            Assert.That(await Context.Set<SeriesGame>().AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task ReportMatch_TheFirstGameOfABestOf3_ScoresWithoutDeciding()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        Context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, MatchID(1));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(GamesInOrder(sm), Is.EqualTo(new[] { ((Guid?)MatchID(1), (int?)null, 1) }));
        }
        await _dataPull.Received(1).GetAndSaveMatch(MatchID(1));
    }

    [Test]
    public async Task ReportMatch_AGamePlayedBeforeOneReported_TakesItsPlaceInTheSeries()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var later = await AddMatchAsync(Context, 1, InSeason.AddHours(1), HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.Game(1, halo: later);
        seeded.SeasonMatch.Scored(1, 0);
        await Context.SaveChangesAsync();
        // The away team won the game played an hour before, as in-game team 0
        await AddMatchAsync(Context, 2, InSeason, AwayXbox, Outcomes.Won, HomeXbox, Outcomes.Lost);
        Context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, MatchID(2));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GamesInOrder(sm), Is.EqualTo(new[] { ((Guid?)MatchID(2), (int?)null, 1), ((Guid?)MatchID(1), (int?)null, 2) }));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)1, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        }
    }

    [Test]
    public async Task ReportMatch_TheDecidingGame_DecidesTheMatch()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        await AddMatchAsync(Context, 1, InSeason, AwayXbox, Outcomes.Won, HomeXbox, Outcomes.Lost);
        Context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, MatchID(1));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)0, (int?)1, (SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
    }

    [Test]
    public async Task ReportMatch_ReplacingAGameEnteredByHand_TakesItsNumber_AndDeletesIt()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var byHand = ManualGame(seeded, "unit/replaced", 1, 5);
        Context.RecordedGames.Add(byHand);
        var replaced = seeded.SeasonMatch.Game(1, recorded: byHand);
        seeded.SeasonMatch.Scored(0, 1);
        await Context.SaveChangesAsync();
        await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);

        Context.ChangeTracker.Clear();
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, MatchID(1), replaced.SeriesGameID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GamesInOrder(sm), Is.EqualTo(new[] { ((Guid?)MatchID(1), (int?)null, 1) }));
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(await Context.RecordedGames.AnyAsync(g => g.RecordedGameID == byHand.RecordedGameID), Is.False);
        }
    }

    #endregion

    #region Forfeits

    [Test]
    public async Task Forfeit_ASeasonMatchThatIsNotThere_Throws()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(987654));

        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task Forfeit_WithoutBothTeams_Throws()
    {
        var seeded = await SeedAsync(Context, withAwayTeam: false);
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(seeded.SeasonMatch.SeasonMatchID));

        Assert.That(ex!.Message, Is.EqualTo("You cannot report a match for a season match that does not have both teams assigned"));
    }

    [Test]
    public async Task Forfeit_ADecidedMatch_Throws_AndKeepsItsResult()
    {
        var seeded = await SeedAsync(Context);
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.DoubleForfeit(seeded.SeasonMatch.SeasonMatchID));

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss)));
        }
    }

    [TestCase(Forfeiter.Home, SeasonMatchResult.Forfeit, SeasonMatchResult.Won)]
    [TestCase(Forfeiter.Away, SeasonMatchResult.Won, SeasonMatchResult.Forfeit)]
    [TestCase(Forfeiter.Both, SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit)]
    public async Task Forfeit_InTheRegularSeason_DecidesTheMatch(Forfeiter forfeiter, SeasonMatchResult home, SeasonMatchResult away)
    {
        var seeded = await SeedAsync(Context);
        var id = seeded.SeasonMatch.SeasonMatchID;
        Context.ChangeTracker.Clear();

        var forfeit = forfeiter switch
        {
            Forfeiter.Home => _service.HomeForfeit(id),
            Forfeiter.Away => _service.AwayForfeit(id),
            _ => _service.DoubleForfeit(id),
        };
        await forfeit;

        var sm = await Reload(id);
        Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)home, (SeasonMatchResult?)away)));
    }

    [Test]
    public async Task HomeForfeit_InTheWinnerBracket_SendsTheAwayTeamOn_AndTheHomeTeamToTheLoserBracket()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var seeds = b.SeedTeamIDs;

        await _service.HomeForfeit(b.Sm(Bracket.Winner, 1));

        Context.ChangeTracker.Clear();
        var w1 = await Reload(b.Sm(Bracket.Winner, 1));
        var w3 = await Reload(b.Sm(Bracket.Winner, 3));
        var l1 = await Reload(b.Sm(Bracket.Loser, 1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Slots(w1), Is.EqualTo(Expect(seeds[0], SeasonMatchResult.Forfeit, seeds[3], SeasonMatchResult.Won)));
            Assert.That(w3.HomeTeamID, Is.EqualTo(seeds[3]));
            Assert.That(l1.HomeTeamID, Is.EqualTo(seeds[0]));
        }
    }

    [Test]
    public async Task DoubleForfeit_InTheWinnerBracket_GivesTheMatchesItFedAByeEach_AndTheirWaitingTeamsGoOn()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var seeds = b.SeedTeamIDs;
        // Seed 3 waits in W3, seed 2 in L1
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2));
        Context.ChangeTracker.Clear();

        await _service.DoubleForfeit(b.Sm(Bracket.Winner, 1));

        Context.ChangeTracker.Clear();
        var w1 = await Reload(b.Sm(Bracket.Winner, 1));
        var w3 = await Reload(b.Sm(Bracket.Winner, 3));
        var l1 = await Reload(b.Sm(Bracket.Loser, 1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Slots(w1), Is.EqualTo(Expect(seeds[0], SeasonMatchResult.Forfeit, seeds[3], SeasonMatchResult.Forfeit)));
            Assert.That(Slots(w3), Is.EqualTo(Expect(null, SeasonMatchResult.Bye, seeds[2], SeasonMatchResult.Won)));
            Assert.That(Slots(l1), Is.EqualTo(Expect(null, SeasonMatchResult.Bye, seeds[1], SeasonMatchResult.Won)));
        }
    }

    [Test]
    public async Task Forfeit_TheGrandFinal_IsRefused()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var grandFinalID = b.Sm(Bracket.GrandFinal);
        var grandFinal = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == grandFinalID);
        grandFinal.PlaceTeam(SeasonMatchSide.Home, b.SeedTeamIDs[0]);
        grandFinal.PlaceTeam(SeasonMatchSide.Away, b.SeedTeamIDs[1]);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.HomeForfeit(grandFinalID));

        var sm = await Reload(grandFinalID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("The grand final cannot be forfeited, report its games instead"));
            Assert.That(sm.HomeTeamResult, Is.Null);
        }
    }

    #endregion

    #region UndoResult

    [Test]
    public async Task UndoResult_ASeasonMatchThatIsNotThere_Throws()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.UndoResult(987654));

        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task UndoResult_AWinOnABye_IsRefused()
    {
        var seeded = await SeedAsync(Context, withAwayTeam: false);
        seeded.SeasonMatch.WonOnBye(SeasonMatchSide.Away);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.UndoResult(seeded.SeasonMatch.SeasonMatchID));

        Assert.That(ex!.Message, Is.EqualTo("This match has a BYE, so its result is decided by the bracket and cannot be undone"));
    }

    [Test]
    public async Task UndoResult_WithNothingToUndo_IsRefused()
    {
        var seeded = await SeedAsync(Context);
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.UndoResult(seeded.SeasonMatch.SeasonMatchID));

        Assert.That(ex!.Message, Is.EqualTo("This match has no result to undo"));
    }

    [Test]
    public async Task UndoResult_OfASeriesItsGamesDecided_ClearsTheGames_FreesTheWaypointOne_AndDeletesTheOneEnteredByHand()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        var byHand = ManualGame(seeded, "unit/undo", 5, 2, InSeason.AddHours(1));
        Context.RecordedGames.Add(byHand);
        seeded.SeasonMatch.Game(1, halo: waypoint);
        seeded.SeasonMatch.Game(2, recorded: byHand);
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        var waypointID = MatchID(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)null, (int?)null, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(sm.SeriesGames, Is.Empty);
            Assert.That(await Context.RecordedGames.AnyAsync(g => g.RecordedGameID == byHand.RecordedGameID), Is.False);
            Assert.That(await Context.Matches.AnyAsync(m => m.MatchID == waypointID), Is.True, "Free to be reported again");
        }
    }

    [Test]
    public async Task UndoResult_OfAForfeit_KeepsTheGamesPlayedBeforeIt()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var byHand = ManualGame(seeded, "unit/kept", 5, 2);
        Context.RecordedGames.Add(byHand);
        seeded.SeasonMatch.Game(1, recorded: byHand);
        seeded.SeasonMatch.Scored(1, 0);
        seeded.SeasonMatch.Forfeited(Forfeiter.Away);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScoreAndResults(sm), Is.EqualTo(((int?)1, (int?)0, (SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(GamesInOrder(sm), Is.EqualTo(new[] { ((Guid?)null, (int?)byHand.RecordedGameID, 1) }));
        }
    }

    [Test]
    public async Task UndoResult_InTheBracket_TakesBothTeamsBackOutOfTheMatchesItFed()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var seeds = b.SeedTeamIDs;
        var w1ID = b.Sm(Bracket.Winner, 1);
        await _service.AwayForfeit(w1ID);
        Context.ChangeTracker.Clear();

        await _service.UndoResult(w1ID);

        Context.ChangeTracker.Clear();
        var w1 = await Reload(w1ID);
        var w3 = await Reload(b.Sm(Bracket.Winner, 3));
        var l1 = await Reload(b.Sm(Bracket.Loser, 1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Slots(w1), Is.EqualTo(Expect(seeds[0], null, seeds[3], null)));
            Assert.That(w3.HomeTeamID, Is.Null);
            Assert.That(l1.HomeTeamID, Is.Null);
        }
    }

    [Test]
    public async Task UndoResult_InTheBracket_IsRefused_OnceAMatchItFedHasAResultOfItsOwn()
    {
        var b = await SeedDoubleEliminationAsync(Context, _bracketService);
        var (w1, w3) = (b.Sm(Bracket.Winner, 1), b.Sm(Bracket.Winner, 3));
        await _service.AwayForfeit(w1);
        Context.ChangeTracker.Clear();
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2));
        Context.ChangeTracker.Clear();
        await _service.AwayForfeit(w3);
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.UndoResult(w1));

        var sm = await Reload(w1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo($"Season match {w3} that this result fed into already has a result of its own. Undo that match first."));
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
        }
    }

    #endregion
}
