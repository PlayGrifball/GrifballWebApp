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
}

public enum HistoricalImportJobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
}
