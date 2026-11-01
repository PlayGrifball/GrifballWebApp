using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Server.Jobs;

public class JobsOverviewDto
{
    public required List<JobDto> Jobs { get; init; }
    /// <summary>The schedulers that have checked in to the store (clustered: SQL Server and Postgres); empty on SQLite.</summary>
    public required List<JobNodeDto> Nodes { get; init; }
}

public class JobDto
{
    public required string Group { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>One of the app's recurring jobs (<see cref="RecurringJob"/>): its schedule can be changed and reset.</summary>
    public bool Recurring { get; init; }
    /// <summary>Off (a Discord job while Discord is off): no trigger, and it can't be run.</summary>
    public bool Enabled { get; init; }
    /// <summary>Scheduled, Paused, Off (no trigger), Error (its trigger failed to fire) or Blocked (running, the next waits).</summary>
    public required string State { get; init; }
    /// <summary>How often it runs, in seconds (a recurring job's interval).</summary>
    public long? IntervalSeconds { get; init; }
    public long? DefaultIntervalSeconds { get; init; }
    public DateTime? NextRunAt { get; init; }
    public DateTime? PreviousRunAt { get; init; }
    public string? PausedReason { get; init; }
    public string? PausedBy { get; init; }
    public DateTime? PausedAt { get; init; }
    public required List<JobRunningDto> Running { get; init; }
    /// <summary>Its runs so far (within the history kept): the last one's result, and counts.</summary>
    public JobStatusDto? Status { get; init; }
}

public class JobRunningDto
{
    public required string FireInstanceID { get; init; }
    public DateTime StartedAt { get; init; }
    public required string Node { get; init; }
}

public class JobStatusDto
{
    public required string LastResult { get; init; }
    public DateTime LastRunAt { get; init; }
    public double LastDurationMs { get; init; }
    public DateTime? LastSucceededAt { get; init; }
    public DateTime? LastFailedAt { get; init; }
    public string? LastFailureMessage { get; init; }
    public int ConsecutiveFailures { get; init; }
    public long RunCount { get; init; }
    public long FailureCount { get; init; }
}

public class JobRunDto
{
    public DateTime FiredAt { get; init; }
    public double DurationMs { get; init; }
    public required string Result { get; init; }
    public string? Error { get; init; }
    public required string Node { get; init; }
    /// <summary>Run from the Jobs page rather than by its schedule.</summary>
    public bool Manual { get; init; }
    public required string Trigger { get; init; }
}

public class JobNodeDto
{
    public required string InstanceID { get; init; }
    public DateTime? LastCheckInAt { get; init; }
    public required string State { get; init; }
}

public enum JobChange
{
    Done,
    NotFound,
    /// <summary>Not now: off, no trigger yet (no worker has started), or not a recurring job.</summary>
    Conflict,
}

/// <summary>
/// The Jobs page: every job in Quartz's store, its trigger and runs (the database's execution history, shared by every
/// pod), and what a sysadmin can do to it. Works from any pod: an API pod's scheduler is in standby and only reads and
/// writes the store, and the worker fires what it stores.
/// </summary>
public class JobsService
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(365);
    public const string PausedReason = "Paused on the Jobs page";
    /// <summary>How far ahead a paused job's rescheduled trigger starts at the earliest (<see cref="Replace"/>).</summary>
    public static readonly TimeSpan PausedStartGrace = TimeSpan.FromMinutes(1);

    private readonly ISchedulerFactory _schedulers;
    private readonly IExecutionHistoryStore _history;
    private readonly RecurringJobs _recurring;
    private readonly TimeProvider _time;

    public JobsService(ISchedulerFactory schedulers, IExecutionHistoryStore history, RecurringJobs recurring, TimeProvider time)
    {
        _schedulers = schedulers;
        _history = history;
        _recurring = recurring;
        _time = time;
    }

