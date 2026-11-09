using GrifballWebApp.Domain.Shared;
using System.Linq.Expressions;

namespace GrifballWebApp.Domain.League;

/// <summary>
/// A time for each of a season's regular matches, proposed by the schedule recommendation job (the genetic algorithm,
/// ScheduleService): its best schedule, how good it is, and whether a commissioner applied it to the matches. Made whole
/// by <see cref="Propose"/>, then applied once (<see cref="Apply"/>); its proposed times never change.
/// </summary>
[AggregateRoot]
public class ScheduleRecommendation : AuditableEntity
{
    /// <summary>For EF. A recommendation is made by <see cref="Propose"/>.</summary>
    private ScheduleRecommendation() { }

    /// <summary>
    /// The job's best schedule for season <paramref name="seasonID"/>, asked for by <paramref name="requestedBy"/>: its
    /// fitness (0 to 1), the generations it ran, and a proposed time for each match (one per match). Not applied.
    /// </summary>
    public static ScheduleRecommendation Propose(int seasonID, string? requestedBy, double fitness, int generations, IEnumerable<ProposedMatchTime> matches)
    {
        if (!(fitness is >= 0 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(fitness), fitness, "A schedule's fitness is 0 to 1");
        ArgumentOutOfRangeException.ThrowIfNegative(generations);
        ArgumentNullException.ThrowIfNull(matches);

        var recommendation = new ScheduleRecommendation
        {
            SeasonID = seasonID,
            RequestedBy = requestedBy,
            Fitness = fitness,
            Generations = generations,
        };
        foreach (var match in matches)
        {
            if (recommendation._matches.Any(m => m.SeasonMatchID == match.SeasonMatchID))
                throw new ArgumentException($"Season match {match.SeasonMatchID} is proposed twice", nameof(matches));
            recommendation._matches.Add(new ScheduleRecommendationMatch(recommendation, match));
        }
        return recommendation;
    }

    // Nothing outside the recommendation sets its state, its foreign key or its navigations: only its own methods, and EF
    // (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int ScheduleRecommendationID { get; internal set; }
    public int SeasonID { get; private set; }
    public Season Season { get; private set; } = null!;
    /// <summary>Who asked for it (a user name).</summary>
    public string? RequestedBy { get; private set; }
    /// <summary>0 to 1: 1 is a schedule with no overlap and every team available (SeasonScheduleEvaluator)
    /// (CK_Event_ScheduleRecommendations_FitnessInRange).</summary>
    public double Fitness { get; private set; }
    /// <summary>The generations the algorithm ran (CK_Event_ScheduleRecommendations_GenerationsNotNegative).</summary>
    public int Generations { get; private set; }
    /// <summary>When its times were set on the matches (<see cref="Apply"/>); null until then.</summary>
    public DateTime? AppliedAt { get; private set; }
    /// <summary>Who applied it; null until then, and when nobody signed in did
    /// (CK_Event_ScheduleRecommendations_AppliedByOnlyWhenApplied).</summary>
    public string? AppliedBy { get; private set; }
    /// <summary>Read-only: made with the recommendation (<see cref="Propose"/>), never changed (EF uses the field).</summary>
    public IReadOnlyCollection<ScheduleRecommendationMatch> Matches => _matches;
    private readonly List<ScheduleRecommendationMatch> _matches = [];

    /// <summary>Not applied yet: its times can still be set on the matches. For queries.</summary>
    public static readonly Expression<Func<ScheduleRecommendation, bool>> Unapplied = r => r.AppliedAt == null;

    private static readonly Func<ScheduleRecommendation, bool> _isUnapplied = Unapplied.Compile();

    /// <summary><see cref="Unapplied"/>, for a loaded recommendation.</summary>
    public bool IsApplied => !_isUnapplied(this);

    /// <summary>A match's conflict: it overlaps another of either team's matches, or a team isn't available then.</summary>
    public int ConflictCount => _matches.Count(m => m.HasConflict);

    /// <summary>The time proposed for each season match, by its ID.</summary>
    public IReadOnlyDictionary<int, DateTime> ProposedTimes() => _matches.ToDictionary(m => m.SeasonMatchID, m => m.ScheduledTime);

    /// <summary>
    /// Stamps it applied, by <paramref name="by"/> (may be unknown) at <paramref name="at"/>: its times are set on the
    /// matches. Once: one applied already stays as it was.
    /// </summary>
    public ScheduleRecommendationApply Apply(string? by, DateTime at)
    {
        if (IsApplied)
            return ScheduleRecommendationApply.AlreadyApplied;
        AppliedAt = at;
        AppliedBy = by;
        return ScheduleRecommendationApply.Applied;
    }
}

/// <summary>What <see cref="ScheduleRecommendation.Apply"/> did.</summary>
public enum ScheduleRecommendationApply
{
    Applied = 1,
    AlreadyApplied = 2,
}

/// <summary>A match's proposed time in a new <see cref="ScheduleRecommendation"/> (<see cref="ScheduleRecommendation.Propose"/>),
/// and what is wrong with it.</summary>
public readonly record struct ProposedMatchTime(int SeasonMatchID, DateTime ScheduledTime, bool Overlaps, bool HomeTeamAvailable, bool AwayTeamAvailable);

/// <summary>One match's proposed time in a <see cref="ScheduleRecommendation"/>, and what is wrong with it. Made by its
/// recommendation, never changed.</summary>
[AggregatePart(typeof(ScheduleRecommendation))]
public class ScheduleRecommendationMatch
{
    /// <summary>For EF.</summary>
    private ScheduleRecommendationMatch() { }

    internal ScheduleRecommendationMatch(ScheduleRecommendation recommendation, ProposedMatchTime match)
    {
        ScheduleRecommendation = recommendation;
        SeasonMatchID = match.SeasonMatchID;
        ScheduledTime = match.ScheduledTime;
        Overlaps = match.Overlaps;
        HomeTeamAvailable = match.HomeTeamAvailable;
        AwayTeamAvailable = match.AwayTeamAvailable;
    }

    public int ScheduleRecommendationMatchID { get; internal set; }
    public int ScheduleRecommendationID { get; private set; }
    public ScheduleRecommendation ScheduleRecommendation { get; private set; } = null!;
    /// <summary>The season match (no foreign key: a match deleted since is skipped when the times are applied).</summary>
    public int SeasonMatchID { get; private set; }
    public DateTime ScheduledTime { get; private set; }
    /// <summary>Overlaps another of either team's matches.</summary>
    public bool Overlaps { get; private set; }
    public bool HomeTeamAvailable { get; private set; }
    public bool AwayTeamAvailable { get; private set; }

    /// <summary>Overlaps, or a team isn't available then.</summary>
    public bool HasConflict => Overlaps || !HomeTeamAvailable || !AwayTeamAvailable;
}
