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
    /// <summary>How long a game of a series takes, for its length (<see cref="LengthOf"/>).</summary>
    public const int MinutesPerGame = 15;

    private readonly HashSet<(DayOfWeek, TimeOnly)> _slots = [.. slots];

    /// <summary>How long a best-of-<paramref name="bestOf"/> series takes: all its games.</summary>
    public static TimeSpan LengthOf(int bestOf) => TimeSpan.FromMinutes(MinutesPerGame * Math.Max(bestOf, 1));

    /// <summary>What is wrong with <paramref name="match"/>'s time, given the season's <paramref name="others"/> (scheduled); none: it can stand.</summary>
    public IReadOnlyList<ScheduleConflict> Check(ScheduledMatch match, IEnumerable<ScheduledMatch> others)
    {
        var conflicts = new List<ScheduleConflict>();
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(match.StartsAt, DateTimeKind.Utc), league);
        var slot = (local.DayOfWeek, TimeOnly.FromDateTime(local));

        if (match.StartsAt < seasonStart || match.EndsAt > seasonEnd)
            conflicts.Add(new(ScheduleConflictKind.OutsideSeason, "It's outside the season's dates."));
        if (!_slots.Contains(slot))
            conflicts.Add(new(ScheduleConflictKind.NotASeasonTimeslot, "It isn't one of the season's timeslots."));

        var theirs = others.Where(o => o.SeasonMatchID != match.SeasonMatchID).ToList();
        if (theirs.Any(o => o.Shares(match) && o.Overlaps(match)))
            conflicts.Add(new(ScheduleConflictKind.TeamPlaysThen, "One of its teams plays another match then."));
        if (!CanPlay(match.HomeTeamID, slot))
            conflicts.Add(new(ScheduleConflictKind.HomeTeamUnavailable, "The home team isn't available then."));
        if (!CanPlay(match.AwayTeamID, slot))
            conflicts.Add(new(ScheduleConflictKind.AwayTeamUnavailable, "The away team isn't available then."));

        var night = DateOnly.FromDateTime(local);
        var week = Week(local);
        var sameTeams = theirs.Where(o => o.Shares(match)).Select(o => (Match: o, Local: Local(o))).ToList();
        foreach (var team in new[] { match.HomeTeamID, match.AwayTeamID })
        {
            var ofTeam = sameTeams.Where(o => o.Match.HomeTeamID == team || o.Match.AwayTeamID == team).ToList();
            if (ofTeam.Count(o => DateOnly.FromDateTime(o.Local) == night) >= rules.MaxMatchesPerTeamPerNight
                && conflicts.All(c => c.Kind != ScheduleConflictKind.TooManyTonight))
                conflicts.Add(new(ScheduleConflictKind.TooManyTonight, $"A team would play more than {rules.MaxMatchesPerTeamPerNight} that night."));
            if (ofTeam.Count(o => Week(o.Local) == week) >= rules.MaxMatchesPerTeamPerWeek
                && conflicts.All(c => c.Kind != ScheduleConflictKind.TooManyThisWeek))
                conflicts.Add(new(ScheduleConflictKind.TooManyThisWeek, $"A team would play more than {rules.MaxMatchesPerTeamPerWeek} that week."));
        }

        if (rules.MaxConcurrentMatches is { } cap && theirs.Count(o => o.Overlaps(match)) >= cap)
            conflicts.Add(new(ScheduleConflictKind.TooManyAtOnce, $"More than {cap} matches would run at once."));
        return conflicts;
    }

    private bool CanPlay(int teamID, (DayOfWeek, TimeOnly) slot) => teamSlots.TryGetValue(teamID, out var can) && can.Contains(slot);

    private DateTime Local(ScheduledMatch match) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(match.StartsAt, DateTimeKind.Utc), league);

    /// <summary>The week (Monday to Sunday) a league time falls in: its Monday.</summary>
    private static DateOnly Week(DateTime local)
        => DateOnly.FromDateTime(local).AddDays(-(((int)local.DayOfWeek + 6) % 7));
}