    public async Task<JobsOverviewDto> Overview(CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        var headers = (await scheduler.QueryJobs(new JobQuery { Take = PagedQuery.All }, ct)).Items;
        var statuses = (await _history.QueryJobRunStatuses(new JobRunStatusQuery { SchedulerName = scheduler.SchedulerName, Take = PagedQuery.All }, ct)).Items
            .ToDictionary(s => s.Job);
        var running = (await scheduler.QueryFireInstances(new FireInstanceQuery { State = FireInstanceState.Executing, Take = PagedQuery.All }, ct)).Items;

        var jobs = new List<JobDto>();
        // The recurring jobs first, in the order they're registered; then anything else in the store
        var keys = _recurring.All.Select(j => j.Job.Key).Where(k => headers.Any(h => h.Key.Equals(k)))
            .Concat(headers.Select(h => h.Key).Where(k => _recurring.Find(k) is null).OrderBy(k => k.Group).ThenBy(k => k.Name));
        foreach (var key in keys)
        {
            var header = headers.First(h => h.Key.Equals(key));
            jobs.Add(await Job(scheduler, header, statuses.GetValueOrDefault(key), running.Where(r => key.Equals(r.JobKey)).ToList(), ct));
        }

        var nodes = await scheduler.QueryClusterNodes(ct);
        return new JobsOverviewDto
        {
            Jobs = jobs,
            Nodes = nodes.Select(n => new JobNodeDto { InstanceID = n.InstanceId, LastCheckInAt = n.LastCheckInUtc?.UtcDateTime, State = n.State.ToString() }).ToList(),
        };
    }

    private async Task<JobDto> Job(IScheduler scheduler, JobHeader header, JobRunStatus? status, List<FireInstance> running, CancellationToken ct)
    {
        var recurring = _recurring.Find(header.Key);
        var triggerKey = recurring?.TriggerKey ?? new TriggerKey(header.Key.Name, header.Key.Group);
        var trigger = await scheduler.GetTrigger(triggerKey, ct);
        var state = trigger is null ? TriggerState.None : await scheduler.GetTriggerState(triggerKey, ct);
        var pause = state == TriggerState.Paused ? await scheduler.GetTriggerPause(triggerKey, ct) : null;
        var defaultTrigger = recurring?.DefaultTrigger(_time.GetUtcNow()) as ISimpleTrigger;
        return new JobDto
        {
            Group = header.Key.Group,
            Name = header.Key.Name,
            Description = recurring?.Description ?? header.Description,
            Recurring = recurring is not null,
            Enabled = recurring?.Enabled ?? true,
            State = state switch
            {
                TriggerState.None => "Off",
                TriggerState.Paused => "Paused",
                TriggerState.Error => "Error",
                TriggerState.Blocked => "Blocked",
                _ => "Scheduled",
            },
            IntervalSeconds = (trigger as ISimpleTrigger)?.RepeatInterval is { } interval ? (long)interval.TotalSeconds : null,
            DefaultIntervalSeconds = defaultTrigger is null ? null : (long)defaultTrigger.RepeatInterval.TotalSeconds,
            NextRunAt = state == TriggerState.Paused ? null : trigger?.NextFireTimeUtc?.UtcDateTime,
            PreviousRunAt = trigger?.PreviousFireTimeUtc?.UtcDateTime,
            PausedReason = pause?.Reason,
            PausedBy = pause?.RequestedBy,
            PausedAt = pause?.PausedAtUtc.UtcDateTime,
            Running = running.Select(r => new JobRunningDto { FireInstanceID = r.FireInstanceId, StartedAt = r.FireTimeUtc.UtcDateTime, Node = r.SchedulerInstanceId }).ToList(),
            Status = status is null ? null : new JobStatusDto
            {
                LastResult = status.LastResult.ToString(),
                LastRunAt = status.LastFiredAtUtc.UtcDateTime,
                LastDurationMs = status.LastDuration.TotalMilliseconds,
                LastSucceededAt = status.LastSucceededAtUtc?.UtcDateTime,
                LastFailedAt = status.LastFailedAtUtc?.UtcDateTime,
                LastFailureMessage = status.LastFailureMessage,
                ConsecutiveFailures = status.ConsecutiveFailures,
                RunCount = status.RunCount,
                FailureCount = status.FailureCount,
            },
        };
    }

    /// <summary>A job's latest runs, newest first.</summary>
    public async Task<List<JobRunDto>> History(JobKey key, int take, CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        var runs = await _history.QueryExecutions(new ExecutionHistoryQuery { SchedulerName = scheduler.SchedulerName, Job = key, Take = take }, ct);
        return runs.Items.OrderByDescending(r => r.FiredAtUtc).Select(r => new JobRunDto
        {
            FiredAt = r.FiredAtUtc.UtcDateTime,
            DurationMs = r.Duration.TotalMilliseconds,
            Result = r.EffectiveResult.ToString(),
            Error = r.ExceptionMessage ?? r.Summary,
            Node = r.SchedulerInstanceId,
            Manual = r.Manual,
            Trigger = r.TriggerName,
        }).ToList();
    }

