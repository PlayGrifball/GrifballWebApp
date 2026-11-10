using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Server.MatchPlanner;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>MatchPlannerControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchPlannerControllerUnitTests
{
    // Refused before the service is asked
    private readonly MatchPlannerController _controller = new(Substitute.For<ILogger<MatchPlannerController>>(), null!);

    [Test]
    public void Authorization_MutatingActionsRequireCommissioner()
    {
        var t = typeof(MatchPlannerController);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(t), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.CreateSeasonMatches))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.UpdateMatchTime))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.GetScheduledMatches)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(MatchPlannerController.GetUnscheduledMatches)), Is.Null);
        }
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
    public async Task GetMatches_InvalidSeason_ReturnsBadRequest()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((BadRequestObjectResult)await _controller.GetScheduledMatches(0)).Value, Is.EqualTo("Please provide seasonID"));
            Assert.That(((BadRequestObjectResult)await _controller.GetUnscheduledMatches(-1)).Value, Is.EqualTo("Please provide seasonID"));
        }
    }
}
