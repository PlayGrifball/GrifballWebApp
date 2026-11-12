using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvailabilityTimeslotDto = GrifballWebApp.Application.Availability.TimeslotDto;
using GrifballWebApp.Application.EventOrganizer;

namespace GrifballWebApp.Server.Test;

/// <summary>EventOrganizerControllerTests's unit tests: a real GrifballContext (temp-file SQLite, #402) for
/// EventOrganizerService, which needs nothing else. Its other tests stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class EventOrganizerControllerUnitTests : SqliteGrifballContextFixture
{
    private EventOrganizerController Controller() => new(new EventOrganizerService(Db));

    [Test]
    public void Controller_RequiresCommissionerOrSysadmin()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(EventOrganizerController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }

    [Test]
    public async Task GetSeasons_ReturnsNone_WhenThereAreNoSeasons()
    {
        var result = await Controller().GetSeasons(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetGameVersions_ReturnsTheSeededVersions()
    {
        var result = await Controller().GetGameVersions(CancellationToken.None);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetLeagues_ReturnsTheSeededLeagues()
    {
        var result = await Controller().GetLeagues(CancellationToken.None);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetSeason_RepliesNotFound_ForANonExistentSeason()
    {
        var result = await Controller().GetSeason(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season");
    }
}