    /// <summary>Runs it now, on the worker (within a few seconds: it looks for new triggers every 5).</summary>
    public async Task<JobChange> RunNow(JobKey key, CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        if (!await scheduler.Exists(key, ct))
            return JobChange.NotFound;
        if (_recurring.Find(key) is { Enabled: false })
            return JobChange.Conflict;
        await scheduler.TriggerJob(key, cancellationToken: ct);
        return JobChange.Done;
    }

    /// <summary>Pauses (every trigger of the job, a waiting retry too) or resumes it.</summary>
    public async Task<JobChange> SetPaused(JobKey key, bool paused, string? by, CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        if (!await scheduler.Exists(key, ct))
            return JobChange.NotFound;
        if (paused)
            await scheduler.PauseJobWith(key, new PauseDetails { Reason = PausedReason, RequestedBy = by }, ct);
        else
            await scheduler.ResumeJob(key, ct);
        return JobChange.Done;
    }

    /// <summary>
    /// A recurring job runs every <paramref name="interval"/> from now on: next one interval after its last run (or now,
    /// when that has passed). A pause stays.
    /// </summary>
    public Task<JobChange> SetInterval(JobKey key, TimeSpan interval, CancellationToken ct = default)
    {
        if (interval < MinInterval || interval > MaxInterval)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Between {MinInterval} and {MaxInterval}");
        return Replace(key, (job, current, now) =>
        {
            var start = (current.PreviousFireTimeUtc ?? now) + interval;
            return current.GetTriggerBuilder()
                .StartAt(start < now ? now : start)
                .WithSimpleSchedule(s => s.WithInterval(interval).RepeatForever().WithMisfireInstruction((SimpleTriggerMisfireInstruction)current.MisfireInstructionCode))
                .Build();
        }, ct);
    }

    /// <summary>A recurring job's trigger made from its default again (its first run as the default says). A pause stays.</summary>
    public Task<JobChange> ResetSchedule(JobKey key, CancellationToken ct = default)
        => Replace(key, (job, _, now) => job.DefaultTrigger(now), ct);

    private async Task<JobChange> Replace(JobKey key, Func<RecurringJob, ITrigger, DateTimeOffset, ITrigger> replacement, CancellationToken ct)
    {
        var job = _recurring.Find(key);
        if (job is null)
            return JobChange.NotFound;
        var scheduler = await _schedulers.GetScheduler(ct);
        if (!job.Enabled || await scheduler.GetTrigger(job.TriggerKey, ct) is not { } current)
            return JobChange.Conflict;
        var pause = await scheduler.GetTriggerState(job.TriggerKey, ct) == TriggerState.Paused
            ? await scheduler.GetTriggerPause(job.TriggerKey, ct) ?? new PauseInfo(PausedReason, null, _time.GetUtcNow())
            : null;
        var now = _time.GetUtcNow();
        var trigger = replacement(job, current, now);
        // The new trigger isn't paused (Quartz drops a trigger's own pause when it's replaced) and the pause is put back
        // in a second call: a paused job's trigger can't be due before then, or a worker could fire it in between.
        // Resumed, a start that has passed is a misfire, as it would have been.
        if (pause is not null && trigger.StartTimeUtc < now + PausedStartGrace)
            trigger = trigger.GetTriggerBuilder().StartAt(now + PausedStartGrace).Build();
        await scheduler.RescheduleJob(job.TriggerKey, trigger, ct);
        if (pause is not null)
            await scheduler.PauseTriggerWith(job.TriggerKey, new PauseDetails { Reason = pause.Reason, RequestedBy = pause.RequestedBy }, ct);
        return JobChange.Done;
    }

    /// <summary>Asks a running job to stop (its cancellation token), on whichever node runs it.</summary>
    public async Task<bool> Interrupt(string fireInstanceID, CancellationToken ct = default)
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        return await scheduler.InterruptFireInstance(fireInstanceID, ct);
    }
}
