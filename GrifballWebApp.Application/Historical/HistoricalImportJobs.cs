using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GrifballWebApp.Application.Historical;

public class HistoricalImportJobDto
{
    public int JobID { get; set; }
    /// <summary>Import, Refresh or Preview.</summary>
    public string Kind { get; set; } = nameof(HistoricalImportJobKind.Import);
    /// <summary>Manual or Scheduled.</summary>
    public string Trigger { get; set; } = nameof(HistoricalImportJobTrigger.Manual);
    public bool Guarded { get; set; }
    public required string Status { get; set; }
    public string? Stage { get; set; }
    public int SeasonsDone { get; set; }
    public int SeasonsTotal { get; set; }
    public HistoricalImportReport? Report { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    /// <summary>Its seasons' removals that wait for (or got) a decision, and a preview's.</summary>
    public List<HistoricalSeasonReviewDto> Reviews { get; set; } = [];
}

public class HistoricalSeasonReviewDto
{
    public int ReviewID { get; set; }
    public required string SeasonKey { get; set; }
    public string? SeasonName { get; set; }
    /// <summary>The season, once the import made it; null before.</summary>
    public int? SeasonID { get; set; }
    /// <summary>Preview, NeedsReview, Approved or Skipped.</summary>
    public required string Status { get; set; }
    public int StatsBefore { get; set; }
    public int StatsRemoved { get; set; }
    public int TeamsBefore { get; set; }
    public int TeamsRemoved { get; set; }
    public int TitlesBefore { get; set; }
    public int TitlesRemoved { get; set; }
    public int? ApprovalJobID { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>A job in the list: what it is and how it went, without its report.</summary>
public class HistoricalImportJobSummaryDto
{
    public int JobID { get; set; }
    public required string Kind { get; set; }
    public required string Trigger { get; set; }
    public required string Status { get; set; }
    public string? Stage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int NeedsReview { get; set; }
}

public class HistoricalImportScheduleDto
{
    public bool Paused { get; set; }
    public TimeSpan Interval { get; set; }
    public DateTime? LastRunAt { get; set; }
    /// <summary>When the scheduled refresh next fires; null while paused.</summary>
    public DateTime? NextRunAt { get; set; }
    public int? LastJobID { get; set; }
    /// <summary>Why the last due run queued nothing (a run in progress).</summary>
    public string? LastRunNote { get; set; }
}

public enum HistoricalJobDecision
{
    Done,
    NotFound,
    /// <summary>Not in a state that allows it (already decided, not a finished preview).</summary>
    Conflict,
}

public class HistoricalImportWorkerOptions
{
    /// <summary>How often the worker looks for queued jobs when it isn't woken by a new one.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>
    /// A Running job whose heartbeat is older than this was left by a stopped process: it is queued again, and any
    /// worker may claim it. The worker running a job beats every third of this, on a timer, whatever the job's progress
    /// (a long stage, Halo calls waiting their shared turns), so a live job is never taken over by another worker.
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(30);
    /// <summary>Finished jobs (their reports) are kept this long, then deleted.</summary>
    public TimeSpan KeepFinishedFor { get; set; } = TimeSpan.FromDays(30);
    /// <summary>A report's lists (skipped, warnings, missing matches, errors) keep at most this many lines each, then
    /// say how many more there were: a report stays a few hundred KB at most.</summary>
    public int ReportLinesKept { get; set; } = 500;
}

/// <summary>Wakes the worker when a job is queued.</summary>
public sealed class HistoricalImportSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try { _semaphore.Release(); } catch (SemaphoreFullException) { }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
}

/// <summary>
/// Queues imports and reports on them. The archive is stored with the job, in the database, rather than in memory or a
/// temporary file: a job survives a restart (it is picked up again, and a re-run is idempotent), and whichever replica
/// runs the worker can read it.
/// </summary>
public class HistoricalImportJobs
{
    private readonly IApplicationDbContext _context;
    private readonly HistoricalImportSignal _signal;

    private readonly TimeProvider _time;

    public HistoricalImportJobs(IApplicationDbContext context, HistoricalImportSignal signal, TimeProvider? time = null)
    {
        _context = context;
        _signal = signal;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Queues a job of the sources (a refresh, a preview, an import of snapshots): the worker runs it.</summary>
    public async Task<int> Enqueue(HistoricalImportJob job, CancellationToken ct = default)
    {
        _context.HistoricalImportJobs.Add(job);
        await _context.SaveChangesAsync(ct);
        _signal.Notify();
        return job.HistoricalImportJobID;
    }

    /// <summary>Whether a job is queued or running: a scheduled run waits for it.</summary>
    public Task<bool> AnyActive(CancellationToken ct = default)
        => _context.HistoricalImportJobs.AnyAsync(HistoricalImportJob.Active, ct);

    public async Task<List<HistoricalImportJobSummaryDto>> List(int take, CancellationToken ct = default)
    {
        var jobs = await _context.HistoricalImportJobs.AsNoTracking().OrderByDescending(j => j.HistoricalImportJobID).Take(take)
            .Select(j => new { j.HistoricalImportJobID, j.Kind, j.Trigger, j.Status, j.Stage, j.CreatedAt, j.FinishedAt,
                NeedsReview = j.Reviews.Count(r => r.Status == HistoricalSeasonReviewStatus.NeedsReview) })
            .ToListAsync(ct);
        return jobs.Select(j => new HistoricalImportJobSummaryDto
        {
            JobID = j.HistoricalImportJobID,
            Kind = j.Kind.ToString(),
            Trigger = j.Trigger.ToString(),
            Status = j.Status.ToString(),
            Stage = j.Stage,
            CreatedAt = j.CreatedAt,
            FinishedAt = j.FinishedAt,
            NeedsReview = j.NeedsReview,
        }).ToList();
    }

    /// <summary>Queues the import of what a finished preview read (its snapshots, every season): the summary was seen.</summary>
    public async Task<(HistoricalJobDecision Decision, int? JobID)> ApplyPreview(int previewJobId, CancellationToken ct = default)
    {
        var preview = await _context.HistoricalImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.HistoricalImportJobID == previewJobId, ct);
        if (preview is null)
            return (HistoricalJobDecision.NotFound, null);
        if (preview.ApplyPreview() is not { } import)
            return (HistoricalJobDecision.Conflict, null);
        var jobId = await Enqueue(import, ct);
        return (HistoricalJobDecision.Done, jobId);
    }

