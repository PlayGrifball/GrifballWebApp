using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Quartz on its in-memory store and run history, never started (an API pod's view: it stores, it doesn't fire), under
/// a scheduler name of its own (Quartz keeps schedulers by name, process-wide: tests in parallel would share one). For
/// what queues on-demand jobs (<see cref="OnDemand"/>), without a database's job store.
/// </summary>
internal sealed class InMemoryQuartz : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public IScheduler Scheduler { get; }

    public OnDemandJobs OnDemand { get; }

    private InMemoryQuartz(ServiceProvider services, IScheduler scheduler)
    {
        _services = services;
        Scheduler = scheduler;
        OnDemand = new OnDemandJobs(services.GetRequiredService<ISchedulerFactory>(), services.GetRequiredService<IExecutionHistoryStore>());
    }

    public static async Task<InMemoryQuartz> Create()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();
            q.ConfigureScheduler(options => options.InstanceName = $"server-test-{Guid.NewGuid():N}");
        });
        services.AddQuartzExecutionHistory();
        var provider = services.BuildServiceProvider();
        return new InMemoryQuartz(provider, await provider.GetRequiredService<ISchedulerFactory>().GetScheduler());
    }

    public async ValueTask DisposeAsync()
    {
        await Scheduler.Shutdown(waitForJobsToComplete: false);
        await _services.DisposeAsync();
    }
}
