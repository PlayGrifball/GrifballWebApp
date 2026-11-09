using GrifballWebApp.Domain.Shared;
namespace GrifballWebApp.Domain.Archive;

/// <summary>
/// A season an import would remove rows from: what it would remove, and the sysadmin's decision when the guardrail
/// stopped it (or the preview of a manual import). Bookkeeping of its job, removed with it: not temporal.
/// Part of its job: made by <see cref="HistoricalImportJob.AddReview"/>, decided by <see cref="HistoricalImportJob.DecideReview"/>.
/// </summary>
[AggregatePart(typeof(HistoricalImportJob))]
public class HistoricalSeasonReview : AuditableEntity
{
    public const int MaxSeasonKeyLength = 200;
    public const int MaxSeasonNameLength = 200;

    /// <summary>For EF.</summary>
    private HistoricalSeasonReview() { }

    internal HistoricalSeasonReview(HistoricalImportJob job, string seasonKey, string? seasonName, HistoricalSeasonReviewStatus status, HistoricalSeasonRemovals removals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonKey);
        Job = job;
        HistoricalImportJobID = job.HistoricalImportJobID;
        SeasonKey = seasonKey.Length > MaxSeasonKeyLength ? seasonKey[..MaxSeasonKeyLength] : seasonKey;
        SeasonName = seasonName is { Length: > MaxSeasonNameLength } ? seasonName[..MaxSeasonNameLength] : seasonName;
        Status = status;
        StatsBefore = removals.StatsBefore;
        StatsRemoved = removals.StatsRemoved;
        TeamsBefore = removals.TeamsBefore;
        TeamsRemoved = removals.TeamsRemoved;
        TitlesBefore = removals.TitlesBefore;
        TitlesRemoved = removals.TitlesRemoved;
    }

    public int HistoricalSeasonReviewID { get; internal set; }
    public int HistoricalImportJobID { get; private set; }
    /// <summary>The season's HistoricalSourceID.</summary>
    public string SeasonKey { get; private set; } = null!;
    public string? SeasonName { get; private set; }
    public HistoricalSeasonReviewStatus Status { get; private set; }
    public int StatsBefore { get; private set; }
    public int StatsRemoved { get; private set; }
    public int TeamsBefore { get; private set; }
    public int TeamsRemoved { get; private set; }
    public int TitlesBefore { get; private set; }
    public int TitlesRemoved { get; private set; }
    /// <summary>The import job approving it queued; only an approved review has one
    /// (CK_Event_HistoricalSeasonReviews_ApprovalJobOnlyWhenApproved).</summary>
    public int? ApprovalJobID { get; private set; }
    /// <summary>When a sysadmin approved or skipped it; null until then (CK_Event_HistoricalSeasonReviews_DecidedOnceDecided).</summary>
    public DateTime? DecidedAt { get; private set; }

    public virtual HistoricalImportJob Job { get; private set; } = null!;

    /// <summary>Approves or skips a season the guardrail stopped; false when it isn't waiting for a decision.</summary>
    internal bool Decide(bool approve, DateTime decidedAt)
    {
        if (Status != HistoricalSeasonReviewStatus.NeedsReview)
            return false;
        Status = approve ? HistoricalSeasonReviewStatus.Approved : HistoricalSeasonReviewStatus.Skipped;
        DecidedAt = decidedAt;
        return true;
    }

    internal void ApprovalQueued(int approvalJobID)
    {
        if (Status != HistoricalSeasonReviewStatus.Approved)
            throw new InvalidOperationException($"Review {HistoricalSeasonReviewID} is {Status}: only an approved one queues an import");
        if (ApprovalJobID is not null)
            throw new InvalidOperationException($"Review {HistoricalSeasonReviewID}'s import was queued already, as job {ApprovalJobID}");
        ApprovalJobID = approvalJobID;
    }
}

public enum HistoricalSeasonReviewStatus
{
    /// <summary>A manual import's dry run: shown before it is applied.</summary>
    Preview = 1,
    /// <summary>Stopped by the guardrail: left as it was until a sysadmin decides.</summary>
    NeedsReview = 2,
    Approved = 3,
    Skipped = 4,
}
