using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A historical import run in the background (HistoricalImportWorker). The uploaded archive is kept in
/// <see cref="Payload"/> until the job ends, so a job survives a restart and any replica can run it.
/// Not temporal: its progress changes often and the payload is large.
/// <para>
/// Queued, then Running once a worker claims it (<see cref="ClaimAsync"/>), then Succeeded or Failed
/// (<see cref="FinishAsync"/>); a Running job whose heartbeat is stale is Queued again (<see cref="RequeueStaleAsync"/>).
/// The worker's moves are single statements, so two replicas never claim the same job: each one refuses a job that isn't
/// in the state it moves from. Its season reviews (<see cref="HistoricalSeasonReview"/>) are its own.
/// </para>
/// </summary>
[AggregateRoot]
public class HistoricalImportJob : AuditableEntity
{
    public const int MaxStageLength = 200;
    public const string QueuedStage = "Queued";
    public const string RequeuedStage = "Queued again after a restart";
    public const string StartingStage = "Starting";
    public const string SucceededStage = "Done";
    public const string FailedStage = "Failed";

    /// <summary>For EF. A job is queued by one of its factories: <see cref="Upload"/>, <see cref="Refresh"/>,
    /// <see cref="Preview"/>, <see cref="ImportSnapshots"/> or <see cref="ScheduledImport"/>.</summary>
    private HistoricalImportJob() { }

    private HistoricalImportJob(HistoricalImportJobKind kind, HistoricalImportJobTrigger trigger = HistoricalImportJobTrigger.Manual,
        bool guarded = false, string? snapshotIDs = null, string? seasonKeys = null, string? changedSourceIDs = null, string? payload = null)
    {
        Kind = kind;
        Trigger = trigger;
        Guarded = guarded;
        SnapshotIDs = snapshotIDs;
        SeasonKeys = seasonKeys;
        ChangedSourceIDs = changedSourceIDs;
        Payload = payload;
        Status = HistoricalImportJobStatus.Queued;
        Stage = QueuedStage;
    }

    /// <summary>An import of an uploaded archive (HistoricalArchiveInput as JSON), by hand.</summary>
    public static HistoricalImportJob Upload(string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        return new(HistoricalImportJobKind.Import, payload: payload);
    }

    /// <summary>A download of the sources; a scheduled one queues a guarded import when a source the import reads changed.</summary>
    public static HistoricalImportJob Refresh(HistoricalImportJobTrigger trigger = HistoricalImportJobTrigger.Manual) => new(HistoricalImportJobKind.Refresh, trigger);

    /// <summary>A dry run of importing the sources' snapshots: <paramref name="snapshotIDs"/>, or each one's latest.</summary>
    public static HistoricalImportJob Preview(string? snapshotIDs = null) => new(HistoricalImportJobKind.Preview, snapshotIDs: snapshotIDs);

    /// <summary>An import of the sources' snapshots by hand: <paramref name="snapshotIDs"/> (what a preview read), or each
    /// one's latest; of <paramref name="seasonKeys"/> only (one per line), or every season.</summary>
    public static HistoricalImportJob ImportSnapshots(string? snapshotIDs = null, string? seasonKeys = null)
        => new(HistoricalImportJobKind.Import, snapshotIDs: snapshotIDs, seasonKeys: seasonKeys);

    /// <summary>A scheduled refresh's import of the seasons its changed sources make up: guarded, so a season that would
    /// remove too much waits for a sysadmin.</summary>
    public static HistoricalImportJob ScheduledImport(string snapshotIDs, string changedSourceIDs)
        => new(HistoricalImportJobKind.Import, HistoricalImportJobTrigger.Scheduled, guarded: true, snapshotIDs, changedSourceIDs: changedSourceIDs);

    /// <summary>Any job, for tests.</summary>
    internal static HistoricalImportJob Seed(HistoricalImportJobKind kind = HistoricalImportJobKind.Import, bool guarded = false, string? snapshotIDs = null, string? payload = null)
        => new(kind, guarded: guarded, snapshotIDs: snapshotIDs, payload: payload);

