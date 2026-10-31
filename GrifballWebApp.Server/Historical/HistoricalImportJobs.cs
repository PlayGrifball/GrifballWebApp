using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GrifballWebApp.Server.Historical;

public class HistoricalImportJobDto
{
    public int JobID { get; set; }
    public required string Status { get; set; }
    public string? Stage { get; set; }
    public int SeasonsDone { get; set; }
    public int SeasonsTotal { get; set; }
    public HistoricalImportReport? Report { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class HistoricalImportWorkerOptions
{
    /// <summary>How often the worker looks for queued jobs when it isn't woken by a new one.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>A Running job whose heartbeat is older than this was left by a stopped process: it is queued again.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);
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

    public HistoricalImportJobs(GrifballContext context, HistoricalImportSignal signal)
    {
        _context = context;
        _signal = signal;
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
            .Select(j => new { j.HistoricalImportJobID, j.Status, j.Stage, j.SeasonsDone, j.SeasonsTotal, j.Report, j.Error, j.CreatedAt, j.StartedAt, j.FinishedAt })
            .FirstOrDefaultAsync(ct);
        if (job is null)
            return null;
        return new HistoricalImportJobDto
        {
            JobID = job.HistoricalImportJobID,
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
    private readonly IServiceScopeFactory _scopes;
    private readonly HistoricalImportSignal _signal;
    private readonly IOptions<HistoricalImportWorkerOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<HistoricalImportWorker> _logger;

    public HistoricalImportWorker(IServiceScopeFactory scopes, HistoricalImportSignal signal, IOptions<HistoricalImportWorkerOptions> options, TimeProvider time, ILogger<HistoricalImportWorker> logger)
    {
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
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var payload = await context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobId).Select(j => j.Payload).FirstAsync(ct);
            var input = JsonSerializer.Deserialize<HistoricalArchiveInput>(payload!, HistoricalArchiveInput.JsonOptions)
                ?? throw new InvalidDataException("The job's archive is empty");
            var import = scope.ServiceProvider.GetRequiredService<IHistoricalImportService>();
            var report = await import.Import(input, progress => Progress(jobId, progress, ct), ct);
            await Finish(jobId, HistoricalImportJobStatus.Succeeded, JsonSerializer.Serialize(report, HistoricalArchiveInput.JsonOptions), null, ct);
            _logger.LogInformation("Historical import job {JobID} finished", jobId);
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
