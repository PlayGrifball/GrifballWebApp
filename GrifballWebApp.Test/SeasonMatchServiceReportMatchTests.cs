using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        _service = new SeasonMatchService(_context, _dataPullService, _bracketService, new GrifballWebApp.Server.Championships.ChampionshipService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<SeasonMatch> Reload(int id)
    {
        await using var ctx = _context.NewContextLike();
        return await ctx.SeasonMatches.Include(x => x.SeriesGames).AsNoTracking().SingleAsync(x => x.SeasonMatchID == id);
    }

    [Test]
    public async Task ReportMatch_Throws_When_MatchDoesNotExist_AfterPullingIt()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var matchID = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, matchID));

        Assert.That(ex!.Message, Is.EqualTo("Match does not exist"));
        await _dataPullService.Received(1).GetAndSaveMatch(matchID);
    }

    [Test]
    public async Task ReportMatch_DisposesTransaction_When_Throws()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var matchID = Guid.NewGuid();

        await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, matchID));

        Assert.That(_context.Database.CurrentTransaction, Is.Null);
    }

    [Test]
    public async Task ReportMatch_Throws_When_MatchAlreadyLinked()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 });
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Match is already associated with a season match"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_NoTeamWon()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Tie, SeasonMatchTestData.AwayXbox, Outcomes.Tie);

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("There must be a winner for the match you are reporting"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SeasonMatchDoesNotExist()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(99999, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SeasonMatchMissingTeam()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var tbd = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, null, bestOf: 1);
        _context.SeasonMatches.Add(tbd);
        await _context.SaveChangesAsync();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(tbd.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Does.Contain("does not have both teams assigned"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_ResultsAlreadyDecided()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        seeded.SeasonMatch.WonBy(SeasonMatchSide.Away);
        await _context.SaveChangesAsync();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Does.StartWith("Results for this match have already been decided"));
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
            Assert.That(sm.SeriesGames.Single().MatchID, Is.EqualTo(match.MatchID));
            Assert.That(sm.SeriesGames.Single().GameNumber, Is.EqualTo(1));
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
            Assert.That(sm.SeriesGames.Single(x => x.MatchID == early.MatchID).GameNumber, Is.EqualTo(1));
            Assert.That(sm.SeriesGames.Single(x => x.MatchID == late.MatchID).GameNumber, Is.EqualTo(2));
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
        _context.SeriesGames.Add(new SeriesGame { MatchID = first.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 });
        await _context.SaveChangesAsync();
        var second = await SeasonMatchTestData.AddMatchAsync(_context, start.AddMinutes(20), SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, second.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("This series already has all 1 of its games"));
        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(sm.SeriesGames, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReportMatch_Throws_When_PlayersDoNotMatchTeams()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var other = SeasonMatchTestData.OtherXbox;
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, other[..4], Outcomes.Won, other[4..], Outcomes.Lost);

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

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

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_OneTeamHasNoKnownPlayers()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.OtherXbox[..4], Outcomes.Lost);

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SecondTeamIsMixed()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var h = SeasonMatchTestData.HomeXbox;
        var a = SeasonMatchTestData.AwayXbox;
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [h[0], h[1], h[2], SeasonMatchTestData.OtherXbox[0]], Outcomes.Won, [h[3], a[0], a[1], a[2]], Outcomes.Lost);

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_RosteredPlayerWithoutAGamertag_IsLeftOut()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.TeamPlayers.Add(TeamPlayer.Seed(teamID: seeded.Home.TeamID, user: new User { UserName = "nogamertag" }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
    }

    /// <summary>Another commissioner's change to the same series saved first: a save with a series game in the given
    /// state is refused, as the series' unique game numbers refuse it.</summary>
    private sealed class AnotherChangeGotInFirst(EntityState state) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<SeriesGame>().Any(e => e.State == state))
                throw new DbUpdateException("Cannot insert duplicate key row in object 'Event.SeriesGames' with unique index 'IX_SeriesGames_SeasonMatchID_GameNumber'");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Runs a test on a context whose saves of a series game in <paramref name="state"/> collide.</summary>
    private static async Task Colliding(EntityState state, Func<GrifballContext, SeasonMatchService, Task> test)
    {
        var context = await SetUpFixture.NewGrifballContext(new AnotherChangeGotInFirst(state));
        try
        {
            var service = new SeasonMatchService(context, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(),
                new GrifballWebApp.Server.Championships.ChampionshipService(context));
            await test(context, service);
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task ReportMatch_BeatenToItsGameNumber_ByAnotherReport_IsRefused_NotAServerError()
    {
        await Colliding(EntityState.Added, async (context, service) =>
        {
            var seeded = await SeasonMatchTestData.SeedAsync(context, bestOf: 3);
            var match = await SeasonMatchTestData.AddMatchAsync(context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
            context.ChangeTracker.Clear();

            var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

            Assert.Multiple(() =>
            {
                Assert.That(ex!.Message, Is.EqualTo(SeasonMatchService.SeriesChangedMessage));
                Assert.That(context.NewContextLike().SeriesGames.Any(), Is.False, "Nothing was saved");
            });
        });
    }

    [Test]
    public async Task ReportMatch_ReplacingAGame_AnotherChangeGotInFirst_IsRefused_NotAServerError()
    {
        await Colliding(EntityState.Deleted, async (context, service) =>
        {
            var seeded = await SeasonMatchTestData.SeedAsync(context, bestOf: 3);
            var first = await SeasonMatchTestData.AddMatchAsync(context, DateTime.UtcNow.AddHours(-1), SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
            var replacement = await SeasonMatchTestData.AddMatchAsync(context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
            var game = new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchID = first.MatchID, GameNumber = 1 };
            context.SeriesGames.Add(game);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            // The game replaced is taken out (saved) before the new one takes its number: that save collides
            var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(
                () => service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, replacement.MatchID, replacesSeriesGameID: game.SeriesGameID));

            Assert.Multiple(() =>
            {
                Assert.That(ex!.Message, Is.EqualTo(SeasonMatchService.SeriesChangedMessage));
                Assert.That(context.NewContextLike().SeriesGames.Select(g => g.MatchID), Is.EqualTo(new Guid?[] { first.MatchID }), "The series is as it was");
            });
        });
    }
}
