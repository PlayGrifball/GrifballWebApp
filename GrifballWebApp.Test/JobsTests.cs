using GrifballWebApp.Server.Hosting;
using GrifballWebApp.Server.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Test;

/// <summary>
/// Quartz on the app's database, on whichever provider the run uses: the AddQuartz migration's tables pass Quartz's
/// schema check, the worker seeds and runs the recurring jobs, a restart keeps a stored trigger, and an API pod's
/// scheduler changes the store without firing anything.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class JobsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly JobKey Key = new("counting", RecurringJobs.Group);

    private string _connectionString = null!;
    private readonly List<IHost> _hosts = [];

    [SetUp]
    public async Task SetUp()
    {
        var name = $"Jobs_{Guid.NewGuid():N}";
        await TestDatabase.CreateMigratedDatabase(SetUpFixture.DatabaseContainer, name);
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, name);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync();
            host.Dispose();
        }
        await TestDatabase.DropDatabase(_connectionString);
    }

    /// <summary>Counts the runs of <see cref="CountingJob"/> in one host.</summary>
    private sealed class Runs
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Add() => Interlocked.Increment(ref _count);
    }

    private sealed class CountingJob(Runs runs) : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            runs.Add();
            return ValueTask.CompletedTask;
        }
    }

    private static RecurringJob Job(TimeSpan every, TimeSpan? startIn = null, bool enabled = true) => new()
    {
        Key = Key,
        Description = "Counts its runs",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + (startIn ?? TimeSpan.Zero))
            .WithSimpleSchedule(s => s.WithInterval(every).RepeatForever()),
        Enabled = enabled,
    };

    /// <summary>A started host like the app's in <paramref name="role"/>, with <paramref name="job"/> as its one recurring job.</summary>
    private async Task<IHost> StartHost(RecurringJob job, AppRoleKind role = AppRoleKind.Worker)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
        });
        builder.Services.AddLogging();
        builder.Services.AddSingleton<Runs>();
        builder.Services.AddJobs(builder.Configuration, jobs => jobs.Add<CountingJob>(job));
        builder.Services.ApplyAppRole(new AppRole(role));
        var host = builder.Build();
        _hosts.Add(host);
        await host.StartAsync();
        return host;
    }

    private static async Task StopHost(IHost host)
    {
        await host.StopAsync();
    }

    private static Task<IScheduler> Scheduler(IHost host) => host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler().AsTask();

    private static async Task<bool> Eventually(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!timeout.IsCancellationRequested)
        {
            if (await condition())
                return true;
            await Task.Delay(200);
        }
        return false;
    }

    [Test]
    public async Task TheWorker_SeedsTheDefaultTrigger_RunsTheJob_AndKeepsItsHistory()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1)));
        var scheduler = await Scheduler(host);
        var runs = host.Services.GetRequiredService<Runs>();

        Assert.That(await Eventually(() => Task.FromResult(runs.Count > 0)), Is.True, "Due at once, it runs");
        var history = host.Services.GetRequiredService<IExecutionHistoryStore>();
        Assert.That(await Eventually(async () => (await history.QueryExecutions(new ExecutionHistoryQuery { SchedulerName = scheduler.SchedulerName, Job = Key })).Items.Count > 0),
            Is.True, "The run is in the database's history");

        var trigger = await scheduler.GetTrigger(new TriggerKey(Key.Name, Key.Group));
        var entry = (await history.QueryExecutions(new ExecutionHistoryQuery { SchedulerName = scheduler.SchedulerName, Job = Key })).Items[0];
        Assert.Multiple(() =>
        {
            Assert.That(scheduler.Status, Is.EqualTo(SchedulerStatus.Running));
            Assert.That(trigger, Is.InstanceOf<ISimpleTrigger>());
            Assert.That(((ISimpleTrigger)trigger!).RepeatInterval, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(trigger!.Description, Is.EqualTo("Counts its runs"));
            Assert.That(entry.Succeeded, Is.True);
            Assert.That(runs.Count, Is.EqualTo(1), "Once: the next is an hour away");
        });
    }

    [Test]
    public async Task ARestart_KeepsTheStoredTrigger_ItsScheduleAndItsPause()
    {
        var first = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        var triggerKey = new TriggerKey(Key.Name, Key.Group);
        await (await Scheduler(first)).PauseTriggerWith(triggerKey, new PauseDetails { Reason = "Paused on the Jobs page" });
        await StopHost(first);

        var second = await StartHost(Job(TimeSpan.FromMinutes(5)));
        var scheduler = await Scheduler(second);

        var trigger = (ISimpleTrigger?)await scheduler.GetTrigger(triggerKey);
        Assert.Multiple(async () =>
        {
            Assert.That(trigger!.RepeatInterval, Is.EqualTo(TimeSpan.FromHours(1)), "The stored schedule, not the new default");
            Assert.That(await scheduler.GetTriggerState(triggerKey), Is.EqualTo(TriggerState.Paused));
            Assert.That((await scheduler.GetTriggerPause(triggerKey))?.Reason, Is.EqualTo("Paused on the Jobs page"));
            Assert.That(second.Services.GetRequiredService<Runs>().Count, Is.Zero);
        });
    }

    [Test]
    public async Task AJobTurnedOff_LosesItsTrigger_AndGetsTheDefaultBackOnceOn()
    {
        var triggerKey = new TriggerKey(Key.Name, Key.Group);
        var on = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        Assert.That(await (await Scheduler(on)).Exists(triggerKey), Is.True);
        await StopHost(on);

        var off = await StartHost(Job(TimeSpan.FromHours(1), enabled: false));
        var scheduler = await Scheduler(off);
        Assert.Multiple(async () =>
        {
            Assert.That(await scheduler.Exists(triggerKey), Is.False, "Unscheduled");
            Assert.That(await scheduler.Exists(Key), Is.True, "The job itself is durable");
        });
        await StopHost(off);

        var back = await StartHost(Job(TimeSpan.FromHours(2), startIn: TimeSpan.FromHours(1)));
        var trigger = (ISimpleTrigger?)await (await Scheduler(back)).GetTrigger(triggerKey);
        Assert.That(trigger!.RepeatInterval, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public async Task AnApiPod_FiresNothing_ButAJobItRunsNow_RunsOnTheWorker()
    {
        // Not due for an hour: only the manual run runs
        var job = Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1));
        var worker = await StartHost(job);
        var api = await StartHost(job, AppRoleKind.Api);
        var apiScheduler = await Scheduler(api);

        await apiScheduler.TriggerJob(Key);

        Assert.That(await Eventually(() => Task.FromResult(worker.Services.GetRequiredService<Runs>().Count > 0)), Is.True, "The worker ran it");
        Assert.Multiple(() =>
        {
            Assert.That(apiScheduler.Status, Is.Not.EqualTo(SchedulerStatus.Running), "Never started");
            Assert.That(api.Services.GetRequiredService<Runs>().Count, Is.Zero);
        });
    }
}
