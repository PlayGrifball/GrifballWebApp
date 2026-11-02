using Quartz;
using Quartz.Extensibility;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.Jobs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnDemandJobState
{
    /// <summary>Neither queued nor running: <see cref="OnDemandJobStatusDto.LastRun"/> says how the last one went.</summary>
    Idle,
    /// <summary>Waiting for the worker (it looks every 5 s), or for a run of the same job to finish.</summary>
    Queued,
    Running,
}

/// <summary>Where a page's run of an on-demand job is.</summary>
public class OnDemandJobStatusDto
{
    public OnDemandJobState State { get; init; }
    /// <summary>While running: how far it got (0 to 100) and what it is doing, as the job reports it.</summary>
    public int? Progress { get; init; }
    public string? ProgressMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    /// <summary>The last run that finished (within the history kept, a week).</summary>
    public OnDemandRunDto? LastRun { get; init; }
}

public class OnDemandRunDto
{
    public DateTime FiredAt { get; init; }
    public double DurationMs { get; init; }
    /// <summary>Succeeded, Failed, Cancelled or Skipped.</summary>
    public required string Result { get; init; }
    public string? Error { get; init; }
    /// <summary>What the job reported (its JobRunReport): its summary, and its metrics as JSON. Not sent to pages.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Summary { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? MetricsJson { get; init; }
}

/// <summary>
/// Runs a job once for a page, with an input (Quartz's one-off jobs): <see cref="Enqueue{TJob, TInput}"/> stores a
/// trigger named after what it is for (a season match, a season), which the worker fires within a few seconds, and
/// <see cref="Status{TJob}"/> reads where it is. A request while one is queued or running for the same thing adds
/// nothing. Works from any pod, like the Jobs page (<see cref="JobsService"/>).
/// </summary>
public class OnDemandJobs
{
    private readonly ISchedulerFactory _schedulers;
    private readonly IExecutionHistoryStore _history;

    public OnDemandJobs(ISchedulerFactory schedulers, IExecutionHistoryStore history)
    {
        _schedulers = schedulers;
        _history = history;
    }

    /// <summary>The key Quartz keeps a one-off job type's runs under.</summary>
    public static JobKey JobKeyOf(Type type) => new(type.Name, SchedulerConstants.ScheduledJobGroup);

    /// <summary>A run's trigger: named after what it is for, in a group named after the job.</summary>
    public static TriggerKey TriggerKeyOf<TJob>(string name) where TJob : IJob => new(name, typeof(TJob).Name);

    /// <summary>Queues a run of <typeparamref name="TJob"/> for <paramref name="name"/>, unless one is queued or running. Returns where it is.</summary>
    public async Task<OnDemandJobStatusDto> Enqueue<TJob, TInput>(string name, TInput input, string? description = null, string? executionGroup = null, CancellationToken ct = default)
        where TJob : class, IJob<TInput>
    {
        var current = await Status<TJob>(name, ct);
        if (current.State != OnDemandJobState.Idle)
            return current;
        var scheduler = await _schedulers.GetScheduler(ct);
        // Keep: two requests at once store one trigger (the store decides under its lock)
        await scheduler.ScheduleJob<TJob, TInput>(input, TimeSpan.Zero, new OneOffJobOptions
        {
            Name = name,
            Group = typeof(TJob).Name,
            Description = description,
            ExecutionGroup = executionGroup,
            OnConflict = TriggerConflict.Keep,
        }, ct);
        return await Status<TJob>(name, ct);
    }

    public async Task<OnDemandJobStatusDto> Status<TJob>(string name, CancellationToken ct = default) where TJob : IJob
    {
        var scheduler = await _schedulers.GetScheduler(ct);
        var key = TriggerKeyOf<TJob>(name);
        var history = await _history.QueryExecutions(new ExecutionHistoryQuery { SchedulerName = scheduler.SchedulerName, Trigger = key, Take = 1 }, ct);
        var last = history.Items.FirstOrDefault() is { } run ? new OnDemandRunDto
        {
            FiredAt = run.FiredAtUtc.UtcDateTime,
            DurationMs = run.Duration.TotalMilliseconds,
            Result = run.EffectiveResult.ToString(),
            Error = run.ExceptionMessage ?? (run.EffectiveResult == JobRunResult.Failed ? run.Summary : null),
            Summary = run.Summary,
            MetricsJson = run.MetricsJson,
        } : null;

        // Its firing, acquired or executing: by its trigger, as an acquired firing has no job recorded yet. Between the two
        // the trigger has no next fire time, so without the acquired one a run starting would read as idle
        var firing = (await scheduler.QueryFireInstances(new FireInstanceQuery
        {
            TriggerGroup = GroupMatcher<TriggerKey>.GroupEquals(key.Group),
            TriggerName = NameMatcher<TriggerKey>.NameEquals(key.Name),
            State = null,
            Take = PagedQuery.All,
        }, ct)).Items.FirstOrDefault(f => f.TriggerKey.Equals(key));
        if (firing is { State: not FireInstanceState.Executing })
            return new OnDemandJobStatusDto { State = OnDemandJobState.Queued, LastRun = last };
        if (firing is { } running)
            return new OnDemandJobStatusDto
            {
                State = OnDemandJobState.Running,
                Progress = running.Progress,
                ProgressMessage = running.ProgressMessage,
                StartedAt = running.FireTimeUtc.UtcDateTime,
                LastRun = last,
            };

        // A one-shot trigger has a next fire time until it fires
        var trigger = await scheduler.GetTrigger(key, ct);
        return new OnDemandJobStatusDto
        {
            State = trigger?.NextFireTimeUtc is not null ? OnDemandJobState.Queued : OnDemandJobState.Idle,
            LastRun = last,
        };
    }
}
