using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Commissioner;
using GrifballWebApp.Server.MatchPlanner;
using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test.CovD;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CommissionerDashboardControllerTests_d
{
    private GrifballContext _context;
    private CommissionerDashboardController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { ReschedulesChannel = 1UL });
        var service = new RescheduleService(_context, Substitute.For<IDiscordRestClient>(), Substitute.For<ILogger<RescheduleService>>(), options);
        _controller = new CommissionerDashboardController(service);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void Controller_RequiresCommissionerOrSysadmin()
    {
        Assert.That(ControllerTestHelpers_d.ClassAuthorize(typeof(CommissionerDashboardController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }

    private async Task SeedOverdueAndPending()
    {
        var season = await SeedHelpers_d.Season(_context);
        var home = await SeedHelpers_d.TeamWithCaptain(_context, season, "HomeCap", 101);
        var away = await SeedHelpers_d.TeamWithCaptain(_context, season, "AwayCap", 102);
        var now = DateTime.UtcNow;
        _context.SeasonMatches.AddRange(
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = now.AddHours(-100), BestOf = 1 }, // critical
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = away.TeamID, AwayTeamID = home.TeamID, ScheduledTime = now.AddHours(-30), BestOf = 1 },  // overdue, not critical
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = now.AddHours(-30), BestOf = 1, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss }, // reported
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = now.AddHours(5), BestOf = 1 }); // future
        await _context.SaveChangesAsync();

        var future = await _context.SeasonMatches.OrderByDescending(x => x.ScheduledTime).FirstAsync();
        var requester = await _context.Users.FirstAsync(u => u.UserName == "user_HomeCap");
        _context.MatchReschedules.AddRange(
            new MatchReschedule { SeasonMatchID = future.SeasonMatchID, Reason = "busy", RequestedByUserID = requester.Id, Status = RescheduleStatus.Pending, OriginalScheduledTime = future.ScheduledTime, NewScheduledTime = now.AddDays(2) },
            new MatchReschedule { SeasonMatchID = future.SeasonMatchID, Reason = "old", RequestedByUserID = requester.Id, Status = RescheduleStatus.Approved, OriginalScheduledTime = future.ScheduledTime, NewScheduledTime = now.AddDays(3) });
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task GetDashboardData_SummarisesPendingAndOverdue()
    {
        await SeedOverdueAndPending();

        var result = await _controller.GetDashboardData(CancellationToken.None);

        var dto = (CommissionerDashboardDto)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(dto.PendingReschedules, Has.Count.EqualTo(1));
            Assert.That(dto.PendingReschedules[0].Reason, Is.EqualTo("busy"));
            Assert.That(dto.PendingReschedules[0].RequestedByGamertag, Is.EqualTo("HomeCap"));
            Assert.That(dto.OverdueMatches, Has.Count.EqualTo(2));
            Assert.That(dto.OverdueMatches[0].HomeCaptain, Is.EqualTo("HomeCap"));
            Assert.That(dto.Summary.PendingRescheduleCount, Is.EqualTo(1));
            Assert.That(dto.Summary.OverdueMatchCount, Is.EqualTo(2));
            Assert.That(dto.Summary.CriticalOverdueCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetDashboardData_Empty_ReturnsZeroes()
    {
        var dto = (CommissionerDashboardDto)((OkObjectResult)await _controller.GetDashboardData(CancellationToken.None)).Value!;

        Assert.That(dto.PendingReschedules, Is.Empty);
        Assert.That(dto.OverdueMatches, Is.Empty);
        Assert.That(dto.Summary.CriticalOverdueCount, Is.Zero);
    }

    [Test]
    public async Task GetOverdueMatches_ReturnsOldestFirst()
    {
        await SeedOverdueAndPending();

        var list = (List<OverdueMatchDto>)((OkObjectResult)await _controller.GetOverdueMatches(CancellationToken.None)).Value!;

        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0].HoursOverdue, Is.GreaterThanOrEqualTo(99));
        Assert.That(list[1].HoursOverdue, Is.InRange(29, 30));
        Assert.That(list[1].HomeCaptain, Is.EqualTo("AwayCap"));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MatchPlannerControllerTests_d
{
    private GrifballContext _context;
    private MatchPlannerController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new MatchPlannerController(Substitute.For<ILogger<MatchPlannerController>>(), new MatchPlannerService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void Authorization_MutatingActionsRequireCommissioner()
    {
        var t = typeof(MatchPlannerController);
        Assert.Multiple(() =>
        {
            Assert.That(ControllerTestHelpers_d.ClassAuthorize(t), Is.Null);
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(MatchPlannerController.CreateSeasonMatches))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(MatchPlannerController.UpdateMatchTime))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(MatchPlannerController.GetScheduledMatches)), Is.Null);
            Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(MatchPlannerController.GetUnscheduledMatches)), Is.Null);
        });
    }

    [TestCase(0, 1, 1, "Please provide seasonID")]
    [TestCase(1, 0, 1, "homeMatchesPerTeam should be at least 1")]
    [TestCase(1, 1, 0, "bestOf should be at least 1")]
    public async Task CreateSeasonMatches_InvalidInput_ReturnsBadRequest(int seasonId, int home, int bestOf, string message)
    {
        var result = await _controller.CreateSeasonMatches(seasonId, home, bestOf, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo(message));
    }

    [Test]
    public async Task CreateSeasonMatches_Valid_CreatesRoundRobin()
    {
        var season = await SeedHelpers_d.Season(_context);
        await SeedHelpers_d.TeamWithCaptain(_context, season, "A", 1);
        await SeedHelpers_d.TeamWithCaptain(_context, season, "B", 2);
        await SeedHelpers_d.TeamWithCaptain(_context, season, "C", 3);

        var result = await _controller.CreateSeasonMatches(season.SeasonID, 2, 3, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        var matches = await _context.SeasonMatches.Where(x => x.SeasonID == season.SeasonID).ToListAsync();
        Assert.That(matches, Has.Count.EqualTo(3 * 2 * 2));
        Assert.That(matches.All(m => m.BestOf == 3));
    }

    [Test]
    public async Task GetMatches_InvalidSeason_ReturnsBadRequest()
    {
        Assert.That(((BadRequestObjectResult)await _controller.GetScheduledMatches(0)).Value, Is.EqualTo("Please provide seasonID"));
        Assert.That(((BadRequestObjectResult)await _controller.GetUnscheduledMatches(-1)).Value, Is.EqualTo("Please provide seasonID"));
    }

    [Test]
    public async Task GetScheduledAndUnscheduled_SplitByScheduledTime_AndUpdateMatchTimeMovesMatch()
    {
        var season = await SeedHelpers_d.Season(_context);
        var a = await SeedHelpers_d.TeamWithCaptain(_context, season, "Alpha", 1);
        var b = await SeedHelpers_d.TeamWithCaptain(_context, season, "Bravo", 2);
        var t1 = new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var scheduled = new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = b.TeamID, ScheduledTime = t1, BestOf = 1, HomeTeamResult = SeasonMatchResult.Won };
        var unscheduled = new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = a.TeamID, BestOf = 1 };
        _context.SeasonMatches.AddRange(scheduled, unscheduled);
        await _context.SaveChangesAsync();

        var sched = (List<ScheduledMatchDto>)((OkObjectResult)await _controller.GetScheduledMatches(season.SeasonID)).Value!;
        var unsched = (List<UnscheduledMatchDto>)((OkObjectResult)await _controller.GetUnscheduledMatches(season.SeasonID)).Value!;

        Assert.That(sched, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(sched[0].SeasonMatchID, Is.EqualTo(scheduled.SeasonMatchID));
            Assert.That(sched[0].HomeCaptain, Is.EqualTo("Alpha"));
            Assert.That(sched[0].AwayCaptain, Is.EqualTo("Bravo"));
            Assert.That(sched[0].Complete, Is.True);
            Assert.That(sched[0].Time, Is.EqualTo(t1));
            Assert.That(unsched.Single().SeasonMatchID, Is.EqualTo(unscheduled.SeasonMatchID));
            Assert.That(unsched.Single().Complete, Is.False);
        });

        var t2 = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _controller.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = unscheduled.SeasonMatchID, Time = t2 });

        var after = (List<ScheduledMatchDto>)((OkObjectResult)await _controller.GetScheduledMatches(season.SeasonID)).Value!;
        Assert.That(after.Select(x => x.SeasonMatchID), Is.EqualTo(new[] { unscheduled.SeasonMatchID, scheduled.SeasonMatchID }));
    }
}
