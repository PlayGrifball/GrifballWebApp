using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AvailabilityTimeslotDto = GrifballWebApp.Server.Availability.TimeslotDto;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonAndAvailabilityControllerTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SeasonController_ReturnsCurrentSeasonIdAndName()
    {
        var now = DateTime.UtcNow;
        var past = new Season { SeasonName = "Past", SeasonStart = now.AddDays(-60), SeasonEnd = now.AddDays(-30) };
        var current = new Season { SeasonName = "Current", SeasonStart = now.AddDays(-1), SeasonEnd = now.AddDays(10) };
        _context.Seasons.AddRange(past, current);
        await _context.SaveChangesAsync();
        var controller = new SeasonController(new EventOrganizerService(_context), new SeasonService(_context));

        Assert.That(await controller.GetCurrentSeasonID(CancellationToken.None), Is.EqualTo(current.SeasonID));
        Assert.That(await controller.GetSeasonName(past.SeasonID, CancellationToken.None), Is.EqualTo("Past"));
        Assert.That(await controller.GetSeasonName(123456, CancellationToken.None), Is.Null);
    }

    [Test]
    public void SeasonController_IsAnonymous()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(SeasonController)), Is.Null);
    }

    [Test]
    public async Task AvailabilityController_UpdateThenGet_RoundTrips()
    {
        var season = await TeamsTestData.AddCurrentSeason(_context);
        var controller = new AvailabilityController(Substitute.For<ILogger<AvailabilityController>>(), new AvailabilityService(_context));
        var slot = new AvailabilityTimeslotDto { DayOfWeek = DayOfWeek.Thursday, Time = new TimeOnly(7, 17) };

        await controller.UpdateSeasonAvailability(new SeasonAvailabilityDto { SeasonID = season.SeasonID, Timeslots = [slot] }, CancellationToken.None);
        var result = await controller.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Thursday));
        Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(7, 17)));
    }

    [Test]
    public void AvailabilityController_UpdateRequiresCommissioner()
    {
        var t = typeof(AvailabilityController);
        Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(AvailabilityController.UpdateSeasonAvailability))!.Roles, Is.EqualTo("Commissioner"));
        Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(AvailabilityController.GetSeasonAvailability)), Is.Null);
    }
}
