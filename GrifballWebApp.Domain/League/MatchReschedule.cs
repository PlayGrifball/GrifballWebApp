using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using System.Linq.Expressions;

namespace GrifballWebApp.Domain.League;

/// <summary>
/// A request to move a season match to another time, which a commissioner approves or rejects. A match has one pending
/// request at most (a filtered unique index): its pending one is its active one, no pointer kept beside it. Made by
/// <see cref="Request"/> and decided by <see cref="Decide"/>, the only ways its state changes.
/// </summary>
// Adds itself to its match's requests (SeasonMatch.AddReschedule), the inverse of its own match link
[AggregateRoot]
[AggregateAccess(typeof(SeasonMatch))]
public class MatchReschedule : AuditableEntity
{
    /// <summary>For EF. A request is made by <see cref="Request"/>.</summary>
    private MatchReschedule() { }

    /// <summary>
    /// A pending request by <paramref name="requestedByUserID"/> to move <paramref name="seasonMatch"/> to
    /// <paramref name="newScheduledTime"/>; refused when the match can't be rescheduled
    /// (<see cref="WhyNotReschedulable"/>) or already has a pending request. The match's requests must be loaded.
    /// </summary>
    public static RescheduleRequestResult Request(SeasonMatch seasonMatch, DateTime? newScheduledTime, string reason, int requestedByUserID, DateTime requestedAt)
    {
        if (WhyNotReschedulable(seasonMatch) is { } why)
            return new(null, why);
        if (seasonMatch.PendingReschedule is not null)
            return new(null, AlreadyActive);

        var reschedule = new MatchReschedule
        {
            SeasonMatch = seasonMatch,
            OriginalScheduledTime = seasonMatch.ScheduledTime,
            NewScheduledTime = newScheduledTime,
            Reason = reason,
            RequestedByUserID = requestedByUserID,
            RequestedAt = requestedAt,
        };
        seasonMatch.AddReschedule(reschedule);
        return new(reschedule, null);
    }

    /// <summary>The same by the match's key alone, its rules unchecked: for tests, which seed requests of any match.</summary>
    internal static MatchReschedule Request(int seasonMatchID, DateTime? originalScheduledTime, DateTime? newScheduledTime, string reason, int requestedByUserID, DateTime requestedAt) => new()
    {
        SeasonMatchID = seasonMatchID,
        OriginalScheduledTime = originalScheduledTime,
        NewScheduledTime = newScheduledTime,
        Reason = reason,
        RequestedByUserID = requestedByUserID,
        RequestedAt = requestedAt,
    };

    /// <summary>Why a request is refused when the match has a pending one already.</summary>
    public const string AlreadyActive = "There is already an active reschedule request for this match.";

    // Nothing outside the request sets its state, its foreign keys or its navigations: only Request, Decide and AttachThread,
    // and EF (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int MatchRescheduleID { get; internal set; }
    public int SeasonMatchID { get; private set; }
    public SeasonMatch SeasonMatch { get; private set; } = null!;

    /// <summary>The match's time when the request was made.</summary>
    public DateTime? OriginalScheduledTime { get; private set; }

    /// <summary>The time asked for; the match's on approval.</summary>
    public DateTime? NewScheduledTime { get; private set; }

    public string Reason { get; private set; } = null!;

    public int RequestedByUserID { get; private set; }
    public User RequestedByUser { get; private set; } = null!;

    public DateTime RequestedAt { get; private set; }

    /// <summary>Pending until <see cref="Decide"/>; a pending request is the match's active one.</summary>
    public RescheduleStatus Status { get; private set; } = RescheduleStatus.Pending;

    /// <summary>The commissioner who approved or rejected it; null while pending.</summary>
    public int? ApprovedByUserID { get; private set; }
    public User? ApprovedByUser { get; private set; }

    /// <summary>When it was approved or rejected; null while pending.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>The Discord thread it's discussed in, once made (<see cref="AttachThread"/>).</summary>
    public ulong? DiscordThreadID { get; private set; }

    /// <summary>The commissioner's notes on the decision.</summary>
    public string? CommissionerNotes { get; private set; }

    /// <summary>A pending request: the match's active one (for queries). <see cref="AwaitsDecision"/> is the same rule on a loaded request.</summary>
    public static readonly Expression<Func<MatchReschedule, bool>> IsPending = r => r.Status == RescheduleStatus.Pending;

    private static readonly Func<MatchReschedule, bool> isPending = IsPending.Compile();

