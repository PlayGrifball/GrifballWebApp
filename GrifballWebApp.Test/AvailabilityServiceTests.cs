using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Availability;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AvailabilityServiceTests
{
    private GrifballContext _context;
    private AvailabilityService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new AvailabilityService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetSeasonAvailability_Should_ReturnEmptyArray_When_NoAvailabilityExists()
    {
        // Arrange

        // Act
        var result = await _service.GetSeasonAvailability(1, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSeasonAvailability_Should_ReturnTimeslots_When_AvailabilityExists()
    {
        // Arrange
        const int seasonId = 1;
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        var availabilityOption = new AvailabilityOption
        {
            DayOfWeek = DayOfWeek.Monday,
            Time = new TimeOnly(18, 0)
        };

        await _context.Seasons.AddAsync(season);
        await _context.Availability.AddAsync(availabilityOption);
        await _context.SaveChangesAsync();

        var seasonAvailability = new SeasonAvailability
        {
            SeasonID = season.SeasonID,
            AvailabilityOptionID = availabilityOption.AvailabilityOptionID
        };

        await _context.SeasonAvailability.AddAsync(seasonAvailability);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(18, 0)));
    }

    [Test]
    public async Task GetSeasonAvailability_Should_ReturnMultipleTimeslots_When_MultipleAvailabilityExists()
    {
        // Arrange
        const int seasonId = 1;
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        var option1 = new AvailabilityOption
        {
            DayOfWeek = DayOfWeek.Monday,
            Time = new TimeOnly(18, 0)
        };

        var option2 = new AvailabilityOption
        {
            DayOfWeek = DayOfWeek.Wednesday,
            Time = new TimeOnly(20, 0)
        };

        await _context.Seasons.AddAsync(season);
        await _context.Availability.AddRangeAsync(option1, option2);
        await _context.SaveChangesAsync();

        await _context.SeasonAvailability.AddRangeAsync(
            new SeasonAvailability { SeasonID = season.SeasonID, AvailabilityOptionID = option1.AvailabilityOptionID },
            new SeasonAvailability { SeasonID = season.SeasonID, AvailabilityOptionID = option2.AvailabilityOptionID }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(2));
        Assert.That(result.Any(r => r.DayOfWeek == DayOfWeek.Monday && r.Time == new TimeOnly(18, 0)), Is.True);
        Assert.That(result.Any(r => r.DayOfWeek == DayOfWeek.Wednesday && r.Time == new TimeOnly(20, 0)), Is.True);
    }

    [Test]
    public async Task GetSeasonAvailability_Should_ReturnOnlySeasonSpecificTimeslots_When_MultipleSeasons()
    {
        // Arrange
        var season1 = new Season
        {
            SeasonName = "Season 1",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        var season2 = new Season
        {
            SeasonName = "Season 2",
            SeasonStart = DateTime.UtcNow.AddDays(35),
            SeasonEnd = DateTime.UtcNow.AddDays(65)
        };

        var option1 = new AvailabilityOption
        {
            DayOfWeek = DayOfWeek.Monday,
            Time = new TimeOnly(18, 0)
        };

        var option2 = new AvailabilityOption
        {
            DayOfWeek = DayOfWeek.Wednesday,
            Time = new TimeOnly(20, 0)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.Availability.AddRangeAsync(option1, option2);
        await _context.SaveChangesAsync();

        await _context.SeasonAvailability.AddRangeAsync(
            new SeasonAvailability { SeasonID = season1.SeasonID, AvailabilityOptionID = option1.AvailabilityOptionID },
            new SeasonAvailability { SeasonID = season2.SeasonID, AvailabilityOptionID = option2.AvailabilityOptionID }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonAvailability(season1.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(18, 0)));
    }

    [Test]
    public async Task UpdateSeasonAvailability_Should_AddNewAvailability_When_NoExistingAvailability()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var dto = new SeasonAvailabilityDto
        {
            SeasonID = season.SeasonID,
            Timeslots = new[]
            {
                new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(18, 0) },
                new TimeslotDto { DayOfWeek = DayOfWeek.Wednesday, Time = new TimeOnly(20, 0) }
            }
        };

        // Act
        await _service.UpdateSeasonAvailability(dto, CancellationToken.None);

        // Assert
        var result = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);
        Assert.That(result, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task UpdateSeasonAvailability_Should_ReuseExistingAvailabilityOptions_When_OptionsExist()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        var existingOption = new AvailabilityOption
        {
            DayOfWeek = DayOfWeek.Monday,
            Time = new TimeOnly(18, 0)
        };

        await _context.Seasons.AddAsync(season);
        await _context.Availability.AddAsync(existingOption);
        await _context.SaveChangesAsync();

        var initialAvailabilityCount = await _context.Availability.CountAsync();

        var dto = new SeasonAvailabilityDto
        {
            SeasonID = season.SeasonID,
            Timeslots = new[]
            {
                new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(18, 0) }
            }
        };

        // Act
        await _service.UpdateSeasonAvailability(dto, CancellationToken.None);

        // Assert
        var finalAvailabilityCount = await _context.Availability.CountAsync();
        Assert.That(finalAvailabilityCount, Is.EqualTo(initialAvailabilityCount), 
            "Should reuse existing AvailabilityOption instead of creating a new one");
    }
}
