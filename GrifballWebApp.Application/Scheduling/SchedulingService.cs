using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Scheduling;

/// <summary>
/// One of a season's timeslots suggested for a match (<see cref="SchedulingService.SuggestAsync"/>): when (UTC), what is wrong
/// with it (warnings only: a refused one isn't suggested), and how many of each team's players can play it.
/// </summary>
public sealed record TimeSuggestion(DateTime StartsAt, IReadOnlyList<ScheduleConflict> Conflicts, int HomePlayersAvailable, int AwayPlayersAvailable);

/// <summary>
/// A season's schedule, checked and suggested: a match's time against the season's rules (<see cref="ScheduleChecker"/>, with
/// its teams' availability from <see cref="TeamAvailabilityService"/>), and the season's timeslots ranked for a match. The season
/// scheduler, staff setting a time and reschedules all go through it.
/// </summary>
public sealed class SchedulingService(IApplicationDbContext context, TimeProvider time)
{
    /// <summary>How many timeslots <see cref="SuggestAsync"/> gives unless asked for another number.</summary>
    public const int DefaultSuggestions = 5;

    /// <summary>A season loaded for checking: its checker, its matches with a time, its teams' timeslots and its own (league time).</summary>
    public sealed record LoadedSeason(ScheduleChecker Checker, IReadOnlyList<ScheduledMatch> Scheduled, IReadOnlyDictionary<int, TeamSlots> Teams,
        IReadOnlyList<(DayOfWeek Day, TimeOnly Time)> Slots, DateTime Start, DateTime End);

    /// <summary><paramref name="seasonID"/> loaded for checking; null when there is no such season.</summary>
    public async Task<LoadedSeason?> LoadAsync(int seasonID, CancellationToken ct = default)
    {
        var season = await context.Seasons.AsNoTracking().Where(s => s.SeasonID == seasonID)
            .Select(s => new { s.SeasonStart, s.SeasonEnd }).SingleOrDefaultAsync(ct);
        if (season is null)
            return null;
        // The rules and timeslots once: the teams' are worked out from them
        var availability = new TeamAvailabilityService(context);
        var rules = await new SeasonSchedulingRulesService(context).ForAsync(seasonID, ct);
        var slots = await availability.SeasonSlotsAsync(seasonID, ct);
        var teams = await availability.ForSeasonAsync(seasonID, rules, slots, null, ct);
        var scheduled = await context.SeasonMatches.AsNoTracking()
            .Where(m => m.SeasonID == seasonID && m.ScheduledTime != null && m.HomeTeamID != null && m.AwayTeamID != null)
            .Select(m => new { m.SeasonMatchID, m.HomeTeamID, m.AwayTeamID, m.ScheduledTime, m.BestOf })
            .ToListAsync(ct);

        var seasonSlots = slots.Select(s => (s.DayOfWeek, s.Time)).ToList();
        var teamSlots = teams.ToDictionary(t => t.Key,
            t => (IReadOnlySet<(DayOfWeek Day, TimeOnly Time)>)t.Value.Slots.Where(s => s.Available).Select(s => (s.DayOfWeek, s.Time)).ToHashSet());
        var checker = new ScheduleChecker(season.SeasonStart, season.SeasonEnd, rules, seasonSlots, teamSlots, ScheduleService.LeagueTimeZone());
        return new LoadedSeason(checker,
            scheduled.Select(m => new ScheduledMatch(m.SeasonMatchID, m.HomeTeamID!.Value, m.AwayTeamID!.Value, m.ScheduledTime!.Value, ScheduleChecker.LengthOf(m.BestOf))).ToList(),
            teams, seasonSlots, season.SeasonStart, season.SeasonEnd);
    }

    /// <summary>What is wrong with <paramref name="seasonMatchID"/> at <paramref name="at"/> (UTC); null when there's no such match with both teams.</summary>
    public async Task<IReadOnlyList<ScheduleConflict>?> CheckAsync(int seasonMatchID, DateTime at, CancellationToken ct = default)
    {
        if (await MatchAsync(seasonMatchID, ct) is not { } match || await LoadAsync(match.SeasonID, ct) is not { } season)
            return null;
        return season.Checker.Check(match.At(at), season.Scheduled);
    }

