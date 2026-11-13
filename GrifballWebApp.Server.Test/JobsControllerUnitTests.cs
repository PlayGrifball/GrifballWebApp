using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Server.Jobs;
using Microsoft.AspNetCore.Http;

namespace GrifballWebApp.Server.Test;

/// <summary>JobsController's unit tests: the interval validated before JobsService is ever asked, so it stays null.
/// Its other tests stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class JobsControllerUnitTests
{
    // Refused before the service is asked
    private readonly JobsController _controller = new(null!);

    [TestCase(1)]
    [TestCase(366 * 24 * 60 * 60)]
    public async Task SetInterval_RepliesBadRequest_ForAnIntervalOutsideTheAllowedRange(long seconds)
    {
        var result = await _controller.SetInterval("group", "name", new JobIntervalRequest(seconds), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest);
    }
}