    /// <summary>
    /// Whether this request is pending, the match's active one: <see cref="IsPending"/> on a loaded request, so the rule is
    /// written once. Computed, not stored (ignored by EF).
    /// </summary>
    public bool AwaitsDecision => isPending(this);

    /// <summary>
    /// Why this match cannot be rescheduled, or null when it can: it needs both teams (a bracket match waiting on an
    /// earlier result has none yet) and no result (a decided match, a forfeit or a BYE). The match's own rules
    /// (<see cref="SeasonMatch.HasBothTeams"/>, <see cref="SeasonMatch.IsDecided"/>) say which.
    /// </summary>
    public static string? WhyNotReschedulable(SeasonMatch seasonMatch)
    {
        if (!seasonMatch.HasBothTeams)
            return "This match does not have both teams yet.";
        if (seasonMatch.IsDecided)
            return "This match already has a result.";
        return null;
    }

    /// <summary>
    /// A commissioner's decision. An approval moves the match to the time asked for, so a match decided since the request
    /// can only be rejected. Only a pending request is decided. The match must be loaded.
    /// </summary>
    public RescheduleDecision Decide(bool approved, int commissionerUserID, string? notes, DateTime decidedAt)
    {
        if (!AwaitsDecision)
            return RescheduleDecision.AlreadyDecided;
        if (approved && WhyNotReschedulable(SeasonMatch) is not null)
            return RescheduleDecision.MatchNotReschedulable;

        Status = approved ? RescheduleStatus.Approved : RescheduleStatus.Rejected;
        ApprovedByUserID = commissionerUserID;
        ProcessedAt = decidedAt;
        CommissionerNotes = notes;
        if (approved && NewScheduledTime is { } time)
            SeasonMatch.ScheduleAt(time);
        return approved ? RescheduleDecision.Approved : RescheduleDecision.Rejected;
    }

    /// <summary>The note on a request closed because its match was decided first (<see cref="CloseBecauseDecided"/>).</summary>
    public const string ClosedBecauseDecided = "Closed automatically: the match was decided before this request was.";

    /// <summary>
    /// Its match was decided while it waited (#297): a decided match keeps its time, so there is nothing left to approve.
    /// The request is closed as a rejection, by its requester (the decision is made by reports, forfeits and bracket
    /// advances, which don't name a user), at <paramref name="decidedAt"/>, with <see cref="ClosedBecauseDecided"/> as its
    /// note. Only its match calls this, as it is decided; a request already decided is left as it is.
    /// </summary>
    internal void CloseBecauseDecided(DateTime decidedAt)
    {
        if (!AwaitsDecision)
            return;
        Status = RescheduleStatus.Rejected;
        ApprovedByUserID = RequestedByUserID;
        ProcessedAt = decidedAt;
        CommissionerNotes = ClosedBecauseDecided;
    }

    /// <summary>Records the Discord thread made for it; a request has one thread, so a second is refused.</summary>
    public void AttachThread(ulong threadID)
    {
        if (DiscordThreadID is { } current && current != threadID)
            throw new InvalidOperationException($"Reschedule {MatchRescheduleID} already has thread {current}");
        DiscordThreadID = threadID;
    }

    /// <summary>
    /// Moves what <paramref name="fromUserID"/> did to <paramref name="toUserID"/> (a user merge): the request they made,
    /// the decision they made.
    /// </summary>
    public void ReassignUser(int fromUserID, int toUserID)
    {
        if (RequestedByUserID == fromUserID)
        {
            RequestedByUserID = toUserID;
            RequestedByUser = null!;
        }
        if (ApprovedByUserID == fromUserID)
        {
            ApprovedByUserID = toUserID;
            ApprovedByUser = null;
        }
    }
}

/// <summary>What <see cref="MatchReschedule.Request"/> made: the request, or why none was made.</summary>
public sealed record RescheduleRequestResult(MatchReschedule? Reschedule, string? Refused);

/// <summary>What <see cref="MatchReschedule.Decide"/> did.</summary>
public enum RescheduleDecision
{
    Approved,
    Rejected,
    /// <summary>It was approved or rejected already; nothing changed.</summary>
    AlreadyDecided,
    /// <summary>An approval refused: the match has a result now (or lost a team), so its time can't move.</summary>
    MatchNotReschedulable,
}

public enum RescheduleStatus
{
    /// <summary>
    /// Reschedule request is pending commissioner approval
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Reschedule request has been approved
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Reschedule request has been rejected
    /// </summary>
    Rejected = 2,
}
