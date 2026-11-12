using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.MatchPlanner;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using GrifballWebApp.Application.MatchPlanner;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchPlannerControllerTests
{
    private GrifballContext _context;
    private MatchPlannerController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _controller = new MatchPlannerController(new MatchPlannerService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task CreateSeasonMatches_Valid_CreatesRoundRobin()
    {
        var season = await TeamsTestData.AddCurrentSeason(_context);
        await TeamsTestData.AddTeamWithCaptain(_context, season, "A", 1);
        await TeamsTestData.AddTeamWithCaptain(_context, season, "B", 2);
        await TeamsTestData.AddTeamWithCaptain(_context, season, "C", 3);

        var result = await _controller.CreateSeasonMatches(season.SeasonID, 2, 3, CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var matches = await _context.SeasonMatches.Where(x => x.SeasonID == season.SeasonID).ToListAsync();
        Assert.That(matches, Has.Count.EqualTo(3 * 2 * 2));
        Assert.That(matches.All(m => m.BestOf == 3));
    }

    [Test]
    public async Task GetScheduledAndUnscheduled_SplitByScheduledTime_AndUpdateMatchTimeMovesMatch()
    {
        var season = await TeamsTestData.AddCurrentSeason(_context);
        var a = await TeamsTestData.AddTeamWithCaptain(_context, season, "Alpha", 1);
        var b = await TeamsTestData.AddTeamWithCaptain(_context, season, "Bravo", 2);
        var t1 = new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var scheduled = SeasonMatch.Seed(season.SeasonID, a.TeamID, b.TeamID, bestOf: 1, scheduledTime: t1).WonBy(SeasonMatchSide.Home);
        var unscheduled = SeasonMatch.Seed(season.SeasonID, b.TeamID, a.TeamID, bestOf: 1);
        _context.SeasonMatches.AddRange(scheduled, unscheduled);
        await _context.SaveChangesAsync();

        var sched = await _controller.GetScheduledMatches(season.SeasonID, CancellationToken.None);
        var unsched = await _controller.GetUnscheduledMatches(season.SeasonID, CancellationToken.None);

        Assert.That(sched, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sched[0].SeasonMatchID, Is.EqualTo(scheduled.SeasonMatchID));
            Assert.That(sched[0].HomeCaptain, Is.EqualTo("Alpha"));
            Assert.That(sched[0].AwayCaptain, Is.EqualTo("Bravo"));
            Assert.That(sched[0].Complete, Is.True);
            Assert.That(sched[0].Time, Is.EqualTo(t1));
            Assert.That(unsched.Single().SeasonMatchID, Is.EqualTo(unscheduled.SeasonMatchID));
            Assert.That(unsched.Single().Complete, Is.False);
        }

        var t2 = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updated = await _controller.UpdateMatchTime(unscheduled.SeasonMatchID, new UpdateMatchTimeDto { Time = t2 }, CancellationToken.None);
        Assert.That(updated, Is.TypeOf<NoContentResult>());

        var after = await _controller.GetScheduledMatches(season.SeasonID, CancellationToken.None);
        Assert.That(after.Select(x => x.SeasonMatchID), Is.EqualTo(new[] { unscheduled.SeasonMatchID, scheduled.SeasonMatchID }));
    }
}
