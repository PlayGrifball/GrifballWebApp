#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A season an import would remove rows from: what it would remove, and the sysadmin's decision when the guardrail
/// stopped it (or the preview of a manual import). Bookkeeping of its job, removed with it: not temporal.
/// </summary>
public class HistoricalSeasonReview : AuditableEntity
{
    public int HistoricalSeasonReviewID { get; set; }
    public int HistoricalImportJobID { get; set; }
    /// <summary>The season's HistoricalSourceID.</summary>
    public string SeasonKey { get; set; }
    public string SeasonName { get; set; }
    public HistoricalSeasonReviewStatus Status { get; set; }
    public int StatsBefore { get; set; }
    public int StatsRemoved { get; set; }
    public int TeamsBefore { get; set; }
    public int TeamsRemoved { get; set; }
    public int TitlesBefore { get; set; }
    public int TitlesRemoved { get; set; }
    /// <summary>The import job approving it queued.</summary>
    public int? ApprovalJobID { get; set; }
    public DateTime? DecidedAt { get; set; }

    public virtual HistoricalImportJob Job { get; set; }
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
