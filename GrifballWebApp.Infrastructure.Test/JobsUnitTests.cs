using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using GrifballWebApp.Infrastructure.Jobs;
using static GrifballWebApp.Infrastructure.Test.JobsTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>JobsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class JobsUnitTests
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    /// <summary>Counts the runs of <see cref="CountingJob"/> in one host.</summary>
    internal sealed class Runs
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Add() => Interlocked.Increment(ref _count);
    }

    internal sealed class CountingJob(Runs runs) : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            runs.Add();
            return ValueTask.CompletedTask;
        }
    }

    public sealed record CountBy(int Times);

    /// <summary>An on-demand job: counts its input's times, reporting how far it got.</summary>
    [DisallowConcurrentExecution]
    internal sealed class OnDemandCountingJob(Runs runs) : IJob<CountBy>
    {
        public async ValueTask Execute(IJobExecutionContext context, CountBy input, CancellationToken cancellationToken = default)
        {
            // A negative count: runs until it is stopped, half way
            if (input.Times < 0)
            {
                context.ReportProgress(50, "Half way");
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            }
            for (var i = 0; i < input.Times; i++)
                runs.Add();
            context.ReportProgress(100, "Counted");
        }
    }

    [Test]
    public void AJobRegisteredTwice_OrNoConnectionString_FailsAtStartup()
    {
        Assert.That(() => new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1))).Add<CountingJob>(Job(TimeSpan.FromHours(2))),
            Throws.InvalidOperationException.With.Message.Contains("registered twice"));
        Assert.That(() => new RecurringJobs().AddOnDemand<OnDemandCountingJob>("a", "A").AddOnDemand<OnDemandCountingJob>("b", "B"),
            Throws.InvalidOperationException.With.Message.Contains("registered twice"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "Postgres" }).Build();
        Assert.That(() => new ServiceCollection().AddJobs(configuration, _ => { }), Throws.Exception.With.Message.Contains("GrifballContext failed to configure"));
    }
}

/// <summary>JobsTests' helpers, shared with its integration tests.</summary>
internal static class JobsTestData
{
    internal static readonly JobKey Key = new("counting", RecurringJobs.Group);

    internal static RecurringJob Job(TimeSpan every, TimeSpan? startIn = null, bool enabled = true, bool manualOnly = false) => new()
    {
        Key = Key,
        Description = "Counts its runs",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + (startIn ?? TimeSpan.Zero))
            .WithSimpleSchedule(s => s.WithInterval(every).RepeatForever()),
        Enabled = enabled,
        ManualOnly = manualOnly,
    };
}
