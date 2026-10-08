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

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CommissionerDashboardControllerTests
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
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(CommissionerDashboardController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }

    private async Task SeedOverdueAndPending()
    {
        var season = await TeamsTestData.AddCurrentSeason(_context);
        var home = await TeamsTestData.AddTeamWithCaptain(_context, season, "HomeCap", 101);
        var away = await TeamsTestData.AddTeamWithCaptain(_context, season, "AwayCap", 102);
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
