using GrifballWebApp.Database;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Controllers;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using System.Collections;
using System.Reflection;
using Match = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchControllerTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _controller = new SeasonMatchController(new SeasonMatchService(_context, _dataPullService, Substitute.For<IBracketService>(), new GrifballWebApp.Server.Championships.ChampionshipService(_context)));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetSeasonMatchPage_ReturnsNull_When_Missing()
    {
        Assert.That(await _controller.GetSeasonMatchPage(1234, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetSeasonMatchPage_ReturnsPage()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var page = await _controller.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(page!.HomeTeamName, Is.EqualTo("Home Team"));
            // Undecided and without games: the model's rules say so, the page does not work them out
            Assert.That((page.DecidedBy, page.CanUndo), Is.EqualTo(((SeasonMatchDecisionDto?)null, false)));
        });
    }

    // ---------- CanRequestReschedule: who sees the Reschedule button ----------

    private async Task<bool> CanRequest(int seasonMatchID, int? userID)
    {
        if (userID is not null)
            _controller.WithUser(userID.Value.ToString());
        return (await _controller.GetSeasonMatchPage(seasonMatchID, CancellationToken.None))!.CanRequestReschedule;
    }

    [Test]
    public async Task CanRequestReschedule_ForEitherTeamsPlayers_AndCommissioners_NotOthers()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var outsider = await RescheduleTestData.AddUser(_context, "outsider");
        var commissioner = await RescheduleTestData.AddUser(_context, "commish");
        await RescheduleTestData.MakeCommissioner(_context, commissioner);

        Assert.That(await CanRequest(s.Match.SeasonMatchID, null), Is.False, "visitor");
        Assert.That(await CanRequest(s.Match.SeasonMatchID, outsider.Id), Is.False, "on neither team");
        Assert.That(await CanRequest(s.Match.SeasonMatchID, s.Requester.Id), Is.True, "home player");
        Assert.That(await CanRequest(s.Match.SeasonMatchID, s.AwayCaptain.Id), Is.True, "away captain");
        Assert.That(await CanRequest(s.Match.SeasonMatchID, commissioner.Id), Is.True, "commissioner");
    }

    [Test]
    public async Task CanRequestReschedule_IsFalse_ForADecidedMatch_AMatchMissingATeam_OrWithARequestActive()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var decided = SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home);
        var waiting = SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, null, bestOf: 1);
        _context.SeasonMatches.AddRange(decided, waiting);
        await _context.SaveChangesAsync();
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);

        Assert.That(await CanRequest(decided.SeasonMatchID, s.HomeCaptain.Id), Is.False, "decided");
        Assert.That(await CanRequest(waiting.SeasonMatchID, s.HomeCaptain.Id), Is.False, "missing a team");
        Assert.That(await CanRequest(s.Match.SeasonMatchID, s.HomeCaptain.Id), Is.False, "request already active");
    }

    [Test]
    public async Task ReportMatch_ReturnsBadRequest_ForEmptyGuid_WithoutCallingService()
    {
        var result = await _controller.ReportMatch(1, Guid.Empty, CancellationToken.None);

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("Provide valid Guid"));
        await _dataPullService.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
    }

    [Test]
    public async Task ReportMatch_ReturnsOk_AndLinksMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var result = await _controller.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
        Assert.That(await _context.SeriesGames.AnyAsync(x => x.MatchID == match.MatchID), Is.True);
    }

    [Test]
    public async Task ReportMatch_ReplacesTheGamePicked_AndRefusesOneNotOfTheMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var start = new DateTime(2026, 1, 10, 20, 0, 0, DateTimeKind.Utc);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, start, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        await _controller.AddManualGame(seeded.SeasonMatch.SeasonMatchID, new ManualGameRequestDto
        {
            GameVersionID = GrifballWebApp.Server.Stats.PlayerGameStatsQuery.InfiniteGameVersionID, PlayedAt = start, HomeTeamScore = 5, AwayTeamScore = 3,
        }, CancellationToken.None);
        _context.ChangeTracker.Clear();

        var typed = await _context.SeriesGames.Where(g => g.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID).Select(g => g.SeriesGameID).SingleAsync();

        var refused = await _controller.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID, CancellationToken.None, replaces: 999999);
        _context.ChangeTracker.Clear();
        var replaced = await _controller.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID, CancellationToken.None, replaces: typed);

        Assert.Multiple(() =>
        {
            Assert.That((refused as BadRequestObjectResult)?.Value, Is.EqualTo("That game is not one of this match's games"));
            Assert.That(replaced, Is.InstanceOf<OkResult>());
            Assert.That(_context.SeriesGames.Where(g => g.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID).Select(g => g.MatchID).ToList(), Is.EqualTo(new Guid?[] { match.MatchID }));
        });
    }

    [Test]
    public async Task ManualGames_AddEditDelete_ReturnOk_AndBadRequestForWhatTheRulesRefuse()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var id = seeded.SeasonMatch.SeasonMatchID;
        _context.ChangeTracker.Clear();
        ManualGameRequestDto Game(int home, int away) => new() { GameVersionID = 1, HomeTeamScore = home, AwayTeamScore = away };

        var added = await _controller.AddManualGame(id, Game(5, 3), CancellationToken.None);
        var tie = await _controller.AddManualGame(id, Game(3, 3), CancellationToken.None);
        _context.ChangeTracker.Clear();
        var edited = await _controller.EditManualGame(id, added.Value, Game(2, 5), CancellationToken.None);
        var badEdit = await _controller.EditManualGame(id, added.Value, Game(4, 4), CancellationToken.None);
        _context.ChangeTracker.Clear();
        var seriesGameID = await _context.SeriesGames.Where(g => g.RecordedGameID == added.Value).Select(g => g.SeriesGameID).SingleAsync();
        var deleted = await _controller.RemoveGame(id, seriesGameID, CancellationToken.None);
        var badDelete = await _controller.RemoveGame(id, seriesGameID, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(added.Value, Is.GreaterThan(0));
            Assert.That((tie.Result as BadRequestObjectResult)?.Value, Is.EqualTo("A game needs a winner: the two scores can't be equal"));
            Assert.That(edited, Is.InstanceOf<OkResult>());
            Assert.That(badEdit, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(deleted, Is.InstanceOf<OkResult>());
            Assert.That((badDelete as BadRequestObjectResult)?.Value, Is.EqualTo("That game is not one of this match's games"));
        });
    }

    [Test]
    public async Task AManualGameThatDecidesTheSeries_ClosesItsPendingRescheduleRequest()
    {
        // #297: the match page shows no active reschedule request for a match its games decided
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var id = seeded.SeasonMatch.SeasonMatchID;
        var requester = await _context.Users.FirstAsync();
        var reschedule = await RescheduleTestData.AddReschedule(_context, id, requester.Id);
        _context.ChangeTracker.Clear();

        var added = await _controller.AddManualGame(id, new ManualGameRequestDto { GameVersionID = 1, HomeTeamScore = 5, AwayTeamScore = 3 }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var page = await _controller.GetSeasonMatchPage(id, CancellationToken.None);
        var closed = await _context.MatchReschedules.AsNoTracking().SingleAsync(r => r.MatchRescheduleID == reschedule.MatchRescheduleID);
        Assert.Multiple(() =>
        {
            Assert.That(added.Value, Is.GreaterThan(0));
            Assert.That(page!.DecidedBy, Is.EqualTo(SeasonMatchDecisionDto.Games));
            Assert.That(page.CanUndo, Is.True, "Its result can be taken back (SeasonMatch.CanUndoResult)");
            Assert.That(page.ActiveRescheduleRequestId, Is.Null);
            Assert.That((closed.Status, closed.ApprovedByUserID, closed.CommissionerNotes),
                Is.EqualTo((RescheduleStatus.Rejected, (int?)requester.Id, MatchReschedule.ClosedBecauseDecided)));
        });
    }

    [Test]
    public async Task Forfeits_ReturnOk_AndSetResults()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var second = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID, bestOf: 1);
        _context.SeasonMatches.Add(second);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var home = await _controller.HomeForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        var away = await _controller.AwayForfeit(second.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var r1 = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        var r2 = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == second.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(home, Is.InstanceOf<OkResult>());
            Assert.That(away, Is.InstanceOf<OkResult>());
            Assert.That(r1.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(r2.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
        });
    }

    [Test]
    public async Task DoubleForfeit_ReturnsOk_AndSetsBothResults()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.ChangeTracker.Clear();

        var result = await _controller.DoubleForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var sm = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<OkResult>());
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
        });
    }

    [TestCase(nameof(SeasonMatchController.HomeForfeit))]
    [TestCase(nameof(SeasonMatchController.AwayForfeit))]
    [TestCase(nameof(SeasonMatchController.DoubleForfeit))]
    public async Task Forfeits_ReturnBadRequest_ForTheGrandFinal(string action)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.Add(MatchBracketInfo.Seed(seasonMatchID: seeded.SeasonMatch.SeasonMatchID, roundNumber: 3, matchNumber: 4, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2, bracket: Bracket.GrandFinal));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var id = seeded.SeasonMatch.SeasonMatchID;

        var result = action switch
        {
            nameof(SeasonMatchController.HomeForfeit) => await _controller.HomeForfeit(id, CancellationToken.None),
            nameof(SeasonMatchController.AwayForfeit) => await _controller.AwayForfeit(id, CancellationToken.None),
            _ => await _controller.DoubleForfeit(id, CancellationToken.None),
        };

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("The grand final cannot be forfeited, report its games instead"));
    }

    [Test]
    public async Task UndoResult_ReturnsOk_AndClearsTheResult()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.ChangeTracker.Clear();
        await _controller.HomeForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        _context.ChangeTracker.Clear();

        var result = await _controller.UndoResult(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var sm = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<OkResult>());
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        });
    }

    [Test]
    public async Task UndoResult_ReturnsBadRequest_WithNothingToUndo()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.ChangeTracker.Clear();

        var result = await _controller.UndoResult(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("This match has no result to undo"));
    }

    [Test]
    public void CommissionerActions_ArePosts()
    {
        var type = typeof(SeasonMatchController);
        Assert.Multiple(() =>
        {
            foreach (var name in new[] { nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit), nameof(SeasonMatchController.DoubleForfeit), nameof(SeasonMatchController.UndoResult) })
            {
                Assert.That(ControllerTestHelpers.Action(type, name).GetCustomAttribute<HttpPostAttribute>(), Is.Not.Null, name);
                Assert.That(ControllerTestHelpers.Action(type, name).GetCustomAttribute<HttpGetAttribute>(), Is.Null, name);
            }
        });
    }

    [Test]
    public async Task GetPossibleMatches_ReadsTheDatabase_WithoutAskingHalo()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var result = await _controller.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task PullMatches_QueuesOnePullPerSeasonMatch_AndItsStatusSaysSo()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var id = seeded.SeasonMatch.SeasonMatchID;
        await using var jobs = await InMemoryJobs.Create(TimeProvider.System, null);

        var idle = await _controller.MatchPullStatus(id, jobs.OnDemand, CancellationToken.None);
        var queued = (await _controller.PullMatches(id, jobs.OnDemand, CancellationToken.None)).Value;
        // Asked again, back to the season's start this time: still the one queued pull
        var again = (await _controller.PullMatches(id, jobs.OnDemand, CancellationToken.None, backToSeasonStart: true)).Value;
        var missing = await _controller.PullMatches(id + 1000, jobs.OnDemand, CancellationToken.None);

        var triggers = await jobs.Scheduler.GetTriggersOfJob(OnDemandJobs.JobKeyOf(typeof(SeasonMatchPullJob)));
        Assert.Multiple(async () =>
        {
            Assert.That((idle.State, idle.LastRun), Is.EqualTo((OnDemandJobState.Idle, (OnDemandRunDto?)null)));
            Assert.That((queued!.State, again!.State), Is.EqualTo((OnDemandJobState.Queued, OnDemandJobState.Queued)));
            Assert.That((await _controller.MatchPullStatus(id, jobs.OnDemand, CancellationToken.None)).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(triggers.Select(t => t.Key), Is.EqualTo(new[] { OnDemandJobs.TriggerKeyOf<SeasonMatchPullJob>(SeasonMatchPullJob.RunName(id)) }), "Asked twice: one pull");
            Assert.That(triggers.Single().ExecutionGroup, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
            Assert.That(missing.Result, Is.InstanceOf<NotFoundResult>());
        });
    }

    [Test]
    public void Authorization_IsConfigured()
    {
        var type = typeof(SeasonMatchController);
        Assert.Multiple(() =>
        {
            foreach (var name in new[] { nameof(SeasonMatchController.ReportMatch), nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit), nameof(SeasonMatchController.DoubleForfeit), nameof(SeasonMatchController.UndoResult),
                nameof(SeasonMatchController.AddManualGame), nameof(SeasonMatchController.EditManualGame), nameof(SeasonMatchController.RemoveGame) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name)?.Roles, Is.EqualTo("Commissioner"), name);
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(SeasonMatchController.PullMatches))?.Roles, Is.EqualTo("Commissioner,Sysadmin"));
            foreach (var name in new[] { nameof(SeasonMatchController.GetSeasonMatchPage), nameof(SeasonMatchController.GetPossibleMatches), nameof(SeasonMatchController.MatchPullStatus) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name), Is.Null, name);
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Null);
        });
    }
}
