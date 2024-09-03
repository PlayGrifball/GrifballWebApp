using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.Availability;

public class AvailabilityService
{
    private readonly GrifballContext _context;
    private readonly AvailabilityComparer _comparer = new AvailabilityComparer();
    public AvailabilityService(GrifballContext context)
    {
        _context = context;
    }

    public async Task UpdateSeasonAvailability(SeasonAvailabilityDto a, CancellationToken ct)
    {
        var mapped = a.Timeslots.Select(x => new AvailabilityOption()
        {
            DayOfWeek = x.DayOfWeek,
            Time = x.Time,
        }).ToList();

        var existing = await _context.Availability
            .Include(x => x.SeasonAvailability.Where(s => s.SeasonID == a.SeasonID))
            .Where(x => mapped.Select(x => x.DayOfWeek).Contains(x.DayOfWeek))
            .Where(x => mapped.Select(x => x.Time).Contains(x.Time))
            .AsSplitQuery()
            .ToListAsync();

        // Add missing options
        var missing = mapped.Except(existing, _comparer).ToList();
        _context.AddRange(missing);
        
        // Then hook all to this season that are not already attached
        var all = missing.Concat(existing).ToArray();
        foreach (var item in all.Where(x => x.SeasonAvailability.Any() is false))
        {
            item.SeasonAvailability.Add(new SeasonAvailability()
            {
                SeasonID = a.SeasonID,
            });
        }

        await _context.SaveChangesAsync();

        // Now find all SeasonAvailability to remove
        var seasonAvail = all.SelectMany(x => x.SeasonAvailability).Select(x => x.AvailabilityOptionID).ToArray();
        var toRemove = await _context.SeasonAvailability.Where(x => !seasonAvail.Contains(x.AvailabilityOptionID) && x.SeasonID == a.SeasonID).ToArrayAsync();
        _context.SeasonAvailability.RemoveRange(toRemove);
        await _context.SaveChangesAsync();

        // Missing Availability will be left in the db because they may be attached to other seasons, this also allows cross tracking to easy fill in for future seasons
    }
}

public class AvailabilityComparer : IEqualityComparer<AvailabilityOption>
{
    public bool Equals(AvailabilityOption? x, AvailabilityOption? y)
    {
        if (x is null & y is null) return true;
        if (x is null & y is not null) return false;
        if (x is not null & y is null) return false;
        if (x!.Time == y!.Time && x.DayOfWeek == y.DayOfWeek) return true;
        return false;
    }

    public int GetHashCode([DisallowNull] AvailabilityOption obj)
    {
        return 1;
    }
}

public class SeasonAvailabilityDto
{
    public int SeasonID { get; set; }
    public TimeslotDto[] Timeslots { get; set; } = [];
}

public class TimeslotDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
}
