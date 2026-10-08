using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchServiceBracketTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        // Real bracket service so that DetermineNextMatches runs against the entities loaded by SeasonMatchService
        var bracketService = new BracketService(_context, new TeamStandingsService(_context));
        _service = new SeasonMatchService(_context, _dataPullService, bracketService);
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

    [Test]
    public async Task HomeWinsGrandFinal_DoesNotFillSuddenDeath()
    {
        // Grand final: winner -> home of sudden death, loser -> away of sudden death
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

        await _service.AwayForfeit(seeded.SeasonMatch.SeasonMatchID); // home team wins

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

        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID); // away team wins

        var sdLoaded = await Load(sd.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sdLoaded.HomeTeamID, Is.EqualTo(seeded.Away.TeamID));
            Assert.That(sdLoaded.AwayTeamID, Is.EqualTo(seeded.Home.TeamID));
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
}
