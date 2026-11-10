using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.Seasons;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AvailabilityTimeslotDto = GrifballWebApp.Application.Availability.TimeslotDto;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Test;

/// <summary>SeasonAndAvailabilityControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonAndAvailabilityControllerUnitTests
{
    [Test]
    public void SeasonController_IsAnonymous()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(SeasonController)), Is.Null);
    }

    [Test]
    public void AvailabilityController_UpdateRequiresCommissioner()
    {
        var t = typeof(AvailabilityController);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(AvailabilityController.UpdateSeasonAvailability))!.Roles, Is.EqualTo("Commissioner"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(AvailabilityController.GetSeasonAvailability)), Is.Null);
        }
    }
}
