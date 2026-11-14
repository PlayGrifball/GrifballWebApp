using Microsoft.Extensions.Logging.Testing;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using static GrifballWebApp.Infrastructure.Test.JobsUnitTests;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// RecurringJobSeeder, as the worker starts: each recurring job without a trigger gets its default one (and its
/// OnCreated runs, in a scope of its own), one already there is kept, one turned off has its trigger removed. A
/// substituted Quartz scheduler and a fake clock.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RecurringJobSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 4, 0, 0, TimeSpan.Zero);
    private static readonly string[] TriggerRemovedLog = ["Recurring job recurring.counting is off or manual only: its trigger was removed"];
    private static readonly JobKey OtherKey = new("other", RecurringJobs.Group);

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private ServiceProvider _services = null!;
    private readonly FakeLogger<RecurringJobSeeder> _log = new();

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _services = new ServiceCollection().BuildServiceProvider();
    }

    [TearDown]
    public async Task TearDown() => await _services.DisposeAsync();

    private RecurringJobSeeder Seeder(RecurringJobs? jobs = null) =>
        new(_schedulers, jobs ?? new RecurringJobs(), _services.GetRequiredService<IServiceScopeFactory>(), new FakeTimeProvider(Now), _log);

    private void Stored(ScheduleOutcome outcome) => _scheduler.ScheduleTrigger(Arg.Any<ITrigger>(), TriggerConflict.Keep, Arg.Any<CancellationToken>())
        .Returns(new ScheduleTriggerResult(Now.AddHours(1), outcome));

    [Test]
    public async Task Seed_NoTriggerYet_MakesTheDefault_AndRunsOnCreated()
    {
        Stored(ScheduleOutcome.Created);
        IServiceProvider? createdWith = null;
        var job = new RecurringJob
        {
            Key = JobsTestData.Key,
            Description = "Counts its runs",
            DefaultSchedule = (trigger, now) => trigger.StartAt(now + TimeSpan.FromMinutes(10)).WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever()),
            OnCreated = (scheduler, services, _) =>
            {
                Assert.That(scheduler, Is.SameAs(_scheduler));
                createdWith = services;
                return Task.CompletedTask;
            },
        };

        var made = await Seeder().SeedAsync(_scheduler, job, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.True);
            Assert.That(createdWith, Is.Not.SameAs(_services).And.Not.Null, "OnCreated runs in a scope of its own");
            Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Information).Single(), Does.StartWith("Recurring job recurring.counting scheduled from its default, first at "));
        }
        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(job.TriggerKey) && t.JobKey.Equals(job.Key) && t.StartTimeUtc == Now.AddMinutes(10)),
            TriggerConflict.Keep, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Seed_ATriggerAlreadyThere_IsKept_AndOnCreatedDoesntRun()
    {
        Stored(ScheduleOutcome.Kept);
        var created = false;
        var job = new RecurringJob
        {
            Key = JobsTestData.Key,
            Description = "Counts its runs",
            DefaultSchedule = (trigger, now) => trigger.StartAt(now),
            OnCreated = (_, _, _) =>
            {
                created = true;
                return Task.CompletedTask;
            },
        };

        var made = await Seeder().SeedAsync(_scheduler, job, CancellationToken.None);

        Assert.That((made, created), Is.EqualTo((false, false)));
    }

    [Test]
    public async Task Seed_TurnedOff_RemovesItsTrigger_AndMakesNone()
    {
        var job = JobsTestData.Job(TimeSpan.FromHours(1), enabled: false);
        _scheduler.UnscheduleJob(job.TriggerKey, Arg.Any<CancellationToken>()).Returns(true);

        var made = await Seeder().SeedAsync(_scheduler, job, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.False);
            Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Information), Is.EqualTo(TriggerRemovedLog));
        }
        await _scheduler.Received(1).UnscheduleJob(job.TriggerKey, Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }

    [Test]
    public async Task Seed_ManualOnly_GetsNoTrigger_SoItNeverRunsOnASchedule()
    {
        var job = JobsTestData.Job(TimeSpan.FromHours(1), manualOnly: true);

        var made = await Seeder().SeedAsync(_scheduler, job, CancellationToken.None);

        Assert.That(made, Is.False);
        await _scheduler.Received(1).UnscheduleJob(job.TriggerKey, Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }

    [Test]
    public async Task Start_SeedsEveryRecurringJob_OnTheScheduler()
    {
        Stored(ScheduleOutcome.Kept);
        var other = new RecurringJob
        {
            Key = OtherKey,
            Description = "Another",
            DefaultSchedule = (trigger, now) => trigger.StartAt(now).WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(2)).RepeatForever()),
        };
        var counting = JobsTestData.Job(TimeSpan.FromHours(1));
        var jobs = new RecurringJobs().Add<CountingJob>(counting).Add<CountingJob2>(other);
        var seeder = Seeder(jobs);

        await seeder.StartAsync(CancellationToken.None);
        await seeder.StopAsync(CancellationToken.None);

        await _scheduler.Received(1).ScheduleTrigger(Arg.Is<ITrigger>(t => t.Key.Equals(counting.TriggerKey)), TriggerConflict.Keep, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ScheduleTrigger(Arg.Is<ITrigger>(t => t.Key.Equals(other.TriggerKey)), TriggerConflict.Keep, Arg.Any<CancellationToken>());
    }

    /// <summary>A second job type: RecurringJobs keeps one entry per key, of any type.</summary>
    internal sealed class CountingJob2 : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
