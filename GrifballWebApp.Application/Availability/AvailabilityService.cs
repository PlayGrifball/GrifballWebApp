using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Availability;

public class AvailabilityService
{
    private readonly IApplicationDbContext _context;
    private readonly AvailabilityComparer _comparer = new AvailabilityComparer();
    public AvailabilityService(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<TimeslotDto[]> GetSeasonAvailability(int seasonID, CancellationToken ct)
    {
        return _context.Seasons.Timeslots()
            .Where(x => x.SeasonID == seasonID)
            .Select(x => x.AvailabilityOption)
            .Select(x => new TimeslotDto()
            {
                DayOfWeek = x.DayOfWeek,
                Time = x.Time,
            })
            .ToArrayAsync(ct);
    }

    public async Task UpdateSeasonAvailability(SeasonAvailabilityDto a, CancellationToken ct)
    {
        // A day no week has is refused (a 400), nothing saved
        if (a.Timeslots.Any(x => !AvailabilityOption.IsDay(x.DayOfWeek)))
            throw new SeasonRefusedException(SeasonRefusedException.UnknownDay);

        var mapped = a.Timeslots.Select(x => AvailabilityOption.At(x.DayOfWeek, x.Time)).Distinct(_comparer).ToList();

        var season = await _context.Seasons.Include(s => s.SeasonAvailability)
            .FirstOrDefaultAsync(s => s.SeasonID == a.SeasonID, ct)
            ?? throw new Exception("Season does not exist");

        var existing = await _context.Availability
            .Where(x => mapped.Select(x => x.DayOfWeek).Contains(x.DayOfWeek))
            .Where(x => mapped.Select(x => x.Time).Contains(x.Time))
            .ToListAsync(ct);
        // The query narrows by day and by time separately (all it can translate), which also finds the other days at the
        // picked times: keep only the exact (day, time) pairs picked.
        existing = existing.Where(x => mapped.Contains(x, _comparer)).ToList();

        // Add missing options
        var missing = mapped.Except(existing, _comparer).ToList();
        _context.AddRange(missing);

        // The season's timeslots become the ones picked: the rest are taken off it. An imported season is refused (a 400),
        // nothing saved, as the season editor refuses it
        if (season.SetAvailability(missing.Concat(existing).ToArray()) is SeasonEditResult.Imported)
            throw new SeasonRefusedException(SeasonRefusedException.ImportedSeason);
        await _context.SaveChangesAsync(ct);

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