    public int HistoricalImportJobID { get; internal set; }
    public HistoricalImportJobStatus Status { get; private set; } = HistoricalImportJobStatus.Queued;
    /// <summary>The archive (HistoricalArchiveInput as JSON); cleared when the job ends (CK_Event_HistoricalImportJobs_PayloadUntilEnded).</summary>
    public string? Payload { get; private set; }
    /// <summary>What it is doing, e.g. "Pulling Waypoint matches 120/902".</summary>
    public string? Stage { get; private set; }
    public int SeasonsDone { get; private set; }
    public int SeasonsTotal { get; private set; }
    /// <summary>The HistoricalImportReport, as JSON, once it ends.</summary>
    public string? Report { get; private set; }
    public string? Error { get; private set; }
    public DateTime? StartedAt { get; private set; }
    /// <summary>Updated while it runs: a Running job whose heartbeat is old was left by a stopped process and is queued again.</summary>
    public DateTime? HeartbeatAt { get; private set; }
    /// <summary>Set when it ends, and only then (CK_Event_HistoricalImportJobs_FinishedOnceEnded).</summary>
    public DateTime? FinishedAt { get; private set; }
    /// <summary>What it does: an import (of <see cref="Payload"/>, or of the sources' snapshots), a refresh of the sources,
    /// or a dry run of an import.</summary>
    public HistoricalImportJobKind Kind { get; private set; } = HistoricalImportJobKind.Import;
    public HistoricalImportJobTrigger Trigger { get; private set; } = HistoricalImportJobTrigger.Manual;
    /// <summary>A season whose import would remove too much is stopped for review instead (the scheduled imports).</summary>
    public bool Guarded { get; private set; }
    /// <summary>The snapshots an import or dry run reads (comma-separated IDs), so applying a preview imports what was
    /// previewed; null reads each source's latest.</summary>
    public string? SnapshotIDs { get; private set; }
    /// <summary>The seasons (HistoricalSourceIDs, one per line) it imports; null is all of them.</summary>
    public string? SeasonKeys { get; private set; }
    /// <summary>A scheduled import's changed sources (comma-separated IDs): it imports the seasons they make up.</summary>
    public string? ChangedSourceIDs { get; private set; }

    /// <summary>The seasons it would remove rows from, for a sysadmin: added by <see cref="AddReview"/>, decided by
    /// <see cref="DecideReview"/>; removed with the job.</summary>
    public IReadOnlyCollection<HistoricalSeasonReview> Reviews => _reviews;
    private readonly List<HistoricalSeasonReview> _reviews = [];

    /// <summary>Queued or running: a scheduled run waits for it. For queries.</summary>
    public static readonly Expression<Func<HistoricalImportJob, bool>> Active
        = j => j.Status == HistoricalImportJobStatus.Queued || j.Status == HistoricalImportJobStatus.Running;

    /// <summary>Succeeded or failed. For queries.</summary>
    public static readonly Expression<Func<HistoricalImportJob, bool>> Ended
        = j => j.Status == HistoricalImportJobStatus.Succeeded || j.Status == HistoricalImportJobStatus.Failed;

    /// <summary>A finished preview: its snapshots can be imported (<see cref="ApplyPreview"/>).</summary>
    public bool IsAppliablePreview => Kind == HistoricalImportJobKind.Preview && Status == HistoricalImportJobStatus.Succeeded;

    /// <summary>The import of what this finished preview read (its snapshots, every season); null when it isn't one.</summary>
    public HistoricalImportJob? ApplyPreview() => IsAppliablePreview ? ImportSnapshots(SnapshotIDs) : null;

    /// <summary>
    /// A season the import would remove rows from: stopped by the guardrail (<paramref name="stopped"/>, a guarded import's),
    /// waiting for a sysadmin; or listed by a preview, which is shown before it is applied.
    /// </summary>
    public HistoricalSeasonReview AddReview(string seasonKey, string? seasonName, bool stopped, HistoricalSeasonRemovals removals)
    {
        if (stopped && !Guarded)
            throw new InvalidOperationException($"Job {HistoricalImportJobID} isn't guarded: no season of it is stopped");
        if (!stopped && Kind != HistoricalImportJobKind.Preview)
            throw new InvalidOperationException($"Job {HistoricalImportJobID} is an {Kind}, not a preview: only a stopped season is reviewed");
        var review = new HistoricalSeasonReview(this, seasonKey, seasonName,
            stopped ? HistoricalSeasonReviewStatus.NeedsReview : HistoricalSeasonReviewStatus.Preview, removals);
        _reviews.Add(review);
        return review;
    }

