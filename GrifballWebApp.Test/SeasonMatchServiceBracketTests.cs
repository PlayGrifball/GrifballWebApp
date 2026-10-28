using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchServiceBracketTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private BracketService _bracketService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        // Real bracket service so that DetermineNextMatches runs against the entities loaded by SeasonMatchService
        _bracketService = new BracketService(_context, new TeamStandingsService(_context));
        _service = new SeasonMatchService(_context, _dataPullService, _bracketService);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private record Bracket3(SeasonMatchTestData.Seeded Seeded, SeasonMatch Next, SeasonMatch LoserNext, MatchBracketInfo Info);

    /// <summary>
    /// Seeded match is a winner bracket round 1 game (seeds 1 vs 2). Winner goes to the HOME slot of a winner bracket game,
    /// loser goes to the AWAY slot of a loser bracket game.
    /// </summary>
    private async Task<Bracket3> SeedWinnerBracket(Bracket bracket = Bracket.Winner, Bracket nextBracket = Bracket.Winner, Bracket loserBracket = Bracket.Loser, bool winnerIntoAway = false)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var info = new MatchBracketInfo
        {
            SeasonMatchID = seeded.SeasonMatch.SeasonMatchID,
            RoundNumber = 1, MatchNumber = 1,
            HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2,
            Bracket = bracket,
        };
        _context.Add(info);
        await _context.SaveChangesAsync();

        var next = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        var loserNext = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        _context.SeasonMatches.AddRange(next, loserNext);
        await _context.SaveChangesAsync();

        var nextInfo = new MatchBracketInfo
        {
            SeasonMatchID = next.SeasonMatchID, RoundNumber = 2, MatchNumber = 2, Bracket = nextBracket,
            HomeTeamPreviousMatchBracketInfoID = winnerIntoAway ? null : info.MatchBracketInfoID,
            HomeTeamSeedNumber = winnerIntoAway ? 3 : null,
            AwayTeamPreviousMatchBracketInfoID = winnerIntoAway ? info.MatchBracketInfoID : null,
            AwayTeamSeedNumber = winnerIntoAway ? null : 3,
        };
        var loserInfo = new MatchBracketInfo
        {
            SeasonMatchID = loserNext.SeasonMatchID, RoundNumber = 1, MatchNumber = 3, Bracket = loserBracket,
            HomeTeamSeedNumber = 4,
            AwayTeamPreviousMatchBracketInfoID = info.MatchBracketInfoID,
        };
        _context.AddRange(nextInfo, loserInfo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return new Bracket3(seeded, next, loserNext, info);
    }

    private async Task<SeasonMatch> Load(int id)
    {
        await using var ctx = _context.NewContextLike();
        return await ctx.SeasonMatches.AsNoTracking().SingleAsync(x => x.SeasonMatchID == id);
    }

    [Test]
    public async Task HomeForfeit_InWinnerBracket_AdvancesAwayTeam_AndDropsHomeToLoserBracket()
    {
        var b = await SeedWinnerBracket();

        await _service.HomeForfeit(b.Seeded.SeasonMatch.SeasonMatchID);

        var next = await Load(b.Next.SeasonMatchID);
        var loserNext = await Load(b.LoserNext.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(next.HomeTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
            Assert.That(next.AwayTeamID, Is.Null);
            Assert.That(loserNext.AwayTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(loserNext.HomeTeamID, Is.Null);
        });
    }

    [Test]
    public async Task AwayForfeit_InWinnerBracket_WinnerIntoAwaySlot()
    {
        var b = await SeedWinnerBracket(winnerIntoAway: true);

        await _service.AwayForfeit(b.Seeded.SeasonMatch.SeasonMatchID);

        var next = await Load(b.Next.SeasonMatchID);
        var loserNext = await Load(b.LoserNext.SeasonMatchID);
        var sm = await Load(b.Seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(next.AwayTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(next.HomeTeamID, Is.Null);
            Assert.That(loserNext.AwayTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
        });
    }

    [Test]
    public async Task ReportMatch_DecidingGameInWinnerBracket_AdvancesTeams()
    {
        var b = await SeedWinnerBracket();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(b.Seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var next = await Load(b.Next.SeasonMatchID);
        var loserNext = await Load(b.LoserNext.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(next.HomeTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(loserNext.AwayTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
        });
    }

    /// <summary>
    /// Seeded match is a grand final. Its winner goes to the home slot of the sudden death, its loser to the away slot.
    /// </summary>
    private async Task<(SeasonMatchTestData.Seeded Seeded, SeasonMatch SuddenDeath)> SeedGrandFinal()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var gf = new MatchBracketInfo { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, RoundNumber = 1, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.GrandFinal };
        _context.Add(gf);
        var sd = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        _context.SeasonMatches.Add(sd);
        await _context.SaveChangesAsync();
        _context.Add(new MatchBracketInfo
        {
            SeasonMatchID = sd.SeasonMatchID, RoundNumber = 2, MatchNumber = 2, Bracket = Bracket.GrandFinalSuddenDeath,
            HomeTeamPreviousMatchBracketInfoID = gf.MatchBracketInfoID, AwayTeamPreviousMatchBracketInfoID = gf.MatchBracketInfoID,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return (seeded, sd);
    }

    [Test]
    public async Task HomeWinsGrandFinal_DoesNotFillSuddenDeath()
    {
        var (seeded, sd) = await SeedGrandFinal();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sdLoaded = await Load(sd.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sdLoaded.HomeTeamID, Is.Null);
            Assert.That(sdLoaded.AwayTeamID, Is.Null);
        });
    }

    [Test]
    public async Task AwayWinsGrandFinal_FillsSuddenDeath()
    {
        var (seeded, sd) = await SeedGrandFinal();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Lost, SeasonMatchTestData.AwayXbox, Outcomes.Won);
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sdLoaded = await Load(sd.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sdLoaded.HomeTeamID, Is.EqualTo(seeded.Away.TeamID));
            Assert.That(sdLoaded.AwayTeamID, Is.EqualTo(seeded.Home.TeamID));
        });
    }

    [Test]
    public async Task Forfeits_AreRejected_ForTheGrandFinalAndSuddenDeath(
        [Values(Bracket.GrandFinal, Bracket.GrandFinalSuddenDeath)] Bracket bracket,
        [Values("Home", "Away", "Double")] string forfeit)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.Add(new MatchBracketInfo { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, RoundNumber = 3, MatchNumber = 5, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = bracket });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var id = seeded.SeasonMatch.SeasonMatchID;

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => forfeit switch
        {
            "Home" => _service.HomeForfeit(id),
            "Away" => _service.AwayForfeit(id),
            _ => _service.DoubleForfeit(id),
        });

        var sm = await Load(id);
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Is.EqualTo("The grand final cannot be forfeited, report its games instead"));
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        });
    }

    [Test]
    public async Task HomeForfeit_LoserDropsOppositeABye_GoesStraightToTheNextLoserMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var third = new Team { SeasonID = seeded.Season.SeasonID, TeamName = "Third Team" };
        _context.Teams.Add(third);
        await _context.SaveChangesAsync();
        // Three teams: W1 = seed 1 vs BYE, W2 = seed 3 vs seed 2, L1 = BYE (W1 has no loser) vs loser of W2
        await _bracketService.CreateBracket(3, seeded.Season.SeasonID, doubleElimination: true, bestOf: 1);
        await _bracketService.SetSeeds(seeded.Season.SeasonID, new CustomSeedDto[]
        {
            new() { Seed = 1, TeamID = third.TeamID },
            new() { Seed = 2, TeamID = seeded.Away.TeamID },
            new() { Seed = 3, TeamID = seeded.Home.TeamID },
        });
        var infos = await _context.MatchBracketInfo.AsNoTracking().Where(x => x.SeasonMatch.SeasonID == seeded.Season.SeasonID).ToListAsync();
        int Sm(Bracket kind, int number) => infos.Single(x => x.Bracket == kind && x.MatchNumber == number).SeasonMatchID;
        _context.ChangeTracker.Clear();

        await _service.HomeForfeit(Sm(Bracket.Winner, 2)); // seed 3 forfeits to seed 2

        var final = await Load(Sm(Bracket.Winner, 3));
        var l1 = await Load(Sm(Bracket.Loser, 1));
        var l2 = await Load(Sm(Bracket.Loser, 2));
        Assert.Multiple(() =>
        {
            Assert.That((final.HomeTeamID, final.AwayTeamID), Is.EqualTo(((int?)third.TeamID, (int?)seeded.Away.TeamID)));
            Assert.That((l1.HomeTeamResult, l1.AwayTeamID, l1.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Bye, (int?)seeded.Home.TeamID, (SeasonMatchResult?)SeasonMatchResult.Won)));
            // L1 has a BYE, so the team that dropped into it goes straight on to L2
            Assert.That((l2.HomeTeamID, l2.AwayTeamID), Is.EqualTo(((int?)null, (int?)seeded.Home.TeamID)));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_ForPlayoffMatch_ReturnsBracketInfo()
    {
        var b = await SeedWinnerBracket();

        var page = await _service.GetSeasonMatchPage(b.Seeded.SeasonMatch.SeasonMatchID);

        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.IsPlayoff, Is.True);
            Assert.That(page.HomeTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(page.AwayTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
            Assert.That(page.BracketInfo, Is.Not.Null);
            Assert.That(page.BracketInfo!.HomeTeamSeed, Is.EqualTo(1));
            Assert.That(page.BracketInfo.AwayTeamSeed, Is.EqualTo(2));
            Assert.That(page.BracketInfo.HomeTeamPreviousMatchID, Is.Null);
            Assert.That(page.BracketInfo.AwayTeamPreviousMatchID, Is.Null);
            Assert.That(page.BracketInfo.WinnerNextMatchID, Is.EqualTo(b.Next.SeasonMatchID));
            Assert.That(page.BracketInfo.LoserNextMatchID, Is.EqualTo(b.LoserNext.SeasonMatchID));
            Assert.That(page.BracketInfo.IsGrandFinal, Is.False);
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_ForLaterRound_ReturnsPreviousMatchIDs()
    {
        var b = await SeedWinnerBracket();

        var page = await _service.GetSeasonMatchPage(b.Next.SeasonMatchID);

        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.IsPlayoff, Is.True);
            Assert.That(page.HomeTeamName, Is.Null);
            Assert.That(page.AwayTeamID, Is.Null);
            Assert.That(page.BracketInfo!.HomeTeamSeed, Is.Null);
            Assert.That(page.BracketInfo.AwayTeamSeed, Is.EqualTo(3));
            Assert.That(page.BracketInfo.HomeTeamPreviousMatchID, Is.EqualTo(b.Seeded.SeasonMatch.SeasonMatchID));
            Assert.That(page.BracketInfo.AwayTeamPreviousMatchID, Is.Null);
            Assert.That(page.BracketInfo.WinnerNextMatchID, Is.Null);
            Assert.That(page.BracketInfo.LoserNextMatchID, Is.Null);
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_IncludesActiveRescheduleRequest()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var user = await _context.Users.FirstAsync();
        var reschedule = new MatchReschedule { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, Reason = "busy", RequestedByUserID = user.Id };
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();
        seeded.SeasonMatch.ActiveRescheduleRequestId = reschedule.MatchRescheduleID;
        await _context.SaveChangesAsync();

        var page = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(page!.ActiveRescheduleRequestId, Is.EqualTo(reschedule.MatchRescheduleID));
        Assert.That(page.BracketInfo, Is.Null);
    }

    private record CreatedBracket(int SeasonID, int[] TeamBySeed, Func<Bracket, int, int> Sm);

    /// <summary>
    /// Creates a real bracket for <paramref name="teamCount"/> teams, seeded in order: seed 1 is the first team.
    /// <see cref="CreatedBracket.Sm"/> finds a match's SeasonMatchID by bracket and match number.
    /// </summary>
    private async Task<CreatedBracket> CreateBracket(int teamCount, bool doubleElimination)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var teams = new List<Team> { seeded.Home, seeded.Away };
        for (var i = teams.Count + 1; i <= teamCount; i++)
            teams.Add(new Team { SeasonID = seeded.Season.SeasonID, TeamName = $"Team {i}" });
        _context.Teams.AddRange(teams.Skip(2));
        await _context.SaveChangesAsync();

        await _bracketService.CreateBracket(teamCount, seeded.Season.SeasonID, doubleElimination, bestOf: 1);
        await _bracketService.SetSeeds(seeded.Season.SeasonID, teams.Select((t, i) => new CustomSeedDto { Seed = i + 1, TeamID = t.TeamID }).ToArray());
        var infos = await _context.MatchBracketInfo.AsNoTracking().Where(x => x.SeasonMatch.SeasonID == seeded.Season.SeasonID).ToListAsync();
        _context.ChangeTracker.Clear();
        return new CreatedBracket(seeded.Season.SeasonID, teams.Select(t => t.TeamID).ToArray(),
            (kind, number) => infos.Single(x => x.Bracket == kind && x.MatchNumber == number).SeasonMatchID);
    }

    private static (int? HomeTeamID, SeasonMatchResult? HomeTeamResult, int? AwayTeamID, SeasonMatchResult? AwayTeamResult) Slots(SeasonMatch sm) =>
        (sm.HomeTeamID, sm.HomeTeamResult, sm.AwayTeamID, sm.AwayTeamResult);

    private static (int?, SeasonMatchResult?, int?, SeasonMatchResult?) Expect(int? homeTeamID, SeasonMatchResult? homeTeamResult, int? awayTeamID, SeasonMatchResult? awayTeamResult) =>
        (homeTeamID, homeTeamResult, awayTeamID, awayTeamResult);

    private const SeasonMatchResult Won = SeasonMatchResult.Won;
    private const SeasonMatchResult Forfeit = SeasonMatchResult.Forfeit;
    private const SeasonMatchResult Bye = SeasonMatchResult.Bye;

    // Four teams, double elimination: W1 = seed 1 vs seed 4, W2 = seed 3 vs seed 2, W3 = winners of W1 and W2,
    // L1 = losers of W1 and W2, L2 = loser of W3 vs winner of L1, GF = winner of W3 vs winner of L2, then SD.

    [Test]
    public async Task DoubleForfeit_InWinnerBracket_AdvancesBothWaitingOpponents()
    {
        var b = await CreateBracket(4, doubleElimination: true);
        var seed = b.TeamBySeed;
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2)); // seed 3 waits in W3, seed 2 waits in L1

        await _service.DoubleForfeit(b.Sm(Bracket.Winner, 1));

        Assert.That(Slots(await Load(b.Sm(Bracket.Winner, 1))), Is.EqualTo(Expect(seed[0], Forfeit, seed[3], Forfeit)));
        Assert.That(Slots(await Load(b.Sm(Bracket.Winner, 3))), Is.EqualTo(Expect(null, Bye, seed[2], Won)));
        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 1))), Is.EqualTo(Expect(null, Bye, seed[1], Won)));
        // W3 had no loser, so seed 2 also walks through L2
        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 2))), Is.EqualTo(Expect(null, Bye, seed[1], Won)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinal, 4))), Is.EqualTo(Expect(seed[2], null, seed[1], null)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinalSuddenDeath, 5))), Is.EqualTo(Expect(null, null, null, null)));
    }

    [Test]
    public async Task DoubleForfeit_InLoserBracket_AdvancesTheWaitingOpponent()
    {
        var b = await CreateBracket(4, doubleElimination: true);
        var seed = b.TeamBySeed;
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 1));
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2));
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 3)); // seed 1 to the grand final, seed 3 waits in L2

        await _service.DoubleForfeit(b.Sm(Bracket.Loser, 1)); // seed 4 and seed 2

        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 1))), Is.EqualTo(Expect(seed[3], Forfeit, seed[1], Forfeit)));
        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 2))), Is.EqualTo(Expect(seed[2], Won, null, Bye)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinal, 4))), Is.EqualTo(Expect(seed[0], null, seed[2], null)));
    }

    [Test]
    public async Task DoubleForfeit_InLoserFinal_GrandFinalGoesToTheWinnerBracketChampion_WithoutSuddenDeath()
    {
        var b = await CreateBracket(4, doubleElimination: true);
        var seed = b.TeamBySeed;
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 1));
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2));
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 3));
        await _service.AwayForfeit(b.Sm(Bracket.Loser, 1)); // L2 = seed 3 vs seed 4

        await _service.DoubleForfeit(b.Sm(Bracket.Loser, 2));

        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinal, 4))), Is.EqualTo(Expect(seed[0], Won, null, Bye)));
        // The winner bracket champion never lost, so there is nothing for a sudden death to replay
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinalSuddenDeath, 5))), Is.EqualTo(Expect(null, null, null, null)));
    }

    [Test]
    public async Task DoubleForfeit_InWinnerFinal_GrandFinalGoesToTheLoserBracketChampion_WithoutSuddenDeath()
    {
        var b = await CreateBracket(4, doubleElimination: true);
        var seed = b.TeamBySeed;
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 1));
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2));

        await _service.DoubleForfeit(b.Sm(Bracket.Winner, 3)); // seed 1 and seed 3
        await _service.AwayForfeit(b.Sm(Bracket.Loser, 1)); // seed 4 beats seed 2

        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 2))), Is.EqualTo(Expect(null, Bye, seed[3], Won)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinal, 4))), Is.EqualTo(Expect(null, Bye, seed[3], Won)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinalSuddenDeath, 5))), Is.EqualTo(Expect(null, null, null, null)));
    }

    [Test]
    public async Task DoubleForfeit_ChainsThroughMatchesThatAlreadyHaveABye()
    {
        // Three teams: W1 = seed 1 vs BYE, W2 = seed 3 vs seed 2, L1 = BYE (W1 has no loser) vs loser of W2
        var b = await CreateBracket(3, doubleElimination: true);
        var seed = b.TeamBySeed;

        await _service.DoubleForfeit(b.Sm(Bracket.Winner, 2));

        Assert.That(Slots(await Load(b.Sm(Bracket.Winner, 3))), Is.EqualTo(Expect(seed[0], Won, null, Bye)));
        // Both of L1's slots are BYEs now, so a BYE goes on to L2, which then sends one to the grand final
        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 1))), Is.EqualTo(Expect(null, Bye, null, Bye)));
        Assert.That(Slots(await Load(b.Sm(Bracket.Loser, 2))), Is.EqualTo(Expect(null, Bye, null, Bye)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinal, 4))), Is.EqualTo(Expect(seed[0], Won, null, Bye)));
        Assert.That(Slots(await Load(b.Sm(Bracket.GrandFinalSuddenDeath, 5))), Is.EqualTo(Expect(null, null, null, null)));
    }

    [Test]
    public async Task DoubleForfeit_InSingleEliminationFinal_LeavesNoChampion()
    {
        var b = await CreateBracket(4, doubleElimination: false);
        var seed = b.TeamBySeed;
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 1));
        await _service.AwayForfeit(b.Sm(Bracket.Winner, 2));

        await _service.DoubleForfeit(b.Sm(Bracket.Winner, 3));

        Assert.That(Slots(await Load(b.Sm(Bracket.Winner, 3))), Is.EqualTo(Expect(seed[0], Forfeit, seed[2], Forfeit)));
    }

    [Test]
    public async Task DoubleForfeit_InRegularSeason_GivesBothTeamsALoss()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.ChangeTracker.Clear();

        await _service.DoubleForfeit(seeded.SeasonMatch.SeasonMatchID);

        await using var standingsContext = _context.NewContextLike();
        var standings = await new TeamStandingsService(standingsContext).GetTeamStandings(seeded.Season.SeasonID);
        Assert.That(Slots(await Load(seeded.SeasonMatch.SeasonMatchID)), Is.EqualTo(Expect(seeded.Home.TeamID, Forfeit, seeded.Away.TeamID, Forfeit)));
        Assert.That(standings.Select(x => (x.Wins, x.Losses)), Is.EqualTo(new[] { (0, 1), (0, 1) }));
    }

    [Test]
    public async Task GetSeasonMatchPage_FlagsTheGrandFinal()
    {
        var (seeded, sd) = await SeedGrandFinal();

        var gf = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);
        var suddenDeath = await _service.GetSeasonMatchPage(sd.SeasonMatchID);

        Assert.Multiple(() =>
        {
            Assert.That(gf!.BracketInfo!.IsGrandFinal, Is.True);
            Assert.That(suddenDeath!.BracketInfo!.IsGrandFinal, Is.True);
        });
    }
}
