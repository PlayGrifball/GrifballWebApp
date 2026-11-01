using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Jobs;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Refreshes the historical sources on a schedule (daily by default): it queues a refresh job, which queues a guarded
/// import only when a source changed. Runs go through the job queue, so one runs at a time and never alongside a manual
/// one: a run that falls due while a job is queued or running tries again <see cref="RetryAfter"/> later, until none is.
/// Paused (its trigger, from the Historical Import page or the Jobs page), it queues nothing.
/// </summary>
public sealed class HistoricalRefreshJob : IJob
{
    public static readonly JobKey Key = new("historical-refresh", RecurringJobs.Group);
    public static readonly TriggerKey TriggerKey = new(Key.Name, Key.Group);
    /// <summary>The one-off trigger of a run that waits for a queued or running job.</summary>
    public static readonly TriggerKey RetryTriggerKey = new($"{Key.Name}-retry", Key.Group);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);
    public const string WaitingNote = "Waiting for a queued or running job to finish";
    public const string PausedReason = "Paused on the Historical Import page";

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Refreshes the historical sources; an import is queued when one changed",
        // The first run 10 minutes after the worker first starts with it: a deploy (or a restart loop) doesn't start a
        // download at once. A run missed while no worker was up runs when one is.
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(10))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromDays(1)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
        OnCreated = CarryOverPauseAsync,
    };

    private readonly HistoricalImportJobs _jobs;
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;
    private readonly ILogger<HistoricalRefreshJob> _logger;

    public HistoricalRefreshJob(HistoricalImportJobs jobs, GrifballContext context, TimeProvider time, ILogger<HistoricalRefreshJob> logger)
    {
        _jobs = jobs;
        _context = context;
        _time = time;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default)
    {
        if (await RunAsync(ct) is null)
        {
            var retry = TriggerBuilder.Create()
                .WithIdentity(RetryTriggerKey)
                .ForJob(Key)
                .WithDescription(WaitingNote)
                .StartAt(_time.GetUtcNow() + RetryAfter)
                .Build();
            await context.Scheduler.ScheduleTrigger(retry, TriggerConflict.Replace, ct);
        }
    }

    /// <summary>
    /// Queues the scheduled refresh unless a job is queued or running. Returns the job's ID, or null when it waits (and
    /// says so on the page).
    /// </summary>
    public async Task<int?> RunAsync(CancellationToken ct)
    {
        var settings = await _jobs.Settings(ct);
        if (await _jobs.AnyActive(ct))
        {
            if (settings.LastScheduledRunNote is null)
            {
                settings.LastScheduledRunNote = WaitingNote;
                await _context.SaveChangesAsync(ct);
            }
            return null;
        }
        var now = _time.GetUtcNow().UtcDateTime;
        var jobId = await _jobs.Enqueue(new HistoricalImportJob { Kind = HistoricalImportJobKind.Refresh, Trigger = HistoricalImportJobTrigger.Scheduled }, ct);
        settings.LastScheduledRunAt = now;
        settings.LastScheduledJobID = jobId;
        settings.LastScheduledRunNote = null;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Scheduled historical refresh queued as job {JobID}", jobId);
        return jobId;
    }

    /// <summary>A schedule paused before it moved to Quartz (<see cref="HistoricalImportSettings.SchedulePaused"/>) starts paused.</summary>
    public static async Task CarryOverPauseAsync(IScheduler scheduler, IServiceProvider services, CancellationToken ct)
    {
        var context = services.GetRequiredService<GrifballContext>();
        if (await context.HistoricalImportSettings.AnyAsync(s => s.SchedulePaused, ct))
            await scheduler.PauseTriggerWith(TriggerKey, new PauseDetails { Reason = PausedReason }, ct);
    }
}

/// <summary>The scheduled refresh as the Historical Import page shows it: its trigger, and the last run it queued.</summary>
public class HistoricalImportSchedule
{
    private readonly ISchedulerFactory _schedulers;
    private readonly HistoricalImportJobs _jobs;

    public HistoricalImportSchedule(ISchedulerFactory schedulers, HistoricalImportJobs jobs)
    {
        _schedulers = schedulers;
        _jobs = jobs;
    }

    public async Task<HistoricalImportScheduleDto> Get(CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        var trigger = await scheduler.GetTrigger(HistoricalRefreshJob.TriggerKey, ct);
        var paused = trigger is not null && await scheduler.GetTriggerState(trigger.Key, ct) == TriggerState.Paused;
        var settings = await _jobs.Settings(ct);
        return new HistoricalImportScheduleDto
        {
            Paused = paused,
            // No trigger yet (no worker has started since the move to Quartz): the default it will get
            Interval = (trigger as ISimpleTrigger)?.RepeatInterval ?? TimeSpan.FromDays(1),
            LastRunAt = settings.LastScheduledRunAt,
            NextRunAt = paused ? null : trigger?.NextFireTimeUtc?.UtcDateTime,
            LastJobID = settings.LastScheduledJobID,
            LastRunNote = settings.LastScheduledRunNote,
        };
    }

    /// <summary>
    /// Pauses or resumes the refresh's trigger; a pause also drops a run waiting to try again. False when it has no
    /// trigger yet (no worker has made it).
    /// </summary>
    public async Task<bool> SetPaused(bool paused, string? by, CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        if (!paused)
            return await scheduler.ResumeTrigger(HistoricalRefreshJob.TriggerKey, ct);
        await scheduler.UnscheduleJob(HistoricalRefreshJob.RetryTriggerKey, ct);
        return await scheduler.PauseTriggerWith(HistoricalRefreshJob.TriggerKey, new PauseDetails { Reason = HistoricalRefreshJob.PausedReason, RequestedBy = by }, ct);
    }
}
