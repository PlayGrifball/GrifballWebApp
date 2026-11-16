using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Calendar;

/// <summary>
/// The calendar feeds of season matches (iCalendar, <see cref="IcsWriter"/>), which calendar apps subscribe to by URL: a
/// team's matches, a player's (their teams'), and a season's. Each scheduled match is an event at its time, a slot long
/// (<see cref="SeasonMatch.SlotLength"/>), under a UID of its own (<see cref="Uid"/>) so a match moved to another time
/// moves in the calendar too. An unscheduled match and a BYE, which is never played, are left out.
/// <para>
/// A calendar app fetches a feed signed in to nothing, and its URL is shared, so a feed holds only what a signed-out
/// visitor may see (<see cref="SeasonViewer.Public"/>) whoever asks for it: a season not announced yet is in none (#435),
/// and its team or the season itself is no feed at all, as one that doesn't exist.
/// </para>
/// </summary>
public class MatchCalendarService
{
    /// <summary>The right-hand side of every event's UID: what makes it the app's own among a calendar's events.</summary>
    private const string UidDomain = "grifballwebapp";

    private readonly IApplicationDbContext _context;
    private readonly SeasonVisibility _visibility;
    private readonly UrlService _urls;
    private readonly TimeProvider _time;

    /// <summary>The feeds of the matches <paramref name="context"/> holds, of the seasons <paramref name="visibility"/> shows
    /// the public, with links to the site (<paramref name="urls"/>); <paramref name="time"/> stamps a match never saved
    /// through the audit (the system clock when not given).</summary>
    public MatchCalendarService(IApplicationDbContext context, SeasonVisibility visibility, UrlService urls, TimeProvider? time = null)
    {
        _context = context;
        _visibility = visibility;
        _urls = urls;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>A season match's event UID, the same for as long as the match is: <c>seasonmatch-42@grifballwebapp</c>.</summary>
    public static string Uid(int seasonMatchID) => $"seasonmatch-{seasonMatchID}@{UidDomain}";

    /// <summary>The seasons a feed may show: the public's.</summary>
    private IQueryable<Season> PublicSeasons() => _visibility.Visible(SeasonViewer.Public);

    /// <summary>The team's scheduled matches, home and away; null when there is no such team, or it is of a season not announced yet.</summary>
    public async Task<string?> TeamAsync(int teamID, CancellationToken ct = default)
    {
        var seasons = PublicSeasons();
        var team = await _context.Teams.AsNoTracking()
            .Where(t => t.TeamID == teamID && seasons.Any(s => s.SeasonID == t.SeasonID))
            .Select(t => new { t.TeamName, t.Season.SeasonName })
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;

        var events = await EventsAsync(_context.SeasonMatches.Where(m => m.HomeTeamID == teamID || m.AwayTeamID == teamID), ct);
        return IcsWriter.Write(new CalendarFeed($"{team.TeamName} ({team.SeasonName})", events));
    }

    /// <summary>
    /// The scheduled matches of every team the player is on, of the seasons the public may see: a team of a season not
    /// announced yet adds none. Null when there is no such user; a player on no team has an empty calendar.
    /// </summary>
    public async Task<string?> PlayerAsync(int userID, CancellationToken ct = default)
    {
        var player = await _context.Users.AsNoTracking().Where(u => u.Id == userID).Select(User.Named).FirstOrDefaultAsync(ct);
        if (player is null)
            return null;

        var rosters = _context.Teams.Rosters();
        var matches = _context.SeasonMatches.Where(m => rosters.Any(p => p.UserID == userID && (p.TeamID == m.HomeTeamID || p.TeamID == m.AwayTeamID)));
        var events = await EventsAsync(matches, ct);
        var name = player.Name is { } shown ? $"{shown}'s matches" : "Grifball matches";
        return IcsWriter.Write(new CalendarFeed(name, events));
    }

    /// <summary>The season's scheduled matches, a bracket slot's too (its team still to come shown as TBD); null when there
    /// is no such season, or it isn't announced yet.</summary>
    public async Task<string?> SeasonAsync(int seasonID, CancellationToken ct = default)
    {
        var season = await PublicSeasons().Where(s => s.SeasonID == seasonID).Select(s => new { s.SeasonName }).FirstOrDefaultAsync(ct);
        if (season is null)
            return null;

        var events = await EventsAsync(_context.SeasonMatches.Where(m => m.SeasonID == seasonID), ct);
        return IcsWriter.Write(new CalendarFeed($"{season.SeasonName} schedule", events));
    }

    /// <summary>The events of the scheduled matches among <paramref name="matches"/> that a feed may show (of a public
    /// season, no BYE), soonest first.</summary>
    private async Task<List<CalendarEvent>> EventsAsync(IQueryable<SeasonMatch> matches, CancellationToken ct)
    {
        var seasons = PublicSeasons();
        var rows = await matches.AsNoTracking()
            .Where(m => m.ScheduledTime != null && seasons.Any(s => s.SeasonID == m.SeasonID))
            .Where(m => m.HomeTeamResult != SeasonMatchResult.Bye && m.AwayTeamResult != SeasonMatchResult.Bye)
            .OrderBy(m => m.ScheduledTime)
            .ThenBy(m => m.SeasonMatchID)
            .Select(m => new MatchRow
            {
                SeasonMatchID = m.SeasonMatchID,
                ScheduledTime = m.ScheduledTime!.Value,
                HomeTeamName = m.HomeTeam!.TeamName,
                AwayTeamName = m.AwayTeam!.TeamName,
                SeasonName = m.Season.SeasonName,
                BestOf = m.BestOf,
                ModifiedAt = m.ModifiedAt,
            })
            .ToListAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        return rows.ConvertAll(m => ToEvent(m, now));
    }

    private CalendarEvent ToEvent(MatchRow match, DateTime now)
    {
        var url = _urls.SeasonMatch(match.SeasonMatchID);
        return new CalendarEvent(
            Uid: Uid(match.SeasonMatchID),
            Start: match.ScheduledTime,
            End: match.ScheduledTime + SeasonMatch.SlotLength,
            Summary: $"{match.HomeTeamName ?? "TBD"} vs {match.AwayTeamName ?? "TBD"} ({match.SeasonName})",
            // The page in the notes too: Google Calendar shows no URL property
            Description: $"{match.SeasonName}, best of {match.BestOf}\n{url}",
            Url: url,
            // The audit stamps every save, a new time included; a row it never stamped (imported by hand) is as of now
            LastModified: match.ModifiedAt == default ? now : match.ModifiedAt);
    }

    /// <summary>What an event needs of a season match.</summary>
    private sealed class MatchRow
    {
        public required int SeasonMatchID { get; init; }
        public required DateTime ScheduledTime { get; init; }
        public required string? HomeTeamName { get; init; }
        public required string? AwayTeamName { get; init; }
        public required string SeasonName { get; init; }
        public required int BestOf { get; init; }
        public required DateTime ModifiedAt { get; init; }
    }
}
