using GrifballWebApp.Server.MatchPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using GrifballWebApp.Application.MatchPlanner;

namespace GrifballWebApp.Server.Test;

/// <summary>MatchPlannerControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchPlannerControllerUnitTests
{
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
}
