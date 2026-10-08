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
public class MatchPlannerControllerTests
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
            Assert.That(ControllerTestHelpers.ClassAuthorize(t), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.CreateSeasonMatches))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.UpdateMatchTime))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.GetScheduledMatches)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.GetUnscheduledMatches)), Is.Null);
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
        var season = await TeamsTestData.AddCurrentSeason(_context);
        await TeamsTestData.AddTeamWithCaptain(_context, season, "A", 1);
        await TeamsTestData.AddTeamWithCaptain(_context, season, "B", 2);
        await TeamsTestData.AddTeamWithCaptain(_context, season, "C", 3);

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
        var season = await TeamsTestData.AddCurrentSeason(_context);
        var a = await TeamsTestData.AddTeamWithCaptain(_context, season, "Alpha", 1);
        var b = await TeamsTestData.AddTeamWithCaptain(_context, season, "Bravo", 2);
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
