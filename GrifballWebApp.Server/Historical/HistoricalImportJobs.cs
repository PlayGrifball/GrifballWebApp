using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GrifballWebApp.Server.Historical;

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
    private readonly GrifballContext _context;
    private readonly HistoricalImportSignal _signal;

    private readonly TimeProvider _time;

    public HistoricalImportJobs(GrifballContext context, HistoricalImportSignal signal, TimeProvider? time = null)
    {
        _context = context;
        _signal = signal;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Queues a job of the sources (a refresh, a preview, an import of snapshots): the worker runs it.</summary>
    public async Task<int> Enqueue(HistoricalImportJob job, CancellationToken ct = default)
    {
        job.Stage = "Queued";
        _context.HistoricalImportJobs.Add(job);
        await _context.SaveChangesAsync(ct);
        _signal.Notify();
        return job.HistoricalImportJobID;
    }

    /// <summary>Whether a job is queued or running: a scheduled run waits for it.</summary>
    public Task<bool> AnyActive(CancellationToken ct = default)
        => _context.HistoricalImportJobs.AnyAsync(j => j.Status == HistoricalImportJobStatus.Queued || j.Status == HistoricalImportJobStatus.Running, ct);

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
        var preview = await _context.HistoricalImportJobs.AsNoTracking().Where(j => j.HistoricalImportJobID == previewJobId)
            .Select(j => new { j.Kind, j.Status, j.SnapshotIDs }).FirstOrDefaultAsync(ct);
        if (preview is null)
            return (HistoricalJobDecision.NotFound, null);
        if (preview.Kind != HistoricalImportJobKind.Preview || preview.Status != HistoricalImportJobStatus.Succeeded)
            return (HistoricalJobDecision.Conflict, null);
        var jobId = await Enqueue(new HistoricalImportJob { Kind = HistoricalImportJobKind.Import, SnapshotIDs = preview.SnapshotIDs }, ct);
        return (HistoricalJobDecision.Done, jobId);
    }

    /// <summary>Approving a season the guardrail stopped imports it as its job would have (the same snapshots), without
    /// the guardrail; skipping leaves it as it is.</summary>
    public async Task<(HistoricalJobDecision Decision, int? JobID)> Decide(int reviewId, bool approve, CancellationToken ct = default)
    {
        var review = await _context.HistoricalSeasonReviews.Include(r => r.Job).FirstOrDefaultAsync(r => r.HistoricalSeasonReviewID == reviewId, ct);
        if (review is null)
            return (HistoricalJobDecision.NotFound, null);
        if (review.Status != HistoricalSeasonReviewStatus.NeedsReview)
            return (HistoricalJobDecision.Conflict, null);
        review.DecidedAt = _time.GetUtcNow().UtcDateTime;
        if (!approve)
        {
            review.Status = HistoricalSeasonReviewStatus.Skipped;
            await _context.SaveChangesAsync(ct);
            return (HistoricalJobDecision.Done, null);
        }
        review.Status = HistoricalSeasonReviewStatus.Approved;
        var job = new HistoricalImportJob
        {
            Kind = HistoricalImportJobKind.Import,
            SnapshotIDs = review.Job.SnapshotIDs,
            SeasonKeys = review.SeasonKey,
        };
        var jobId = await Enqueue(job, ct);
        review.ApprovalJobID = jobId;
        await _context.SaveChangesAsync(ct);
        return (HistoricalJobDecision.Done, jobId);
    }

    /// <summary>The scheduler's row, made when first needed.</summary>
    public async Task<HistoricalImportSettings> Settings(CancellationToken ct = default)
    {
        var settings = await _context.HistoricalImportSettings.FirstOrDefaultAsync(s => s.HistoricalImportSettingsID == GrifballWebApp.Database.Models.HistoricalImportSettings.SingletonID, ct);
        if (settings is not null)
            return settings;
        // Two pods making it at once is a key conflict for one of them: its request fails once, the row is there after
        settings = new HistoricalImportSettings { HistoricalImportSettingsID = GrifballWebApp.Database.Models.HistoricalImportSettings.SingletonID };
        _context.HistoricalImportSettings.Add(settings);
        await _context.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<int> Enqueue(HistoricalArchiveInput input, CancellationToken ct = default)
    {
        var job = new HistoricalImportJob
        {
            Payload = JsonSerializer.Serialize(input, HistoricalArchiveInput.JsonOptions),
            Stage = "Queued",
        };
        _context.HistoricalImportJobs.Add(job);
        await _context.SaveChangesAsync(ct);
        _signal.Notify();
        return job.HistoricalImportJobID;
    }

    public async Task<HistoricalImportJobDto?> Get(int jobId, CancellationToken ct = default)
    {
        var job = await _context.HistoricalImportJobs.AsNoTracking()
            .Where(j => j.HistoricalImportJobID == jobId)
            .Select(j => new { j.HistoricalImportJobID, j.Kind, j.Trigger, j.Guarded, j.Status, j.Stage, j.SeasonsDone, j.SeasonsTotal, j.Report, j.Error, j.CreatedAt, j.StartedAt, j.FinishedAt })
            .FirstOrDefaultAsync(ct);
        if (job is null)
            return null;
        var reviews = await _context.HistoricalSeasonReviews.AsNoTracking().Where(r => r.HistoricalImportJobID == jobId)
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

/// <summary>
/// Runs queued historical imports one at a time, outside any request: its cancellation is the host's shutdown. A job
/// is claimed by flipping it from Queued to Running in one statement, so two replicas never run the same one. A job
/// stopped by a shutdown stays Running until its heartbeat is stale, then is queued again and runs from the start
/// (the import is idempotent).
/// </summary>
public sealed class HistoricalImportWorker : BackgroundService
{
    private readonly XuidLookupSignal _lookups;
    private readonly IServiceScopeFactory _scopes;
    private readonly HistoricalImportSignal _signal;
    private readonly IOptions<HistoricalImportWorkerOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<HistoricalImportWorker> _logger;

    public HistoricalImportWorker(IServiceScopeFactory scopes, HistoricalImportSignal signal, IOptions<HistoricalImportWorkerOptions> options, TimeProvider time, ILogger<HistoricalImportWorker> logger, XuidLookupSignal lookups)
    {
        _lookups = lookups;
        _scopes = scopes;
        _signal = signal;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                while (await RunNextAsync(ct))
                {
                }
                await _signal.WaitAsync(_options.Value.PollInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The historical import worker failed; trying again in {Delay}", _options.Value.PollInterval);
                try { await Task.Delay(_options.Value.PollInterval, _time, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Runs the oldest queued job, after queueing again any left Running by a stopped process. False when none was queued.</summary>
    public async Task<bool> RunNextAsync(CancellationToken ct)
    {
        int jobId;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var now = _time.GetUtcNow().UtcDateTime;
            var stale = now - _options.Value.StaleAfter;
            // Retention: finished jobs older than KeepFinishedFor go
            var expired = now - _options.Value.KeepFinishedFor;
            await context.HistoricalImportJobs
                .Where(j => (j.Status == HistoricalImportJobStatus.Succeeded || j.Status == HistoricalImportJobStatus.Failed) && j.FinishedAt < expired)
                .ExecuteDeleteAsync(ct);
            await context.HistoricalImportJobs
                .Where(j => j.Status == HistoricalImportJobStatus.Running && (j.HeartbeatAt == null || j.HeartbeatAt < stale))
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, HistoricalImportJobStatus.Queued).SetProperty(j => j.Stage, "Queued again after a restart"), ct);

            var next = await context.HistoricalImportJobs.Where(j => j.Status == HistoricalImportJobStatus.Queued)
                .OrderBy(j => j.HistoricalImportJobID).Select(j => (int?)j.HistoricalImportJobID).FirstOrDefaultAsync(ct);
            if (next is null)
                return false;
            jobId = next.Value;
            var claimed = await context.HistoricalImportJobs
                .Where(j => j.HistoricalImportJobID == jobId && j.Status == HistoricalImportJobStatus.Queued)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, HistoricalImportJobStatus.Running)
                    .SetProperty(j => j.StartedAt, now).SetProperty(j => j.HeartbeatAt, now).SetProperty(j => j.Stage, "Starting"), ct);
            if (claimed == 0)
                return true; // another replica took it
        }

        await RunAsync(jobId, ct);
        return true;
    }

    private async Task RunAsync(int jobId, CancellationToken ct)
    {
        // The heartbeat, on a timer while the job runs: progress alone can be further apart than StaleAfter
        using var running = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = HeartbeatAsync(jobId, running.Token);
        try
        {
            await RunJobAsync(jobId, ct);
        }
        finally
        {
            await running.CancelAsync();
            await heartbeat;
        }
    }

    /// <summary>Beats the job's heartbeat every third of StaleAfter until <paramref name="ct"/> is cancelled (the job is done).</summary>
    private async Task HeartbeatAsync(int jobId, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromTicks(Math.Max(_options.Value.StaleAfter.Ticks / 3, TimeSpan.TicksPerMillisecond)), _time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
                    var now = _time.GetUtcNow().UtcDateTime;
                    await context.HistoricalImportJobs
                        .Where(j => j.HistoricalImportJobID == jobId && j.Status == HistoricalImportJobStatus.Running)
                        .ExecuteUpdateAsync(s => s.SetProperty(j => j.HeartbeatAt, now), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Tried again at the next tick: two missed in a row still leave a third before StaleAfter
                    _logger.LogWarning(ex, "Couldn't beat historical import job {JobID}'s heartbeat", jobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunJobAsync(int jobId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var job = await context.HistoricalImportJobs.AsNoTracking().Where(j => j.HistoricalImportJobID == jobId)
                .Select(j => new { j.Kind, j.Payload }).FirstAsync(ct);
            HistoricalImportReport report;
            if (job.Kind == HistoricalImportJobKind.Import && job.Payload is not null)
            {
                // An uploaded archive
                var input = JsonSerializer.Deserialize<HistoricalArchiveInput>(job.Payload, HistoricalArchiveInput.JsonOptions)
                    ?? throw new InvalidDataException("The job's archive is empty");
                var import = scope.ServiceProvider.GetRequiredService<IHistoricalImportService>();
                report = await import.Import(input, progress => Progress(jobId, progress, ct), ct);
            }
            else
            {
                // The sources: a refresh, a preview, an import of their snapshots
                var runner = scope.ServiceProvider.GetRequiredService<HistoricalImportRunner>();
                report = await runner.Run(jobId, progress => Progress(jobId, progress, ct), ct);
            }
            Cap(report, _options.Value.ReportLinesKept);
            await Finish(jobId, HistoricalImportJobStatus.Succeeded, JsonSerializer.Serialize(report, HistoricalArchiveInput.JsonOptions), null, ct);
            _logger.LogInformation("Historical import job {JobID} finished", jobId);
            // The import's new placeholders get their XUIDs looked up
            _lookups.Notify();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Historical import job {JobID} was stopped by a shutdown; it runs again once its heartbeat is stale", jobId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Historical import job {JobID} failed", jobId);
            await Finish(jobId, HistoricalImportJobStatus.Failed, null, ex.GetBaseException().Message, CancellationToken.None);
        }
    }

    /// <summary>Keeps each of the report's lists to <paramref name="lines"/> lines, the last saying how many more there were.</summary>
    public static void Cap(HistoricalImportReport report, int lines)
    {
        foreach (var list in new[] { report.Skipped, report.Warnings, report.MissingMatches, report.Errors })
        {
            if (list.Count <= lines)
                continue;
            var more = list.Count - lines;
            list.RemoveRange(lines, more);
            list.Add($"... and {more} more");
        }
    }

    private async Task Progress(int jobId, HistoricalImportProgress progress, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var now = _time.GetUtcNow().UtcDateTime;
        var stage = progress.Stage.Length > 200 ? progress.Stage[..200] : progress.Stage;
        await context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Stage, stage).SetProperty(j => j.SeasonsDone, progress.SeasonsDone)
                .SetProperty(j => j.SeasonsTotal, progress.SeasonsTotal).SetProperty(j => j.HeartbeatAt, now), ct);
    }

    private async Task Finish(int jobId, HistoricalImportJobStatus status, string? report, string? error, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var now = _time.GetUtcNow().UtcDateTime;
        // The archive is no longer needed once the job ends
        await context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status).SetProperty(j => j.Report, report).SetProperty(j => j.Error, error)
                .SetProperty(j => j.Payload, (string?)null).SetProperty(j => j.FinishedAt, now).SetProperty(j => j.HeartbeatAt, now)
                .SetProperty(j => j.Stage, status == HistoricalImportJobStatus.Succeeded ? "Done" : "Failed"), ct);
    }
}