    /// <summary>
    /// A sysadmin's decision on a season the guardrail stopped, at <paramref name="decidedAt"/>: approving it returns the
    /// import that imports it as this job would have (the same snapshots, without the guardrail), to be queued and then
    /// noted (<see cref="ApprovalQueued"/>); skipping leaves it as it is. Decided once.
    /// </summary>
    public (HistoricalSeasonReviewDecision Decision, HistoricalImportJob? Approval) DecideReview(int reviewID, bool approve, DateTime decidedAt)
    {
        var review = _reviews.FirstOrDefault(r => r.HistoricalSeasonReviewID == reviewID);
        if (review is null)
            return (HistoricalSeasonReviewDecision.NotFound, null);
        if (!review.Decide(approve, decidedAt))
            return (HistoricalSeasonReviewDecision.NotAwaitingReview, null);
        return (HistoricalSeasonReviewDecision.Decided, approve ? ImportSnapshots(SnapshotIDs, review.SeasonKey) : null);
    }

    /// <summary>The import approving a review queued, as <paramref name="approvalJobID"/>.</summary>
    public void ApprovalQueued(int reviewID, int approvalJobID)
        => (_reviews.FirstOrDefault(r => r.HistoricalSeasonReviewID == reviewID)
            ?? throw new InvalidOperationException($"Job {HistoricalImportJobID} has no review {reviewID}")).ApprovalQueued(approvalJobID);

    // The worker's moves: single statements, each refusing a job that isn't in the state it moves from (another replica's,
    // or one already moved on)

