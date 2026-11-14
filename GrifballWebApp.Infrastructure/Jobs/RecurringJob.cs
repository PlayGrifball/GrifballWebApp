using Quartz;

namespace GrifballWebApp.Infrastructure.Jobs;

/// <summary>
/// A job the app runs on a schedule of its own (<see cref="RecurringJobs.Add{TJob}"/>). Its trigger is made from
/// <see cref="DefaultSchedule"/> the first time the worker starts without one (<see cref="RecurringJobSeeder"/>); after
/// that the stored trigger is the schedule, so a pause or a new schedule set on the Jobs page outlives restarts and
/// deploys. Resetting it on the Jobs page makes it from the default again.
/// </summary>
public sealed class RecurringJob
{
    /// <summary>The job's key; its trigger has the same name and group.</summary>
    public required JobKey Key { get; init; }

    /// <summary>What it does, for the Jobs page.</summary>
    public required string Description { get; init; }

    /// <summary>The schedule and start of the trigger made when there is none (its key and its job are set already).</summary>
    public required Func<TriggerBuilder<IJob>, DateTimeOffset, TriggerBuilder<IJob>> DefaultSchedule { get; init; }

    /// <summary>
    /// Off (Discord jobs while Discord is off), the worker removes its trigger and the Jobs page can't run it; turned
    /// back on, the trigger is made from the default again.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Run from the Jobs page only (a cleanup a sysadmin starts when they choose): the worker gives it no trigger, so it never
    /// runs on a schedule, can't be given one, and can't be paused. Its <see cref="DefaultSchedule"/> isn't used.
    /// </summary>
    public bool ManualOnly { get; init; }

    /// <summary>Runs once, in a scope of its own, when the seeder has just made the trigger.</summary>
    public Func<IScheduler, IServiceProvider, CancellationToken, Task>? OnCreated { get; init; }

    public TriggerKey TriggerKey => new(Key.Name, Key.Group);

    /// <summary>The trigger <see cref="DefaultSchedule"/> describes, starting from <paramref name="now"/>.</summary>
    public ITrigger DefaultTrigger(DateTimeOffset now)
        => DefaultSchedule(TriggerBuilder.Create().WithIdentity(TriggerKey).ForJob(Key).WithDescription(Description), now).Build();
}

/// <summary>The recurring jobs, registered with <see cref="JobsServiceCollectionExtensions.AddJobs"/>.</summary>
public sealed class RecurringJobs
{
    /// <summary>The group every recurring job's key and trigger are in.</summary>
    public const string Group = "recurring";

    private readonly List<(Type Type, RecurringJob Job)> _jobs = [];

    public IReadOnlyList<(Type Type, RecurringJob Job)> All => _jobs;

    public RecurringJobs Add<TJob>(RecurringJob job) where TJob : class, IJob
    {
        if (_jobs.Any(j => j.Job.Key.Equals(job.Key)))
            throw new InvalidOperationException($"Recurring job {job.Key} is registered twice");
        _jobs.Add((typeof(TJob), job));
        return this;
    }

    public RecurringJob? Find(JobKey key) => _jobs.Select(j => j.Job).FirstOrDefault(j => j.Key.Equals(key));

    private readonly List<OnDemandJob> _onDemand = [];

    /// <summary>The jobs started from a page with an input of their own (<see cref="OnDemandJobs"/>).</summary>
    public IReadOnlyList<OnDemandJob> OnDemand => _onDemand;

    /// <param name="name">What the Jobs page calls it (its key is its class's name, Quartz's for one-off jobs).</param>
    public RecurringJobs AddOnDemand<TJob>(string name, string description) where TJob : class, IJob
    {
        var job = new OnDemandJob(typeof(TJob), name, description);
        if (_onDemand.Any(j => j.Key.Equals(job.Key)))
            throw new InvalidOperationException($"On-demand job {job.Key} is registered twice");
        _onDemand.Add(job);
        return this;
    }

    public OnDemandJob? FindOnDemand(JobKey key) => _onDemand.FirstOrDefault(j => j.Key.Equals(key));
}

/// <summary>
/// A job run once per request, with an input (Quartz's one-off jobs: <see cref="OnDemandJobs.Enqueue{TJob, TInput}"/>):
/// one durable job per type, in Quartz's group for them, and a trigger per run.
/// </summary>
public sealed record OnDemandJob(Type Type, string Name, string Description)
{
    public JobKey Key => OnDemandJobs.JobKeyOf(Type);
}
