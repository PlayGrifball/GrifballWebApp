using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Home;

/// <summary>The home page's calendar: current and upcoming signups, drafts and seasons. The site and /events both read it.</summary>
public class HomeEventsService
{
    private readonly TimeProvider _time;
    private readonly SeasonVisibility _visibility;

    /// <summary>The calendar of the seasons <paramref name="visibility"/> shows a viewer: it reads them through it alone.</summary>
    public HomeEventsService(SeasonVisibility visibility, TimeProvider? time = null)
    {
        _visibility = visibility;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Current and upcoming signups, drafts and seasons, soonest first: of the seasons <paramref name="viewer"/> may see
    /// (<see cref="SeasonVisibility.Visible"/>), so a season not announced yet is staff's alone.</summary>
    public async Task<Event[]> CurrentAndFuture(SeasonViewer viewer = SeasonViewer.Public, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var visible = _visibility.Visible(viewer);

        var signups = await visible.Where(Season.SignupsNotClosedAt(now))
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.SignupsOpen,
                End = x.SignupsClose,
                EventType = EventType.Signup,
            }).ToArrayAsync(ct);

        // Need precise end for draft.
        var drafts = await visible.Where(Season.NotStartedAt(now))
            .Select(x => new Event()
            {
                SeasonID = x.SeasonID,
                Name = x.SeasonName,
                Start = x.DraftStart,
                End = x.SeasonStart,
                EventType = EventType.Draft,
            }).ToArrayAsync(ct);


        var seasons = await visible.Where(Season.NotEndedAt(now))
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
