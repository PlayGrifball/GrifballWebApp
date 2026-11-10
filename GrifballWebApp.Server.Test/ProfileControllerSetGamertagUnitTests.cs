using GrifballWebApp.Server.Profile;
using AvailabilityTimeslotDto = GrifballWebApp.Application.Availability.TimeslotDto;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.Test;

/// <summary>ProfileControllerSetGamertagTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileControllerSetGamertagUnitTests
{
    [Test]
    public void SetGamertag_RequiresPlayerRole()
    {
        Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(ProfileController), nameof(ProfileController.SetGamertag))!.Roles, Is.EqualTo("Player"));
    }
}