    /// <summary>Running to Queued: the jobs whose heartbeat is older than <paramref name="staleBefore"/> (or never beat),
    /// left by a stopped process. Any worker may claim them again.</summary>
    public static Task<int> RequeueStaleAsync(IQueryable<HistoricalImportJob> jobs, DateTime staleBefore, CancellationToken ct = default)
        => jobs.Where(j => j.Status == HistoricalImportJobStatus.Running && (j.HeartbeatAt == null || j.HeartbeatAt < staleBefore))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, HistoricalImportJobStatus.Queued).SetProperty(j => j.Stage, RequeuedStage), ct);

    /// <summary>
    /// Queued to Running, started at <paramref name="now"/> cut to whole microseconds (the finest time every provider keeps:
    /// Postgres stores microseconds), so the start stored is the one returned. Returns the claim, its start: the run passes
    /// it to each of its moves (<see cref="BeatAsync"/>, <see cref="ReportProgressAsync"/>, <see cref="RecordSnapshotsReadAsync"/>,
    /// <see cref="FinishAsync"/>), and they refuse any run but this one. Null when it isn't queued (another replica took it),
    /// or when its last start isn't before this one: each claim starts later than the one before, so two never share a
    /// start (after a clock went back, the job waits queued for a later pass).
    /// </summary>
    public static async Task<DateTime?> ClaimAsync(IQueryable<HistoricalImportJob> jobs, int jobID, DateTime now, CancellationToken ct = default)
    {
        var claim = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, now.Kind);
        var claimed = await jobs.Where(j => j.HistoricalImportJobID == jobID && j.Status == HistoricalImportJobStatus.Queued
                && (j.StartedAt == null || j.StartedAt < claim))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, HistoricalImportJobStatus.Running)
                .SetProperty(j => j.StartedAt, claim).SetProperty(j => j.HeartbeatAt, claim).SetProperty(j => j.Stage, StartingStage), ct) > 0;
        return claimed ? claim : null;
    }

    /// <summary>The job, while the run that made <paramref name="claim"/> runs it: Running, and not claimed again since.</summary>
    private static IQueryable<HistoricalImportJob> RunBy(IQueryable<HistoricalImportJob> jobs, int jobID, DateTime claim)
        => jobs.Where(j => j.HistoricalImportJobID == jobID && j.Status == HistoricalImportJobStatus.Running && j.StartedAt == claim);

    /// <summary>The run that made <paramref name="claim"/> is alive at <paramref name="now"/>. False when the job isn't
    /// running under that claim: it ended, or was queued again (and maybe claimed by another run).</summary>
    public static async Task<bool> BeatAsync(IQueryable<HistoricalImportJob> jobs, int jobID, DateTime claim, DateTime now, CancellationToken ct = default)
        => await RunBy(jobs, jobID, claim).ExecuteUpdateAsync(s => s.SetProperty(j => j.HeartbeatAt, now), ct) > 0;

    /// <summary>A running job's progress (its stage cut to <see cref="MaxStageLength"/>), which beats its heartbeat too. False
    /// when it isn't running under <paramref name="claim"/>.</summary>
    public static async Task<bool> ReportProgressAsync(IQueryable<HistoricalImportJob> jobs, int jobID, DateTime claim, string stage, int seasonsDone, int seasonsTotal, DateTime now, CancellationToken ct = default)
    {
        stage = stage.Length > MaxStageLength ? stage[..MaxStageLength] : stage;
        return await RunBy(jobs, jobID, claim)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Stage, stage).SetProperty(j => j.SeasonsDone, seasonsDone)
                .SetProperty(j => j.SeasonsTotal, seasonsTotal).SetProperty(j => j.HeartbeatAt, now), ct) > 0;
    }

    /// <summary>The snapshots a running import or preview read, so applying it (or approving a season) imports what was
    /// seen. False when it isn't running under <paramref name="claim"/>.</summary>
    public static async Task<bool> RecordSnapshotsReadAsync(IQueryable<HistoricalImportJob> jobs, int jobID, DateTime claim, IEnumerable<int> snapshotIDs, CancellationToken ct = default)
    {
        var ids = string.Join(',', snapshotIDs);
        return await RunBy(jobs, jobID, claim).ExecuteUpdateAsync(s => s.SetProperty(j => j.SnapshotIDs, ids), ct) > 0;
    }

    /// <summary>Running to Succeeded (with its <paramref name="report"/>) or Failed (with its <paramref name="error"/>) at
    /// <paramref name="now"/>; its archive is let go of. False when it isn't running under <paramref name="claim"/>: the
    /// run's result is dropped, and the job is left as it is (another run's, or ended).</summary>
    public static async Task<bool> FinishAsync(IQueryable<HistoricalImportJob> jobs, int jobID, DateTime claim, bool succeeded, string? report, string? error, DateTime now, CancellationToken ct = default)
    {
        var status = succeeded ? HistoricalImportJobStatus.Succeeded : HistoricalImportJobStatus.Failed;
        var stage = succeeded ? SucceededStage : FailedStage;
        return await RunBy(jobs, jobID, claim)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status).SetProperty(j => j.Report, report).SetProperty(j => j.Error, error)
                .SetProperty(j => j.Payload, (string?)null).SetProperty(j => j.FinishedAt, now).SetProperty(j => j.HeartbeatAt, now)
                .SetProperty(j => j.Stage, stage), ct) > 0;
    }

    /// <summary>Deletes the jobs that ended before <paramref name="endedBefore"/> (their retention is over), with their reviews.</summary>
    public static Task<int> DeleteEndedAsync(IQueryable<HistoricalImportJob> jobs, DateTime endedBefore, CancellationToken ct = default)
        => jobs.Where(Ended).Where(j => j.FinishedAt < endedBefore).ExecuteDeleteAsync(ct);
}

/// <summary>What a season's import would remove: its rows before, and how many would go.</summary>
public readonly record struct HistoricalSeasonRemovals(int StatsBefore, int StatsRemoved, int TeamsBefore, int TeamsRemoved, int TitlesBefore, int TitlesRemoved);

/// <summary>What <see cref="HistoricalImportJob.DecideReview"/> did.</summary>
public enum HistoricalSeasonReviewDecision
{
    Decided = 1,
    NotFound = 2,
    /// <summary>Not stopped by the guardrail, or decided already.</summary>
    NotAwaitingReview = 3,
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
