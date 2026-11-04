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

    public sealed record CountBy(int Times);

    /// <summary>An on-demand job: counts its input's times, reporting how far it got.</summary>
    [DisallowConcurrentExecution]
    private sealed class OnDemandCountingJob(Runs runs) : IJob<CountBy>
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

    /// <summary>Whether <see cref="BreakableJob"/> can be made: while broken, its constructor throws, as a job whose services fail to resolve.</summary>
    private sealed class Breaker
    {
        public volatile bool Broken;
    }

    /// <summary>An on-demand job that counts its run, unless it can't be made (<see cref="Breaker"/>).</summary>
    private sealed class BreakableJob : IJob<CountBy>
    {
        private readonly Runs _runs;

        public BreakableJob(Runs runs, Breaker breaker)
        {
            if (breaker.Broken)
                throw new InvalidOperationException("A service the job needs failed to resolve");
            _runs = runs;
        }

        public ValueTask Execute(IJobExecutionContext context, CountBy input, CancellationToken cancellationToken = default)
        {
            _runs.Add();
            return ValueTask.CompletedTask;
        }
    }

    private Task<IHost> StartHost(RecurringJob job, AppRoleKind role = AppRoleKind.Worker, bool onDemand = false) => StartHost<CountingJob>(job, role, onDemand);

    /// <param name="onDemand">With <see cref="OnDemandCountingJob"/> too.</param>
    /// <summary>The user each run of <see cref="WhoRunsJob"/> or <see cref="WhoRunsScheduledJob"/> ran as, in order.</summary>
    private sealed class SeenUsers
    {
        public System.Collections.Concurrent.ConcurrentQueue<int?> Ids { get; } = new();
    }

    /// <summary>The app's current user, as a job's scope sees it (no HTTP request, no Discord interaction).</summary>
    private sealed class TestCurrentUser : GrifballWebApp.Database.Services.ICurrentUserService
    {
        private int? _id;
        public void SetCurrentUserIdFromClaims(System.Security.Claims.ClaimsPrincipal? user) { }
        public void SetCurrentUserId(int userId) => _id = userId;
        public int? GetCurrentUserId() => _id;
    }

    public sealed record WhoRuns(int? RequestedByUserID) : IRequestedByUser;

    /// <summary>An on-demand job that notes who it runs as.</summary>
    private sealed class WhoRunsJob(GrifballWebApp.Database.Services.ICurrentUserService user, SeenUsers seen) : IJob<WhoRuns>
    {
        public ValueTask Execute(IJobExecutionContext context, WhoRuns input, CancellationToken cancellationToken = default)
        {
            seen.Ids.Enqueue(user.GetCurrentUserId());
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A recurring job that notes who it runs as.</summary>
    private sealed class WhoRunsScheduledJob(GrifballWebApp.Database.Services.ICurrentUserService user, SeenUsers seen) : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            seen.Ids.Enqueue(user.GetCurrentUserId());
            return ValueTask.CompletedTask;
        }
    }

    private async Task<IHost> StartHost<TJob>(RecurringJob job, AppRoleKind role = AppRoleKind.Worker, bool onDemand = false, Action<RecurringJobs>? more = null) where TJob : class, IJob
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
        });
        builder.Services.AddLogging();
        builder.Services.AddSingleton<Breaker>();
        builder.Services.AddSingleton<Runs>();
        builder.Services.AddSingleton<SeenUsers>();
        builder.Services.AddScoped<GrifballWebApp.Database.Services.ICurrentUserService, TestCurrentUser>();
        builder.Services.AddJobs(builder.Configuration, jobs =>
        {
            jobs.Add<TJob>(job);
            if (onDemand)
                jobs.AddOnDemand<OnDemandCountingJob>("counting-for-a-page", "Counts for a page");
            more?.Invoke(jobs);
        });
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
        // Its history is written a moment before its firing's row is cleared
        Assert.That(await Eventually(async () => (await service.Overview()).Jobs.Single().Running.Count == 0), Is.True, "And finished");

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
    public async Task ResettingAPausedJob_WhoseDefaultStartsAtOnce_IsNotDueBeforeThePauseIsBack()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)));
        var service = Service(host);
        var scheduler = await Scheduler(host);
        Assert.That(await service.SetPaused(Key, true, "sysadmin"), Is.EqualTo(JobChange.Done));

        // Its default starts at once (Job(..., startIn: null)): the reset's trigger would be due while it's replaced
        var resettable = new JobsService(host.Services.GetRequiredService<ISchedulerFactory>(), host.Services.GetRequiredService<IExecutionHistoryStore>(),
            new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1))), TimeProvider.System);
        var before = DateTimeOffset.UtcNow;
        Assert.That(await resettable.ResetSchedule(Key), Is.EqualTo(JobChange.Done));

        var trigger = await scheduler.GetTrigger(new TriggerKey(Key.Name, Key.Group));
        Assert.Multiple(async () =>
        {
            Assert.That(trigger!.StartTimeUtc, Is.GreaterThanOrEqualTo(before + JobsService.PausedStartGrace));
            Assert.That(await scheduler.GetTriggerState(trigger.Key), Is.EqualTo(TriggerState.Paused));
            Assert.That(host.Services.GetRequiredService<Runs>().Count, Is.Zero);
        });
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
            // Its trigger stays scheduled while it runs (one run at a time: the next waits); the page says Running from Running
            Assert.That(job.State, Is.EqualTo("Scheduled"));
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
        Assert.That(() => new RecurringJobs().AddOnDemand<OnDemandCountingJob>("a", "A").AddOnDemand<OnDemandCountingJob>("b", "B"),
            Throws.InvalidOperationException.With.Message.Contains("registered twice"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "Postgres" }).Build();
        Assert.That(() => new ServiceCollection().AddJobs(configuration, _ => { }), Throws.Exception.With.Message.Contains("GrifballContext failed to configure"));
    }

    [Test]
    public async Task AnOnDemandJob_RunsOnceWithItsInput_EvenAskedTwice_AndSaysHowItWent()
    {
        // Asked on an API pod, which fires nothing: queued until a worker starts
        var job = Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1));
        var api = await StartHost(job, AppRoleKind.Api, onDemand: true);
        var jobs = api.Services.GetRequiredService<OnDemandJobs>();
        Assert.That((await jobs.Status<OnDemandCountingJob>("page-1")).State, Is.EqualTo(OnDemandJobState.Idle), "Never run");

        var first = await jobs.Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(3), "Page 1");
        var second = await jobs.Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(3), "Page 1");
        Assert.Multiple(() =>
        {
            Assert.That(first.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(second.State, Is.EqualTo(OnDemandJobState.Queued), "Asked again while it's queued: the same run");
        });
        var worker = await StartHost(job, AppRoleKind.Worker, onDemand: true);
        var runs = worker.Services.GetRequiredService<Runs>();

        Assert.That(await Eventually(async () => (await jobs.Status<OnDemandCountingJob>("page-1")).LastRun is not null), Is.True);
        var done = await jobs.Status<OnDemandCountingJob>("page-1");
        Assert.Multiple(async () =>
        {
            Assert.That((done.State, done.LastRun!.Result, done.LastRun.Error), Is.EqualTo((OnDemandJobState.Idle, "Succeeded", (string?)null)));
            Assert.That(runs.Count, Is.EqualTo(3), "One run, with its input");
            Assert.That((await jobs.Status<OnDemandCountingJob>("page-2")).LastRun, Is.Null, "Another page's runs are its own");
        });
    }

    [Test]
    public async Task AJobSomeoneStarted_RunsAsThem_AScheduledOneAsNobody()
    {
        // The recurring job runs at once on its schedule: nobody started it
        var host = await StartHost<WhoRunsScheduledJob>(Job(TimeSpan.FromHours(1)), more: jobs => jobs.AddOnDemand<WhoRunsJob>("who-runs", "Notes who it runs as"));
        var seen = host.Services.GetRequiredService<SeenUsers>();
        Assert.That(await Eventually(() => Task.FromResult(seen.Ids.Count >= 1)), Is.True, "The scheduled run");

        // Run now from the Jobs page, by user 7; then one queued from a page by user 42, and one by nobody
        Assert.That(await Service(host).RunNow(Key, 7), Is.EqualTo(JobChange.Done));
        Assert.That(await Eventually(() => Task.FromResult(seen.Ids.Count >= 2)), Is.True, "Run now");
        var jobs = host.Services.GetRequiredService<OnDemandJobs>();
        await jobs.Enqueue<WhoRunsJob, WhoRuns>("by-42", new WhoRuns(42));
        Assert.That(await Eventually(() => Task.FromResult(seen.Ids.Count >= 3)), Is.True, "Queued by 42");
        await jobs.Enqueue<WhoRunsJob, WhoRuns>("by-nobody", new WhoRuns(null));
        Assert.That(await Eventually(() => Task.FromResult(seen.Ids.Count >= 4)), Is.True, "Queued by nobody");

        Assert.That(seen.Ids, Is.EqualTo(new int?[] { null, 7, 42, null }));
    }

    [Test]
    public async Task AnOnDemandRunWhoseJobCouldntBeMade_IsIdle_AndAskingAgainRunsIt()
    {
        var host = await StartHost<CountingJob>(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)), more: jobs => jobs.AddOnDemand<BreakableJob>("breakable", "Can't be made while broken"));
        var jobs = host.Services.GetRequiredService<OnDemandJobs>();
        var scheduler = await Scheduler(host);
        var breaker = host.Services.GetRequiredService<Breaker>();
        var trigger = OnDemandJobs.TriggerKeyOf<BreakableJob>("x");

        // Its job can't be made: Quartz leaves the fired trigger in Error
        breaker.Broken = true;
        await jobs.Enqueue<BreakableJob, CountBy>("x", new CountBy(1));
        Assert.That(await Eventually(async () => await scheduler.GetTriggerState(trigger) == TriggerState.Error), Is.True, "The trigger in Error");
        Assert.That((await jobs.Status<BreakableJob>("x")).State, Is.EqualTo(OnDemandJobState.Idle));

        // Asked again (fixed): the errored trigger, with no fire time ahead, is replaced, not kept, and the job runs
        breaker.Broken = false;
        await jobs.Enqueue<BreakableJob, CountBy>("x", new CountBy(1));
        var runs = host.Services.GetRequiredService<Runs>();
        Assert.That(await Eventually(() => Task.FromResult(runs.Count == 1)), Is.True, "Run once asked again");
    }

    [Test]
    public async Task AnOnDemandJob_IsListedOnTheJobsPage_ButNotRunOrPausedFromIt()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)), onDemand: true);
        var service = Service(host);
        var key = OnDemandJobs.JobKeyOf(typeof(OnDemandCountingJob));

        var listed = (await service.Overview()).Jobs;
        var onDemand = listed.Single(j => j.Name == key.Name);
        Assert.Multiple(async () =>
        {
            Assert.That(listed.Select(j => j.Name), Is.EqualTo(new[] { Key.Name, key.Name }), "After the recurring ones");
            Assert.That((onDemand.State, onDemand.DisplayName, onDemand.Description, onDemand.Recurring), Is.EqualTo(("OnDemand", "counting-for-a-page", (string?)"Counts for a page", false)));
            Assert.That(await service.RunNow(key), Is.EqualTo(JobChange.Conflict), "A run needs its page's input");
            Assert.That(await service.SetPaused(key, true, "sysadmin"), Is.EqualTo(JobChange.Conflict));
        });
    }

    [Test]
    public async Task ARunningOnDemandJob_SaysHowFarItGot_AndCanBeStopped()
    {
        var host = await StartHost(Job(TimeSpan.FromHours(1), startIn: TimeSpan.FromHours(1)), onDemand: true);
        var jobs = host.Services.GetRequiredService<OnDemandJobs>();
        await jobs.Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(-1));

        Assert.That(await Eventually(async () => (await jobs.Status<OnDemandCountingJob>("page-1")) is { State: OnDemandJobState.Running, Progress: 50 }), Is.True,
            "Running, with the progress it reported");
        var running = await jobs.Status<OnDemandCountingJob>("page-1");
        Assert.Multiple(() =>
        {
            Assert.That(running.ProgressMessage, Is.EqualTo("Half way"));
            Assert.That(running.StartedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
        });

        var fireInstance = (await Service(host).Overview()).Jobs.Single(j => j.State == "OnDemand").Running.Single().FireInstanceID;
        Assert.That(await Service(host).Interrupt(fireInstance), Is.True);
        Assert.That(await Eventually(async () => (await jobs.Status<OnDemandCountingJob>("page-1")).LastRun is { Result: "Cancelled" or "Failed" }), Is.True, "Stopped");
    }
}