    /// <summary>
    /// The season's timeslots for <paramref name="seasonMatchID"/>, from now (or <paramref name="from"/>) to the season's end (or
    /// before <paramref name="to"/>), best first: no warnings first, then the most of both teams' players able to play, then the
    /// soonest; a refused one isn't among them. At most <paramref name="count"/>. Null when there's no such match with both teams.
    /// </summary>
    public async Task<IReadOnlyList<TimeSuggestion>?> SuggestAsync(int seasonMatchID, DateTime? from = null, DateTime? to = null, int count = DefaultSuggestions,
        CancellationToken ct = default)
    {
        if (await MatchAsync(seasonMatchID, ct) is not { } match || await LoadAsync(match.SeasonID, ct) is not { } season)
            return null;
        var now = time.GetUtcNow().UtcDateTime;
        // A caller's times as UTC: JSON reads one with an offset other than Z as local
        from = Utc(from);
        to = Utc(to);
        var start = new[] { season.Start, now, from ?? DateTime.MinValue }.Max();
        var end = to is { } before && before < season.End ? before : season.End;
        // Each of the season's teams has each of its timeslots (TeamAvailabilityService.ForSeasonAsync)
        var home = season.Teams[match.HomeTeamID];
        var away = season.Teams[match.AwayTeamID];

        return Instants(season.Slots, start, end)
            .Select(i => (i.At, Conflicts: season.Checker.Check(match.At(i.At), season.Scheduled), i.Slot))
            .Where(s => !s.Conflicts.Any(c => c.Refuses))
            .Select(s => new TimeSuggestion(s.At, s.Conflicts, Players(home, s.Slot), Players(away, s.Slot)))
            .OrderBy(s => s.Conflicts.Count)
            .ThenByDescending(s => s.HomePlayersAvailable + s.AwayPlayersAvailable)
            .ThenBy(s => s.StartsAt)
            .Take(count)
            .ToList();
    }

    /// <summary>Each of the season's weekly timeslots as a UTC time from <paramref name="start"/> to before <paramref name="end"/>, with the slot it is.</summary>
    private static IEnumerable<(DateTime At, (DayOfWeek Day, TimeOnly Time) Slot)> Instants(IReadOnlyList<(DayOfWeek Day, TimeOnly Time)> slots, DateTime start, DateTime end)
    {
        var league = ScheduleService.LeagueTimeZone();
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, league));
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(end, league));
        for (var day = first; day <= last; day = day.AddDays(1))
            foreach (var slot in slots.Where(s => s.Day == day.DayOfWeek).OrderBy(s => s.Time))
            {
                var at = ScheduleService.ToUtc(day.ToDateTime(slot.Time));
                if (at >= start && at < end)
                    yield return (at, slot);
            }
    }

    private static DateTime? Utc(DateTime? time)
    {
        if (time is not { } t)
            return null;
        return t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);
    }

    /// <summary>How many of the team's players can play the slot: one of the season's, which the team has each of.</summary>
    private static int Players(TeamSlots team, (DayOfWeek Day, TimeOnly Time) slot)
        => team.Slots.First(s => s.DayOfWeek == slot.Day && s.Time == slot.Time).PlayersAvailable;

    private async Task<Match?> MatchAsync(int seasonMatchID, CancellationToken ct)
    {
        var match = await context.SeasonMatches.AsNoTracking()
            .Where(m => m.SeasonMatchID == seasonMatchID && m.HomeTeamID != null && m.AwayTeamID != null)
            .Select(m => new { m.SeasonMatchID, m.SeasonID, m.HomeTeamID, m.AwayTeamID, m.BestOf })
            .SingleOrDefaultAsync(ct);
        return match is null ? null : new Match(match.SeasonMatchID, match.SeasonID, match.HomeTeamID!.Value, match.AwayTeamID!.Value, match.BestOf);
    }

    /// <summary>The match being checked: its teams and its series, put at a time.</summary>
    private sealed record Match(int SeasonMatchID, int SeasonID, int HomeTeamID, int AwayTeamID, int BestOf)
    {
        public ScheduledMatch At(DateTime at) => new(SeasonMatchID, HomeTeamID, AwayTeamID, at, ScheduleChecker.LengthOf(BestOf));
    }
}
