using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AvailabilityTimeslotDto = GrifballWebApp.Application.Availability.TimeslotDto;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        var past = Season.Seed(seasonName: "Past").WithDates(seasonStart: now.AddDays(-60), seasonEnd: now.AddDays(-30));
        var current = Season.Seed(seasonName: "Current").WithDates(seasonStart: now.AddDays(-1), seasonEnd: now.AddDays(10));
        _context.Seasons.AddRange(past, current);
        await _context.SaveChangesAsync();
        var controller = new SeasonController(new EventOrganizerService(_context), new SeasonService(_context, new GrifballWebApp.Application.Stats.SeasonStatsService(_context)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await controller.GetCurrentSeasonID(CancellationToken.None), Is.EqualTo(current.SeasonID));
            Assert.That(await controller.GetSeasonName(past.SeasonID, CancellationToken.None), Is.EqualTo("Past"));
            Assert.That(await controller.GetSeasonName(123456, CancellationToken.None), Is.Null);
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Thursday));
            Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(7, 17)));
        }
    }
}
