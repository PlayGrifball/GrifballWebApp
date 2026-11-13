using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class AvailabilityServiceUnitTests : ApplicationDbContextTests
{
    private AvailabilityService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new AvailabilityService(Context);
    }

    private static TimeslotDto Slot(DayOfWeek day, int hour) => new() { DayOfWeek = day, Time = new TimeOnly(hour, 0) };

    [Test]
    public async Task GetSeasonAvailability_NoTimeslots_ReturnsEmpty()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        await Context.SaveChangesAsync();

        var slots = await _service.GetSeasonAvailability(1, CancellationToken.None);

        Assert.That(slots, Is.Empty);
    }

    [Test]
    public async Task UpdateSeasonAvailability_UnknownDay_Throws()
    {
        var dto = new SeasonAvailabilityDto { SeasonID = 1, Timeslots = [new TimeslotDto { DayOfWeek = (DayOfWeek)99, Time = new TimeOnly(18, 0) }] };

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpdateSeasonAvailability(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo(SeasonRefusedException.UnknownDay));
    }

    [Test]
    public async Task UpdateSeasonAvailability_NoSuchSeason_Throws()
    {
        var dto = new SeasonAvailabilityDto { SeasonID = 999, Timeslots = [Slot(DayOfWeek.Monday, 18)] };

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpdateSeasonAvailability(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("There is no such season"));
    }

    [Test]
    public async Task UpdateSeasonAvailability_SetsTimeslotsAndIsIdempotent()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        await Context.SaveChangesAsync();
        var dto = new SeasonAvailabilityDto { SeasonID = 1, Timeslots = [Slot(DayOfWeek.Monday, 18), Slot(DayOfWeek.Tuesday, 19)] };

        await _service.UpdateSeasonAvailability(dto, CancellationToken.None);

        var slots = await _service.GetSeasonAvailability(1, CancellationToken.None);
        Assert.That(slots, Has.Length.EqualTo(2));

        // Running it again with the same timeslots doesn't duplicate the shared AvailabilityOption rows.
        await _service.UpdateSeasonAvailability(dto, CancellationToken.None);
        Context.ChangeTracker.Clear();
        var allOptions = await Context.Availability.ToListAsync();
        Assert.That(allOptions, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UpdateSeasonAvailability_RemovesTimeslotsNoLongerPicked()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        await Context.SaveChangesAsync();
        await _service.UpdateSeasonAvailability(
            new SeasonAvailabilityDto { SeasonID = 1, Timeslots = [Slot(DayOfWeek.Monday, 18), Slot(DayOfWeek.Tuesday, 19)] },
            CancellationToken.None);

        await _service.UpdateSeasonAvailability(
            new SeasonAvailabilityDto { SeasonID = 1, Timeslots = [Slot(DayOfWeek.Monday, 18)] },
            CancellationToken.None);

        var slots = await _service.GetSeasonAvailability(1, CancellationToken.None);
        Assert.That(slots, Has.Length.EqualTo(1));
        Assert.That(slots[0].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
    }
}
