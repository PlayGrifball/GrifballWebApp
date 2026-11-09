using GrifballWebApp.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Server.Sorting;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Server.Seasons;

[Route("[controller]/[action]")]
[ApiController]
public class HomeController : ControllerBase
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;

    public HomeController(GrifballContext context, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = context;
    }

    [HttpGet(Name = "CurrentAndFutureEvents")]
    public async Task<Event[]> CurrentAndFutureEvents(CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        var signups = await _context.Seasons.Where(Season.SignupsNotClosedAt(now))
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.SignupsOpen,
                End = x.SignupsClose,
                EventType = EventType.Signup,
            }).ToArrayAsync(ct);

        // Need precise end for draft.
        var drafts = await _context.Seasons.Where(Season.NotStartedAt(now))
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.DraftStart,
                End = x.SeasonStart,
                EventType = EventType.Draft,
            }).ToArrayAsync(ct);


        var seasons = await _context.Seasons.Where(Season.NotEndedAt(now))
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

