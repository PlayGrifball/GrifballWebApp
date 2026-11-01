using GrifballWebApp.Server.Hosting;
using GrifballWebApp.Server.Jobs;
using Microsoft.AspNetCore.Mvc;
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
    /// <summary>Runs until it is interrupted (the Jobs page's Stop): counts its start, and its end.</summary>
    private sealed class BlockingJob(Runs runs) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            runs.Add();
            try
            {
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                runs.Add();
            }
        }
    }

    private Task<IHost> StartHost(RecurringJob job, AppRoleKind role = AppRoleKind.Worker) => StartHost<CountingJob>(job, role);

    private async Task<IHost> StartHost<TJob>(RecurringJob job, AppRoleKind role = AppRoleKind.Worker) where TJob : class, IJob
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
        });
        builder.Services.AddLogging();
        builder.Services.AddSingleton<Runs>();
        builder.Services.AddJobs(builder.Configuration, jobs => jobs.Add<TJob>(job));
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

        // "Run now" on the Jobs page, served by the API pod
        Assert.That(await Service(api).RunNow(Key), Is.EqualTo(JobChange.Done));

        Assert.That(await Eventually(() => Task.FromResult(worker.Services.GetRequiredService<Runs>().Count > 0)), Is.True, "The worker ran it");
        Assert.Multiple(() =>
        {
            Assert.That(apiScheduler.Status, Is.Not.EqualTo(SchedulerStatus.Running), "Never started");
            Assert.That(api.Services.GetRequiredService<Runs>().Count, Is.Zero);
        });
    }

    private static JobsService Service(IHost host) => host.Services.GetRequiredService<JobsService>();

    [Test]
    public async Task TheJobsPage_ShowsTheJob_ItsRunsAndTheSchedulers()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1)));
        var service = Service(host);
        Assert.That(await Eventually(async () => (await service.History(Key, 10)).Count > 0), Is.True, "It ran, at once");

        var overview = await service.Overview();
        var history = await service.History(Key, 10);
        var job = overview.Jobs.Single();
        Assert.Multiple(() =>
        {
            Assert.That((job.Group, job.Name, job.Description, job.Recurring, job.Enabled, job.State),
                Is.EqualTo((RecurringJobs.Group, "counting", (string?)"Counts its runs", true, true, "Scheduled")));
            Assert.That((job.IntervalSeconds, job.DefaultIntervalSeconds), Is.EqualTo(((long?)3600, (long?)3600)));
            Assert.That(job.NextRunAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(50)));
            Assert.That((job.Status?.LastResult, job.Status?.RunCount, job.Status?.ConsecutiveFailures), Is.EqualTo(((string?)"Succeeded", (long?)1, (int?)0)));
            Assert.That(job.Running, Is.Empty);
            Assert.That(history.Select(r => (r.Result, r.Manual, r.Error)), Is.EqualTo(new[] { ("Succeeded", false, (string?)null) }));
            if (TestDatabase.Provider != GrifballWebApp.Database.DatabaseProvider.Sqlite)
                Assert.That(overview.Nodes, Has.Count.EqualTo(1), "The worker checked in to the cluster");
        });
    }

    [Test]
    public async Task TheJobsPage_PausesReschedulesAndResets_KeepingThePause()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        var service = Service(host);
        async Task<JobDto> Shown() => (await service.Overview()).Jobs.Single();

        Assert.That(await service.SetPaused(Key, true, "sysadmin"), Is.EqualTo(JobChange.Done));
        var paused = await Shown();
        Assert.That(await service.SetInterval(Key, TimeSpan.FromMinutes(10)), Is.EqualTo(JobChange.Done));
        var rescheduled = await Shown();
        Assert.That(await service.ResetSchedule(Key), Is.EqualTo(JobChange.Done));
        var reset = await Shown();
        Assert.That(await service.SetPaused(Key, false, "sysadmin"), Is.EqualTo(JobChange.Done));
        var resumed = await Shown();

        Assert.Multiple(async () =>
        {
            Assert.That((paused.State, paused.PausedBy, paused.PausedReason, paused.NextRunAt), Is.EqualTo(("Paused", (string?)"sysadmin", (string?)JobsService.PausedReason, (DateTime?)null)));
            Assert.That((rescheduled.State, rescheduled.IntervalSeconds, rescheduled.PausedBy), Is.EqualTo(("Paused", (long?)600, (string?)"sysadmin")), "A new interval keeps the pause");
            Assert.That((reset.State, reset.IntervalSeconds), Is.EqualTo(("Paused", (long?)3600)), "So does a reset");
            Assert.That((resumed.State, resumed.IntervalSeconds), Is.EqualTo(("Scheduled", (long?)3600)));
            Assert.That(resumed.NextRunAt, Is.Not.Null);
            var unknown = new JobKey("nope", RecurringJobs.Group);
            Assert.That((await service.RunNow(unknown), await service.SetPaused(unknown, true, null), await service.SetInterval(unknown, TimeSpan.FromMinutes(1)), await service.ResetSchedule(unknown)),
                Is.EqualTo((JobChange.NotFound, JobChange.NotFound, JobChange.NotFound, JobChange.NotFound)));
        });
        Assert.That(() => service.SetInterval(Key, TimeSpan.FromSeconds(1)), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public async Task AJobThatIsOff_IsShownOff_AndCantBeRunOrRescheduled()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), enabled: false));
        var service = Service(host);

        var job = (await service.Overview()).Jobs.Single();
        Assert.Multiple(async () =>
        {
            Assert.That((job.State, job.Enabled, job.NextRunAt), Is.EqualTo(("Off", false, (DateTime?)null)));
            Assert.That(await service.RunNow(Key), Is.EqualTo(JobChange.Conflict));
            Assert.That(await service.SetInterval(Key, TimeSpan.FromMinutes(1)), Is.EqualTo(JobChange.Conflict));
            Assert.That(await service.ResetSchedule(Key), Is.EqualTo(JobChange.Conflict));
            Assert.That(host.Services.GetRequiredService<Runs>().Count, Is.Zero);
        });
    }

    [Test]
    public async Task TheController_RefusesAnIntervalOutOfRange_AndMapsWhatTheServiceSays()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        var controller = new JobsController(Service(host));

        Assert.Multiple(async () =>
        {
            Assert.That(await controller.SetInterval(new SetJobIntervalRequest(Key.Group, Key.Name, 1), CancellationToken.None), Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(await controller.SetInterval(new SetJobIntervalRequest(Key.Group, Key.Name, long.MaxValue), CancellationToken.None), Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(await controller.SetInterval(new SetJobIntervalRequest(Key.Group, Key.Name, 60), CancellationToken.None), Is.InstanceOf<NoContentResult>());
            Assert.That(await controller.Run(new JobKeyRequest(Key.Group, "nope"), CancellationToken.None), Is.InstanceOf<NotFoundResult>());
            Assert.That(await controller.Interrupt(new InterruptJobRequest("not-running"), CancellationToken.None), Is.InstanceOf<NotFoundResult>());
            Assert.That((await controller.History(Key.Group, Key.Name, take: 1000, CancellationToken.None)).Value, Is.Empty);
            var roles = typeof(JobsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(a => a.Roles);
            Assert.That(roles, Is.EqualTo(new[] { "Sysadmin" }));
        });
    }

    [Test]
    public async Task ARunningJob_IsShownRunningOnItsNode_AndStopsWhenInterrupted()
    {
        var host = await StartHost<BlockingJob>(Job(TimeSpan.FromHours(1)));
        var service = Service(host);
        var runs = host.Services.GetRequiredService<Runs>();
        Assert.That(await Eventually(async () => (await service.Overview()).Jobs.Single().Running.Count == 1), Is.True, "Due at once, it starts and blocks");

        var job = (await service.Overview()).Jobs.Single();
        var running = job.Running.Single();
        var controller = new JobsController(service);

        Assert.Multiple(() =>
        {
            Assert.That(job.State, Is.EqualTo("Blocked"), "One run at a time: its trigger waits");
            Assert.That(running.Node, Is.Not.Empty);
            Assert.That(running.StartedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
        });
        Assert.That(await controller.Interrupt(new InterruptJobRequest(running.FireInstanceID), CancellationToken.None), Is.InstanceOf<NoContentResult>());
        Assert.That(await Eventually(() => Task.FromResult(runs.Count == 2)), Is.True, "Its cancellation token stopped it");
    }

    [Test]
    public async Task AJobInTheStoreThatIsntRecurring_IsListed_ButHasNoScheduleToChange()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        var scheduler = await Scheduler(host);
        var other = new JobKey("one-off", "manual");
        await scheduler.AddJob(JobBuilder.Create<CountingJob>().WithIdentity(other).WithDescription("A one-off").StoreDurably().Build());
        var service = Service(host);

        var listed = (await service.Overview()).Jobs;
        var oneOff = listed.Single(j => j.Name == "one-off");
        Assert.Multiple(async () =>
        {
            Assert.That(listed.Select(j => j.Name), Is.EqualTo(new[] { "counting", "one-off" }), "The recurring jobs first");
            Assert.That((oneOff.Recurring, oneOff.Enabled, oneOff.State, oneOff.Description, oneOff.IntervalSeconds, oneOff.DefaultIntervalSeconds),
                Is.EqualTo((false, true, "Off", (string?)"A one-off", (long?)null, (long?)null)));
            Assert.That(await service.ResetSchedule(other), Is.EqualTo(JobChange.NotFound));
            Assert.That(await service.SetInterval(other, TimeSpan.FromMinutes(1)), Is.EqualTo(JobChange.NotFound));
        });
    }

    [Test]
    public async Task TheControllersActions_ListPauseResetAndRefuseAJobThatIsOff()
    {
        var on = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        var controller = new JobsController(Service(on))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "sysadmin")], "test")),
                },
            },
        };

        Assert.Multiple(async () =>
        {
            Assert.That((await controller.List(CancellationToken.None)).Value!.Jobs.Single().Name, Is.EqualTo("counting"));
            Assert.That(await controller.SetPaused(new SetJobPausedRequest(Key.Group, Key.Name, true), CancellationToken.None), Is.InstanceOf<NoContentResult>());
            Assert.That((await Service(on).Overview()).Jobs.Single().PausedBy, Is.EqualTo("sysadmin"));
            Assert.That(await controller.ResetSchedule(new JobKeyRequest(Key.Group, Key.Name), CancellationToken.None), Is.InstanceOf<NoContentResult>());
            Assert.That(await controller.SetPaused(new SetJobPausedRequest(Key.Group, "nope", false), CancellationToken.None), Is.InstanceOf<NotFoundResult>());
        });
        await StopHost(on);

        var off = new JobsController(Service(await StartHost(Job(TimeSpan.FromHours(1), enabled: false))));
        Assert.That(await off.Run(new JobKeyRequest(Key.Group, Key.Name), CancellationToken.None), Is.InstanceOf<ConflictResult>());
    }

    [Test]
    public void AJobRegisteredTwice_OrNoConnectionString_FailsAtStartup()
    {
        Assert.That(() => new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1))).Add<CountingJob>(Job(TimeSpan.FromHours(2))),
            Throws.InvalidOperationException.With.Message.Contains("registered twice"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "Postgres" }).Build();
        Assert.That(() => new ServiceCollection().AddJobs(configuration, _ => { }), Throws.Exception.With.Message.Contains("GrifballContext failed to configure"));
    }
}
