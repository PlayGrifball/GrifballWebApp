using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Server.Scheduler;
using Microsoft.AspNetCore.Http;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SchedulerController's unit tests: the season/recommendation "not found" guards, checked before OnDemandJobs (Quartz)
/// is ever asked, on a real GrifballContext (temp-file SQLite, #402). Its other tests stay in
/// GrifballWebApp.Integration.Test.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SchedulerControllerUnitTests : SqliteGrifballContextFixture
{
    private SchedulerController Controller() => new(Db, new ScheduleRecommendations(Db, TimeProvider.System), null!);

    [Test]
    public async Task Recommend_RepliesNotFound_ForANonExistentSeason()
    {
        var result = await Controller().Recommend(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season");
    }

    [Test]
    public async Task Apply_RepliesNotFound_ForANonExistentRecommendation()
    {
        var result = await Controller().Apply(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such schedule recommendation");
    }
}
