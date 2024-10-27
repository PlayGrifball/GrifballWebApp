using GrifballWebApp.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Server.Sorting;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;

namespace GrifballWebApp.Server.Seasons;

[Route("[controller]/[action]")]
[ApiController]
public class HomeController : ControllerBase
{
    private readonly GrifballContext _context;

    public HomeController(GrifballContext context)
    {
        _context = context;
    }

    [HttpGet(Name = "CurrentAndFutureEvents")]
    public async Task<Event[]> CurrentAndFutureEvents(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var signups = await _context.Seasons.Where(s => now <= s.SignupsClose)
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.SignupsOpen,
                End = x.SignupsClose,
                EventType = EventType.Signup,
            }).ToArrayAsync(ct);

        // Need precise end for draft.
        var drafts = await _context.Seasons.Where(s => now <= s.SeasonStart)
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.DraftStart,
                End = x.SeasonStart,
                EventType = EventType.Draft,
            }).ToArrayAsync(ct);


        var seasons = await _context.Seasons.Where(s => now <= s.SeasonEnd)
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.SeasonStart,
                End = x.SeasonEnd,
                EventType = EventType.Season,
            }).ToArrayAsync(ct);

        return signups.Concat(drafts).Concat(seasons)
            .OrderBy(x => x.Start).ToArray();
    }

    [HttpGet(Name = "PastSeasons")]
    public async Task<PaginationResult<Event>> PastSeasons([FromQuery] PaginationFilter filter, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = _context.Seasons.Where(s => now > s.SeasonEnd)
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.SeasonStart,
                End = x.SeasonEnd,
                EventType = EventType.Season,
            });

        if (filter.SortColumn is null)
        {
            query = query.OrderByDescending(x => x.End);
        }
        else
        {
            query = query.OrderBy(filter);
        }

        return await query.PaginationResult(filter, ct);
    }
}

public class Event
{
    public required int SeasonID { get; set; }
    public required string Name { get; set; }
    public required DateTime Start { get; set; }
    public required DateTime End { get; set; }
    public required EventType EventType { get; set; }
}

public enum EventType
{
    Signup,
    Draft,
    Season,
}

