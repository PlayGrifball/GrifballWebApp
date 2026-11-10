using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvailabilityTimeslotDto = GrifballWebApp.Application.Availability.TimeslotDto;
using GrifballWebApp.Application.EventOrganizer;

namespace GrifballWebApp.Server.Test;

/// <summary>EventOrganizerControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class EventOrganizerControllerUnitTests
{
    [Test]
    public void Controller_RequiresCommissionerOrSysadmin()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(EventOrganizerController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }
}
