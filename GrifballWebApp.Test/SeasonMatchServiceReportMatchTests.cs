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
public class SeasonMatchServiceReportMatchTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private IBracketService _bracketService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _bracketService = Substitute.For<IBracketService>();
        _service = new SeasonMatchService(_context, _dataPullService, _bracketService);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<SeasonMatch> Reload(int id)
    {
        await using var ctx = _context.NewContextLike();
        return await ctx.SeasonMatches.Include(x => x.MatchLinks).AsNoTracking().SingleAsync(x => x.SeasonMatchID == id);
    }

    [Test]
    public async Task ReportMatch_Throws_When_MatchDoesNotExist_AfterPullingIt()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var matchID = Guid.NewGuid();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, matchID));

        Assert.That(ex!.Message, Is.EqualTo("Match does not exist"));
        await _dataPullService.Received(1).GetAndSaveMatch(matchID);
    }

    [Test]
    public async Task ReportMatch_Throws_When_MatchAlreadyLinked()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.MatchLinks.Add(new MatchLink { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        await _context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Match is already associated with a season match"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_NoTeamWon()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Tie, SeasonMatchTestData.AwayXbox, Outcomes.Tie);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("There must be a winner for the match you are reporting"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SeasonMatchDoesNotExist()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(99999, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SeasonMatchMissingTeam()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var tbd = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = null, BestOf = 1 };
        _context.SeasonMatches.Add(tbd);
        await _context.SaveChangesAsync();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(tbd.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Does.Contain("does not have both teams assigned"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_ResultsAlreadyDecided()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        seeded.SeasonMatch.AwayTeamResult = SeasonMatchResult.Won;
        await _context.SaveChangesAsync();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
    }

    [Test]
    public async Task ReportMatch_FirstGameOfBestOf3_UpdatesScoreWithoutDecidingResult()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        // Home players on team 1 to verify the home/away detection does not depend on team index
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.AwayXbox, Outcomes.Lost, SeasonMatchTestData.HomeXbox, Outcomes.Won);

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamScore, Is.EqualTo(1));
            Assert.That(sm.AwayTeamScore, Is.EqualTo(0));
            Assert.That(sm.HomeTeamResult, Is.Null);
            Assert.That(sm.AwayTeamResult, Is.Null);
            Assert.That(sm.MatchLinks.Single().MatchID, Is.EqualTo(match.MatchID));
            Assert.That(sm.MatchLinks.Single().MatchNumber, Is.EqualTo(1));
        });
        _bracketService.DidNotReceiveWithAnyArgs().DetermineNextMatches(default!);
    }

    [Test]
    public async Task ReportMatch_HomeWinsTwo_DecidesHomeWin_AndNumbersGamesByStartTime()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var start = new DateTime(2025, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        var early = await SeasonMatchTestData.AddMatchAsync(_context, start, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        var late = await SeasonMatchTestData.AddMatchAsync(_context, start.AddMinutes(30), SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        // Report the later game first; numbering must follow start time, not report order
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, late.MatchID);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, early.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamScore, Is.EqualTo(2));
            Assert.That(sm.AwayTeamScore, Is.EqualTo(0));
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Loss));
            Assert.That(sm.MatchLinks.Single(x => x.MatchID == early.MatchID).MatchNumber, Is.EqualTo(1));
            Assert.That(sm.MatchLinks.Single(x => x.MatchID == late.MatchID).MatchNumber, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ReportMatch_AwayWinsBestOf1_DecidesAwayWin()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Lost, SeasonMatchTestData.AwayXbox, Outcomes.Won);

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamScore, Is.EqualTo(0));
            Assert.That(sm.AwayTeamScore, Is.EqualTo(1));
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Loss));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Won));
        });
    }

    [Test]
    public async Task ReportMatch_Throws_When_ExceedingBestOf()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var start = DateTime.UtcNow;
        var first = await SeasonMatchTestData.AddMatchAsync(_context, start, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        // Linked but results never set (inconsistent data)
        _context.MatchLinks.Add(new MatchLink { MatchID = first.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        await _context.SaveChangesAsync();
        var second = await SeasonMatchTestData.AddMatchAsync(_context, start.AddMinutes(20), SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, second.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Cannot go over max number of matches. Something is wrong"));
        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(sm.MatchLinks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReportMatch_Throws_When_PlayersDoNotMatchTeams()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var other = SeasonMatchTestData.OtherXbox;
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, other[..4], Outcomes.Won, other[4..], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_TeamsAreMixed()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var h = SeasonMatchTestData.HomeXbox;
        var a = SeasonMatchTestData.AwayXbox;
        // Team 0 has two home and two away players => cannot determine which side is which
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [h[0], h[1], a[0], a[1]], Outcomes.Won, [h[2], h[3], a[2], a[3]], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_OneTeamHasNoKnownPlayers()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.OtherXbox[..4], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SecondTeamIsMixed()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var h = SeasonMatchTestData.HomeXbox;
        var a = SeasonMatchTestData.AwayXbox;
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [h[0], h[1], h[2], SeasonMatchTestData.OtherXbox[0]], Outcomes.Won, [h[3], a[0], a[1], a[2]], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }
}
