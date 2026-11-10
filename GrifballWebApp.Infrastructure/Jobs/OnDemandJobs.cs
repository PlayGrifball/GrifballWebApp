using Quartz;
using Quartz.Extensibility;
using GrifballWebApp.Application.Jobs;

namespace GrifballWebApp.Infrastructure.Jobs;

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
        var last = history.Items is [{ } run, ..] ? new OnDemandRunDto
        {
            FiredAt = run.FiredAtUtc.UtcDateTime,
            DurationMs = run.Duration.TotalMilliseconds,
            Result = run.EffectiveResult.ToString(),
            Error = run.ExceptionMessage ?? (run.EffectiveResult == JobRunResult.Failed ? run.Summary : null),
            Summary = run.Summary,
            MetricsJson = run.MetricsJson,
        } : null;

        // Its firing, once it executes
        var running = (await scheduler.QueryFireInstances(new FireInstanceQuery
        {
            TriggerGroup = GroupMatcher<TriggerKey>.GroupEquals(key.Group),
            TriggerName = NameMatcher<TriggerKey>.NameEquals(key.Name),
            Take = PagedQuery.All,
        }, ct)).Items.FirstOrDefault(f => f.TriggerKey.Equals(key));
        if (running is not null)
            return new OnDemandJobStatusDto
            {
                State = OnDemandJobState.Running,
                Progress = running.Progress,
                ProgressMessage = running.ProgressMessage,
                StartedAt = running.FireTimeUtc.UtcDateTime,
                LastRun = last,
            };

        // A one-shot trigger stays in the store from its queuing until its run completes (then it's deleted), whatever state
        // it passes through on the way: there, it's queued (or just starting). One in error (its job couldn't be made) is
        // done with, and a new request replaces it.
        var state = await scheduler.GetTriggerState(key, ct);
        return new OnDemandJobStatusDto
        {
            State = state is TriggerState.None or TriggerState.Error ? OnDemandJobState.Idle : OnDemandJobState.Queued,
            LastRun = last,
        };
    }
}
