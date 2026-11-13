using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Scheduler;
using GrifballWebApp.Server.Scheduler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SchedulerController past its "not found" guards: a run queued on an in-memory Quartz (<see cref="InMemoryQuartz"/>),
/// the page's view of it, and applying a recommendation, on a temp-file SQLite GrifballContext.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SchedulerControllerRunsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Proposed = new(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
    private InMemoryQuartz? _quartz;

    [TearDown]
    public async Task TearDownQuartz()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
    }

    private async Task<SchedulerController> Controller()
    {
        _quartz = await InMemoryQuartz.Create();
        return new SchedulerController(Db, new ScheduleRecommendations(Db, _time), _quartz.OnDemand).WithUser("1", "commish");
    }

    /// <summary>A season with one undecided match, and a recommendation proposing its time.</summary>
    private async Task<(Season Season, SeasonMatch Match, ScheduleRecommendation Recommendation)> Proposal()
    {
        var season = await AddSeason("Winter");
        var match = await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
        var recommendation = ScheduleRecommendation.Propose(season.SeasonID, "commish", 0.75, 10, [new ProposedMatchTime(match.SeasonMatchID, Proposed, false, true, true)]);
        Db.ScheduleRecommendations.Add(recommendation);
        await Db.SaveChangesAsync();
        return (season, match, recommendation);
    }

    [Test]
    public async Task Recommend_QueuesARun_OnceAtATime()
    {
        var season = await AddSeason("Winter");
        var controller = await Controller();

        var first = await controller.Recommend(season.SeasonID, CancellationToken.None);
        var again = await controller.Recommend(season.SeasonID, CancellationToken.None);

        var trigger = OnDemandJobs.TriggerKeyOf<ScheduleRecommendationJob>(ScheduleRecommendationJob.RunName(season.SeasonID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((OnDemandJobStatusDto)((AcceptedResult)first.Result!).Value!).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(((OnDemandJobStatusDto)((AcceptedResult)again.Result!).Value!).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(await _quartz!.Scheduler.GetTriggersOfJob(OnDemandJobs.JobKeyOf(typeof(ScheduleRecommendationJob))), Has.Count.EqualTo(1));
            Assert.That(await _quartz.Scheduler.GetTriggerState(trigger), Is.EqualTo(Quartz.TriggerState.Normal));
        }
    }

    [Test]
    public async Task Recommendation_IsTheRunsStateAndTheLatest()
    {
        var (season, match, recommendation) = await Proposal();
        var controller = await Controller();

        var page = await controller.Recommendation(season.SeasonID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Run.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(page.Latest!.ScheduleRecommendationID, Is.EqualTo(recommendation.ScheduleRecommendationID));
            Assert.That(page.Latest.Matches.Select(m => (m.SeasonMatchID, m.ScheduledTime, m.HomeTeam, m.AwayTeam)),
                Is.EqualTo(new[] { (match.SeasonMatchID, Proposed, "Home", "Away") }));
        }
    }

    [Test]
    public async Task Apply_SchedulesTheMatches_ThenIsAConflict()
    {
        var (_, match, recommendation) = await Proposal();
        var controller = await Controller();

        var applied = await controller.Apply(recommendation.ScheduleRecommendationID, CancellationToken.None);
        var again = await controller.Apply(recommendation.ScheduleRecommendationID, CancellationToken.None);

        Db.ChangeTracker.Clear();
        var scheduled = await Db.SeasonMatches.SingleAsync(x => x.SeasonMatchID == match.SeasonMatchID);
        var stamped = await Db.ScheduleRecommendations.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied.Value!.MatchesScheduled, Is.EqualTo(1));
            Assert.That(scheduled.ScheduledTime, Is.EqualTo(Proposed));
            Assert.That((stamped.AppliedAt, stamped.AppliedBy), Is.EqualTo(((DateTime?)_time.GetUtcNow().UtcDateTime, (string?)"commish")));
        }
        again.Result.AssertProblem(StatusCodes.Status409Conflict, "That recommendation was applied already");
    }
}
