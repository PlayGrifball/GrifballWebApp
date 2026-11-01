#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A historical import run in the background (HistoricalImportWorker). The uploaded archive is kept in
/// <see cref="Payload"/> until the job ends, so a job survives a restart and any replica can run it.
/// Not temporal: its progress changes often and the payload is large.
/// </summary>
public class HistoricalImportJob : AuditableEntity
{
    public int HistoricalImportJobID { get; set; }
    public HistoricalImportJobStatus Status { get; set; } = HistoricalImportJobStatus.Queued;
    /// <summary>The archive (HistoricalArchiveInput as JSON); cleared when the job ends.</summary>
    public string Payload { get; set; }
    /// <summary>What it is doing, e.g. "Pulling Waypoint matches 120/902".</summary>
    public string Stage { get; set; }
    public int SeasonsDone { get; set; }
    public int SeasonsTotal { get; set; }
    /// <summary>The HistoricalImportReport, as JSON, once it ends.</summary>
    public string Report { get; set; }
    public string Error { get; set; }
    public DateTime? StartedAt { get; set; }
    /// <summary>Updated while it runs: a Running job whose heartbeat is old was left by a stopped process and is queued again.</summary>
    public DateTime? HeartbeatAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    /// <summary>What it does: an import (of <see cref="Payload"/>, or of the sources' snapshots), a refresh of the sources,
    /// or a dry run of an import.</summary>
    public HistoricalImportJobKind Kind { get; set; } = HistoricalImportJobKind.Import;
    public HistoricalImportJobTrigger Trigger { get; set; } = HistoricalImportJobTrigger.Manual;
    /// <summary>A season whose import would remove too much is stopped for review instead (the scheduled imports).</summary>
    public bool Guarded { get; set; }
    /// <summary>The snapshots an import or dry run reads (comma-separated IDs), so applying a preview imports what was
    /// previewed; null reads each source's latest.</summary>
    public string SnapshotIDs { get; set; }
    /// <summary>The seasons (HistoricalSourceIDs, one per line) it imports; null is all of them.</summary>
    public string SeasonKeys { get; set; }
    /// <summary>A scheduled import's changed sources (comma-separated IDs): it imports the seasons they make up.</summary>
    public string ChangedSourceIDs { get; set; }

    public virtual ICollection<HistoricalSeasonReview> Reviews { get; set; } = new List<HistoricalSeasonReview>();
}

public enum HistoricalImportJobKind
{
    Import = 1,
    Refresh = 2,
    Preview = 3,
}

public enum HistoricalImportJobTrigger
{
    Manual = 1,
    Scheduled = 2,
}

public enum HistoricalImportJobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
}