    /// <summary>Approving a season the guardrail stopped imports it as its job would have (the same snapshots), without
    /// the guardrail; skipping leaves it as it is.</summary>
    public async Task<(HistoricalJobDecision Decision, int? JobID)> Decide(int reviewId, bool approve, CancellationToken ct = default)
    {
        var job = await _context.HistoricalImportJobs.Include(j => j.Reviews)
            .FirstOrDefaultAsync(j => j.Reviews.Any(r => r.HistoricalSeasonReviewID == reviewId), ct);
        if (job is null)
            return (HistoricalJobDecision.NotFound, null);
        var (decision, approval) = job.DecideReview(reviewId, approve, _time.GetUtcNow().UtcDateTime);
        if (decision != HistoricalSeasonReviewDecision.Decided)
            return (decision == HistoricalSeasonReviewDecision.NotFound ? HistoricalJobDecision.NotFound : HistoricalJobDecision.Conflict, null);
        if (approval is null)
        {
            await _context.SaveChangesAsync(ct);
            return (HistoricalJobDecision.Done, null);
        }
        // Saves the decision with the import it queues
        var jobId = await Enqueue(approval, ct);
        job.ApprovalQueued(reviewId, jobId);
        await _context.SaveChangesAsync(ct);
        return (HistoricalJobDecision.Done, jobId);
    }

    /// <summary>The scheduler's row, made when first needed.</summary>
    public async Task<HistoricalImportSettings> Settings(CancellationToken ct = default)
    {
        var settings = await _context.HistoricalImportSettings.FirstOrDefaultAsync(s => s.HistoricalImportSettingsID == GrifballWebApp.Domain.Archive.HistoricalImportSettings.SingletonID, ct);
        if (settings is not null)
            return settings;
        // Two pods making it at once is a key conflict for one of them: its request fails once, the row is there after
        settings = GrifballWebApp.Domain.Archive.HistoricalImportSettings.Create();
        _context.HistoricalImportSettings.Add(settings);
        await _context.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<int> Enqueue(HistoricalArchiveInput input, CancellationToken ct = default)
        => await Enqueue(HistoricalImportJob.Upload(JsonSerializer.Serialize(input, HistoricalArchiveInput.JsonOptions)), ct);

    public async Task<HistoricalImportJobDto?> Get(int jobId, CancellationToken ct = default)
    {
        var job = await _context.HistoricalImportJobs.AsNoTracking()
            .Where(j => j.HistoricalImportJobID == jobId)
            .Select(j => new { j.HistoricalImportJobID, j.Kind, j.Trigger, j.Guarded, j.Status, j.Stage, j.SeasonsDone, j.SeasonsTotal, j.Report, j.Error, j.CreatedAt, j.StartedAt, j.FinishedAt })
            .FirstOrDefaultAsync(ct);
        if (job is null)
            return null;
        var reviews = await _context.HistoricalImportJobs.AsNoTracking().Where(j => j.HistoricalImportJobID == jobId).SelectMany(j => j.Reviews)
            .OrderBy(r => r.HistoricalSeasonReviewID)
            .Select(r => new HistoricalSeasonReviewDto
            {
                ReviewID = r.HistoricalSeasonReviewID,
                SeasonKey = r.SeasonKey,
                SeasonName = r.SeasonName,
                // The season the review is of, once it exists, for the link to its page
                SeasonID = _context.Seasons.Where(s => s.HistoricalSourceID == r.SeasonKey).Select(s => (int?)s.SeasonID).FirstOrDefault(),
                Status = r.Status.ToString(),
                StatsBefore = r.StatsBefore,
                StatsRemoved = r.StatsRemoved,
                TeamsBefore = r.TeamsBefore,
                TeamsRemoved = r.TeamsRemoved,
                TitlesBefore = r.TitlesBefore,
                TitlesRemoved = r.TitlesRemoved,
                ApprovalJobID = r.ApprovalJobID,
                DecidedAt = r.DecidedAt,
            })
            .ToListAsync(ct);
        return new HistoricalImportJobDto
        {
            JobID = job.HistoricalImportJobID,
            Kind = job.Kind.ToString(),
            Trigger = job.Trigger.ToString(),
            Guarded = job.Guarded,
            Reviews = reviews,
            Status = job.Status.ToString(),
            Stage = job.Stage,
            SeasonsDone = job.SeasonsDone,
            SeasonsTotal = job.SeasonsTotal,
            Report = job.Report is null ? null : JsonSerializer.Deserialize<HistoricalImportReport>(job.Report, HistoricalArchiveInput.JsonOptions),
            Error = job.Error,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
        };
    }
}
