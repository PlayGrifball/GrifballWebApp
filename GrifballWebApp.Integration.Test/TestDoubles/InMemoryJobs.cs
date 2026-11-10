using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Quartz on its in-memory store, never started (an API pod's view: it stores, it doesn't fire), with recurring jobs
/// in it: for what reads and changes their triggers, without a database's job store.
/// </summary>
internal sealed class InMemoryJobs : IAsyncDisposable
{
    private sealed class NoOpJob : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private readonly ServiceProvider _services;

    public ISchedulerFactory Factory { get; }
    public IScheduler Scheduler { get; }
    public RecurringJobs Jobs { get; }
    public TimeProvider Time { get; }

    /// <summary>The on-demand jobs over this scheduler: what they queue stays queued (it never fires).</summary>
    public OnDemandJobs OnDemand => new(Factory, _services.GetRequiredService<Quartz.Extensibility.IExecutionHistoryStore>());

    private InMemoryJobs(ServiceProvider services, IScheduler scheduler, RecurringJobs jobs, TimeProvider time)
    {
        _services = services;
        Factory = services.GetRequiredService<ISchedulerFactory>();
        Scheduler = scheduler;
        Jobs = jobs;
        Time = time;
    }

    /// <param name="configure">Services the jobs' <see cref="RecurringJob.OnCreated"/> needs (a context).</param>
    public static async Task<InMemoryJobs> Create(TimeProvider time, Action<IServiceCollection>? configure, params RecurringJob[] jobs)
    {
        var recurring = new RecurringJobs();
        foreach (var job in jobs)
            recurring.Add<NoOpJob>(job);
        var services = new ServiceCollection().AddLogging();
        configure?.Invoke(services);
        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();
            foreach (var job in jobs)
                q.AddJob(typeof(NoOpJob), j => j.WithIdentity(job.Key).StoreDurably());
        });
        // The runs' history, in memory: what OnDemand reads
        services.AddQuartzExecutionHistory();
        var provider = services.BuildServiceProvider();
        return new InMemoryJobs(provider, await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(), recurring, time);
    }

    /// <summary>What the worker does as it starts: each job without a trigger gets its default one.</summary>
    public async Task Seed()
    {
        var seeder = new RecurringJobSeeder(Factory, Jobs, _services.GetRequiredService<IServiceScopeFactory>(), Time, NullLogger<RecurringJobSeeder>.Instance);
        await seeder.StartAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await Scheduler.Shutdown();
        await _services.DisposeAsync();
    }
}
