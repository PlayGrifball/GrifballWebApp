using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Scheduling;

/// <summary>What is wrong with a match's time (<see cref="ScheduleChecker.Check"/>).</summary>
public enum ScheduleConflictKind
{
    /// <summary>Before the season starts or after it ends: refused.</summary>
    OutsideSeason = 1,
    /// <summary>Not one of the season's weekly timeslots.</summary>
    NotASeasonTimeslot,
    /// <summary>One of its teams plays another match then: refused.</summary>
    TeamPlaysThen,
    HomeTeamUnavailable,
    AwayTeamUnavailable,
    /// <summary>One of its teams would play more matches that night than the season allows.</summary>
    TooManyTonight,
    /// <summary>One of its teams would play more matches that week than the season allows.</summary>
    TooManyThisWeek,
    /// <summary>More matches would run at once than the season allows.</summary>
    TooManyAtOnce,
}

/// <summary>One thing wrong with a match's time, in words; <see cref="Refuses"/>: a time that can't stand, the rest are warnings.</summary>
public sealed record ScheduleConflict(ScheduleConflictKind Kind, string Message)
{
    public bool Refuses => Kind is ScheduleConflictKind.OutsideSeason or ScheduleConflictKind.TeamPlaysThen;
}

/// <summary>A match at a time (UTC), and how long it lasts.</summary>
public sealed record ScheduledMatch(int SeasonMatchID, int HomeTeamID, int AwayTeamID, DateTime StartsAt, TimeSpan Length)
{
    public DateTime EndsAt => StartsAt + Length;

    public bool Shares(ScheduledMatch other)
        => HomeTeamID == other.HomeTeamID || HomeTeamID == other.AwayTeamID || AwayTeamID == other.HomeTeamID || AwayTeamID == other.AwayTeamID;

    public bool Overlaps(ScheduledMatch other) => StartsAt < other.EndsAt && other.StartsAt < EndsAt;
}

/// <summary>
/// The one set of rules a match's time is checked against, whoever sets it (the season scheduler, staff by hand, a
/// reschedule): the season's window and its weekly timeslots, its teams' other matches and availability
/// (<see cref="TeamAvailabilityService"/>), and the season's caps (<see cref="SeasonSchedulingRules"/>). Times are UTC;
/// timeslots, nights and weeks (Monday to Sunday) are the league's, in <paramref name="league"/>.
/// </summary>
/// <param name="seasonStart">The season's start (UTC).</param>
/// <param name="seasonEnd">The season's end (UTC).</param>
/// <param name="rules">The season's caps.</param>
/// <param name="slots">The season's weekly timeslots, in league time.</param>
/// <param name="teamSlots">Each team's timeslots it can play (in league time), by its ID; a team missing can play none.</param>
/// <param name="league">The league's time zone.</param>
public sealed class ScheduleChecker(DateTime seasonStart, DateTime seasonEnd, SeasonSchedulingRules rules, IReadOnlyCollection<(DayOfWeek Day, TimeOnly Time)> slots,
    IReadOnlyDictionary<int, IReadOnlySet<(DayOfWeek Day, TimeOnly Time)>> teamSlots, TimeZoneInfo league)
{
    private readonly HashSet<(DayOfWeek, TimeOnly)> _slots = [.. slots];

    /// <summary>How much time scheduling keeps for a best-of-<paramref name="bestOf"/> series: <see cref="SeasonMatch.ScheduledLength"/>,
    /// its games and the lobby before them (the calendar shows the games alone).</summary>
    public static TimeSpan LengthOf(int bestOf) => SeasonMatch.ScheduledLength(bestOf);

    /// <summary>What is wrong with <paramref name="match"/>'s time, given the season's <paramref name="others"/> (scheduled); none: it can stand.</summary>
    public IReadOnlyList<ScheduleConflict> Check(ScheduledMatch match, IEnumerable<ScheduledMatch> others)
    {
        var local = Local(match);
        var slot = (local.DayOfWeek, TimeOnly.FromDateTime(local));
        var theirs = others.Where(o => o.SeasonMatchID != match.SeasonMatchID).ToList();
        return [.. Season(match, slot), .. Teams(match, slot, theirs), .. Caps(match, local, theirs)];
    }

    /// <summary>Its time against the season's dates and weekly timeslots: each only when the season has them (an imported one may not).</summary>
    private IEnumerable<ScheduleConflict> Season(ScheduledMatch match, (DayOfWeek, TimeOnly) slot)
    {
        if (seasonEnd > seasonStart && (match.StartsAt < seasonStart || match.EndsAt > seasonEnd))
            yield return new(ScheduleConflictKind.OutsideSeason, "It's outside the season's dates.");
        if (_slots.Count > 0 && !_slots.Contains(slot))
            yield return new(ScheduleConflictKind.NotASeasonTimeslot, "It isn't one of the season's timeslots.");
    }

    /// <summary>Its time against its teams' other matches and availability.</summary>
    private IEnumerable<ScheduleConflict> Teams(ScheduledMatch match, (DayOfWeek, TimeOnly) slot, List<ScheduledMatch> others)
    {
        if (others.Any(o => o.Shares(match) && o.Overlaps(match)))
            yield return new(ScheduleConflictKind.TeamPlaysThen, "One of its teams plays another match then.");
        // Availability is of the season's timeslots: a season without them has none to check
        if (_slots.Count == 0)
            yield break;
        if (!CanPlay(match.HomeTeamID, slot))
            yield return new(ScheduleConflictKind.HomeTeamUnavailable, "The home team isn't available then.");
        if (!CanPlay(match.AwayTeamID, slot))
            yield return new(ScheduleConflictKind.AwayTeamUnavailable, "The away team isn't available then.");
    }

    /// <summary>Its time against the season's caps: a team's matches a night and a week, and matches at once.</summary>
    private IEnumerable<ScheduleConflict> Caps(ScheduledMatch match, DateTime local, List<ScheduledMatch> others)
    {
        var night = DateOnly.FromDateTime(local);
        var week = Week(local);
        // Each team's other matches, in league time: the most of either team's counts
        var perTeam = new[] { match.HomeTeamID, match.AwayTeamID }
            .Select(team => others.Where(o => o.HomeTeamID == team || o.AwayTeamID == team).Select(Local).ToList())
            .ToList();
        if (perTeam.Max(m => m.Count(t => DateOnly.FromDateTime(t) == night)) >= rules.MaxMatchesPerTeamPerNight)
            yield return new(ScheduleConflictKind.TooManyTonight, $"A team would play more than {rules.MaxMatchesPerTeamPerNight} that night.");
        if (perTeam.Max(m => m.Count(t => Week(t) == week)) >= rules.MaxMatchesPerTeamPerWeek)
            yield return new(ScheduleConflictKind.TooManyThisWeek, $"A team would play more than {rules.MaxMatchesPerTeamPerWeek} that week.");
        if (rules.MaxConcurrentMatches is { } cap && others.Count(o => o.Overlaps(match)) >= cap)
            yield return new(ScheduleConflictKind.TooManyAtOnce, $"More than {cap} matches would run at once.");
    }

    private bool CanPlay(int teamID, (DayOfWeek, TimeOnly) slot) => teamSlots.TryGetValue(teamID, out var can) && can.Contains(slot);

    private DateTime Local(ScheduledMatch match) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(match.StartsAt, DateTimeKind.Utc), league);

    /// <summary>The week (Monday to Sunday) a league time falls in: its Monday.</summary>
    private static DateOnly Week(DateTime local)
        => DateOnly.FromDateTime(local).AddDays(-(((int)local.DayOfWeek + 6) % 7));
}
