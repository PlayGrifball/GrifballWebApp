using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        var availabilityOption = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));

        _context.Seasons.Add(season);
        _context.Availability.Add(availabilityOption);
        await _context.SaveChangesAsync();

        var seasonAvailability = SeasonAvailability.Seed(seasonID: season.SeasonID, availabilityOptionID: availabilityOption.AvailabilityOptionID);

        _context.Set<SeasonAvailability>().Add(seasonAvailability);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(18, 0)));
        }
    }

    [Test]
    public async Task GetSeasonAvailability_Should_ReturnMultipleTimeslots_When_MultipleAvailabilityExists()
    {
        // Arrange
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        var option1 = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));

        var option2 = AvailabilityOption.At(DayOfWeek.Wednesday, new TimeOnly(20, 0));

        _context.Seasons.Add(season);
        _context.Availability.AddRange(option1, option2);
        await _context.SaveChangesAsync();

        _context.Set<SeasonAvailability>().AddRange(
            SeasonAvailability.Seed(seasonID: season.SeasonID, availabilityOptionID: option1.AvailabilityOptionID),
            SeasonAvailability.Seed(seasonID: season.SeasonID, availabilityOptionID: option2.AvailabilityOptionID)
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Any(r => r.DayOfWeek == DayOfWeek.Monday && r.Time == new TimeOnly(18, 0)), Is.True);
            Assert.That(result.Any(r => r.DayOfWeek == DayOfWeek.Wednesday && r.Time == new TimeOnly(20, 0)), Is.True);
        }
    }

    [Test]
    public async Task GetSeasonAvailability_Should_ReturnOnlySeasonSpecificTimeslots_When_MultipleSeasons()
    {
        // Arrange
        var season1 = Season.Seed(seasonName: "Season 1").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        var season2 = Season.Seed(seasonName: "Season 2").WithDates(seasonStart: DateTime.UtcNow.AddDays(35), seasonEnd: DateTime.UtcNow.AddDays(65));

        var option1 = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));

        var option2 = AvailabilityOption.At(DayOfWeek.Wednesday, new TimeOnly(20, 0));

        _context.Seasons.AddRange(season1, season2);
        _context.Availability.AddRange(option1, option2);
        await _context.SaveChangesAsync();

        _context.Set<SeasonAvailability>().AddRange(
            SeasonAvailability.Seed(seasonID: season1.SeasonID, availabilityOptionID: option1.AvailabilityOptionID),
            SeasonAvailability.Seed(seasonID: season2.SeasonID, availabilityOptionID: option2.AvailabilityOptionID)
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonAvailability(season1.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(18, 0)));
        }
    }

    [Test]
    public async Task UpdateSeasonAvailability_Should_AddNewAvailability_When_NoExistingAvailability()
    {
        // Arrange
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        _context.Seasons.Add(season);
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        var existingOption = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));

        _context.Seasons.Add(season);
        _context.Availability.Add(existingOption);
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

    [Test]
    public async Task UpdateSeasonAvailability_RefusesASeasonThatDoesNotExist()
    {
        var dto = new SeasonAvailabilityDto { SeasonID = 999, Timeslots = [new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(18, 0) }] };

        await Assert.ThatAsync(() => _service.UpdateSeasonAvailability(dto, CancellationToken.None), Throws.Exception.With.Message.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task UpdateSeasonAvailability_RefusesAnImportedSeason_A400_NothingSaved()
    {
        var season = Season.Imported("wb/Stats", "Summer League 2018", League.DefaultLeagueID, null, null, "https://sheets/wb", SeasonFormat.LeagueSeason);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var dto = new SeasonAvailabilityDto { SeasonID = season.SeasonID, Timeslots = [new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(18, 0) }] };

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpdateSeasonAvailability(dto, CancellationToken.None));

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(ex.Message, Is.EqualTo("That season was imported from the historical archive and can't be edited here."), "The season editor's text");
            Assert.That(await _context.Seasons.Timeslots().AnyAsync(), Is.False);
            Assert.That(await _context.Availability.AnyAsync(), Is.False, "No option saved either");
        }
    }

    [Test]
    public async Task UpdateSeasonAvailability_RefusesADayNoWeekHas_A400_NothingSaved()
    {
        var season = Season.Seed(seasonName: "S").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        // JSON can carry any number for the day
        var dto = new SeasonAvailabilityDto
        {
            SeasonID = season.SeasonID,
            Timeslots = [new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(18, 0) }, new TimeslotDto { DayOfWeek = (DayOfWeek)9, Time = new TimeOnly(18, 0) }],
        };

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpdateSeasonAvailability(dto, CancellationToken.None));

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(ex.Message, Is.EqualTo(SeasonRefusedException.UnknownDay));
            Assert.That(await _context.Seasons.Timeslots().AnyAsync(), Is.False);
            Assert.That(await _context.Availability.AnyAsync(), Is.False, "No option saved either");
        }
    }

    [Test]
    public async Task UpdateSeasonAvailability_AttachesOnlyTheExactSlotsPicked_AndDetachesTheRest()
    {
        var season = Season.Seed(seasonName: "S").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        _context.Seasons.Add(season);
        // Options that exist already, from earlier seasons: Monday 18:00 and Tuesday 20:00
        _context.Availability.AddRange(
            AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0)),
            AvailabilityOption.At(DayOfWeek.Tuesday, new TimeOnly(20, 0)));
        await _context.SaveChangesAsync();

        // Monday 20:00 and Tuesday 18:00: the same days and times, crossed
        await _service.UpdateSeasonAvailability(new SeasonAvailabilityDto
        {
            SeasonID = season.SeasonID,
            Timeslots =
            [
                new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(20, 0) },
                new TimeslotDto { DayOfWeek = DayOfWeek.Tuesday, Time = new TimeOnly(18, 0) },
                new TimeslotDto { DayOfWeek = DayOfWeek.Tuesday, Time = new TimeOnly(18, 0) },
            ],
        }, CancellationToken.None);
        var first = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        // Then deselect Tuesday 18:00
        await _service.UpdateSeasonAvailability(new SeasonAvailabilityDto
        {
            SeasonID = season.SeasonID,
            Timeslots = [new TimeslotDto { DayOfWeek = DayOfWeek.Monday, Time = new TimeOnly(20, 0) }],
        }, CancellationToken.None);
        var second = await _service.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Select(x => (x.DayOfWeek, x.Time)), Is.EquivalentTo(new[] { (DayOfWeek.Monday, new TimeOnly(20, 0)), (DayOfWeek.Tuesday, new TimeOnly(18, 0)) }));
            Assert.That(second.Select(x => (x.DayOfWeek, x.Time)), Is.EqualTo(new[] { (DayOfWeek.Monday, new TimeOnly(20, 0)) }));
            Assert.That(await _context.Availability.CountAsync(), Is.EqualTo(4), "Options stay for other seasons");
        }
    }
}
