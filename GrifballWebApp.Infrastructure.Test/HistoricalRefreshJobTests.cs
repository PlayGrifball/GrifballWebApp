using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Historical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The scheduled historical refresh (HistoricalRefreshJob): it queues a refresh unless a job is queued or running, then
/// waits and tries again later (a retry trigger), saying so on the page; a pause from before Quartz is carried over to
/// its trigger once. And the page's view of it (HistoricalImportSchedule). A real (temp-file SQLite) GrifballContext and
/// HistoricalImportJobs, a substituted Quartz scheduler, and a fake clock.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalRefreshJobTests : SqliteContextFixture
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(Now);
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private HistoricalImportJobs _jobs = null!;

    [SetUp]
    public void SetUp() => _jobs = new HistoricalImportJobs((IApplicationDbContext)Context, new HistoricalImportSignal(), _time);

    private HistoricalRefreshJob Job() => new(_jobs, Context, _time, NullLogger<HistoricalRefreshJob>.Instance);

    private async Task<int> QueueManualRefresh()
    {
        var job = HistoricalImportJob.Refresh(HistoricalImportJobTrigger.Manual);
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        return job.HistoricalImportJobID;
    }

    private async Task<HistoricalImportSettings> SavedSettings()
    {
        Context.ChangeTracker.Clear();
        return await Context.HistoricalImportSettings.AsNoTracking().SingleAsync();
    }

    [Test]
    public async Task Run_NothingQueued_QueuesAScheduledRefresh_AndRecordsIt()
    {
        var jobId = await Job().RunAsync(CancellationToken.None);

        var settings = await SavedSettings();
        var queued = await Context.HistoricalImportJobs.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(jobId, Is.EqualTo(queued.HistoricalImportJobID));
            Assert.That((queued.Kind, queued.Trigger, queued.Status),
                Is.EqualTo((HistoricalImportJobKind.Refresh, HistoricalImportJobTrigger.Scheduled, HistoricalImportJobStatus.Queued)));
            Assert.That((settings.LastScheduledJobID, settings.LastScheduledRunAt, settings.LastScheduledRunNote),
                Is.EqualTo(((int?)queued.HistoricalImportJobID, (DateTime?)Now.UtcDateTime, (string?)null)));
        }
    }

    [Test]
    public async Task Run_AJobQueued_WaitsSayingSo_AndQueuesNothing()
    {
        var manual = await QueueManualRefresh();

        var first = await Job().RunAsync(CancellationToken.None);
        var second = await Job().RunAsync(CancellationToken.None);

        var settings = await SavedSettings();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((first, second), Is.EqualTo(((int?)null, (int?)null)));
            Assert.That(settings.LastScheduledRunNote, Is.EqualTo(HistoricalRefreshJob.WaitingNote));
            Assert.That(settings.LastScheduledJobID, Is.Null);
            Assert.That(await Context.HistoricalImportJobs.Select(j => j.HistoricalImportJobID).ToListAsync(), Is.EqualTo(new[] { manual }));
        }
    }

    [Test]
    public async Task Run_AfterAWait_ClearsTheNote()
    {
        var manual = await QueueManualRefresh();
        await Job().RunAsync(CancellationToken.None);
        await Context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == manual)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, HistoricalImportJobStatus.Succeeded).SetProperty(j => j.FinishedAt, Now.UtcDateTime));
        Context.ChangeTracker.Clear();

        var jobId = await Job().RunAsync(CancellationToken.None);

        var settings = await SavedSettings();
        Assert.That((settings.LastScheduledJobID, settings.LastScheduledRunNote), Is.EqualTo((jobId, (string?)null)));
    }

    [Test]
    public async Task Execute_AJobRunning_TriesAgainInFiveMinutes_ReplacingAnEarlierRetry()
    {
        await QueueManualRefresh();
        var context = Substitute.For<IJobExecutionContext>();
        context.Scheduler.Returns(_scheduler);

        await Job().Execute(context);

        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(HistoricalRefreshJob.RetryTriggerKey) && t.JobKey.Equals(HistoricalRefreshJob.Key)
                && t.StartTimeUtc == Now + HistoricalRefreshJob.RetryAfter && t.Description == HistoricalRefreshJob.WaitingNote),
            TriggerConflict.Replace, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Execute_NothingQueued_QueuesTheRefresh_AndNoRetry()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.Scheduler.Returns(_scheduler);

        await Job().Execute(context);

        Assert.That(await Context.HistoricalImportJobs.CountAsync(j => j.Trigger == HistoricalImportJobTrigger.Scheduled), Is.EqualTo(1));
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }

    [Test]
    public async Task CarryOverPause_APauseFromBeforeQuartz_PausesTheTrigger_Once()
    {
        Context.HistoricalImportSettings.Add(HistoricalImportSettings.PausedBeforeQuartz());
        await Context.SaveChangesAsync();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(GrifballContext)).Returns(Context);

        await HistoricalRefreshJob.CarryOverPauseAsync(_scheduler, services, CancellationToken.None);

        await _scheduler.Received(1).PauseTriggerWith(HistoricalRefreshJob.TriggerKey,
            Arg.Is<PauseDetails?>(d => d != null && d.Reason == HistoricalRefreshJob.PausedReason), Arg.Any<CancellationToken>());
        Assert.That((await SavedSettings()).SchedulePaused, Is.False, "carried over: the trigger holds the pause from now on");
    }

    [Test]
    public async Task CarryOverPause_NothingPaused_LeavesTheTriggerAlone()
    {
        Context.HistoricalImportSettings.Add(HistoricalImportSettings.Create());
        await Context.SaveChangesAsync();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(GrifballContext)).Returns(Context);

        await HistoricalRefreshJob.CarryOverPauseAsync(_scheduler, services, CancellationToken.None);

        await _scheduler.DidNotReceiveWithAnyArgs().PauseTriggerWith(default!, default, default);
    }

    [Test]
    public void Definition_FirstRunsTenMinutesAfterTheWorkerStarts_ThenDaily()
    {
        var trigger = (ISimpleTrigger)HistoricalRefreshJob.Definition.DefaultTrigger(Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trigger.StartTimeUtc, Is.EqualTo(Now.AddMinutes(10)));
            Assert.That(trigger.RepeatInterval, Is.EqualTo(TimeSpan.FromDays(1)));
            Assert.That(trigger.Key, Is.EqualTo(HistoricalRefreshJob.TriggerKey));
        }
    }

    // ---- HistoricalImportSchedule ----

    private HistoricalImportSchedule Schedule()
    {
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        return new HistoricalImportSchedule(schedulers, _jobs);
    }

    /// <summary>The refresh's trigger: every <paramref name="interval"/>, next at <paramref name="next"/>.</summary>
    private void RefreshTrigger(TimeSpan interval, DateTimeOffset next, TriggerState state)
    {
        var trigger = Substitute.For<ISimpleTrigger>();
        trigger.Key.Returns(HistoricalRefreshJob.TriggerKey);
        trigger.RepeatInterval.Returns(interval);
        trigger.NextFireTimeUtc.Returns(next);
        _scheduler.GetTrigger(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(trigger);
        _scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(state);
    }

    [Test]
    public async Task Get_Running_ShowsItsIntervalNextRunAndLastQueuedJob()
    {
        var jobId = await Job().RunAsync(CancellationToken.None);
        RefreshTrigger(TimeSpan.FromHours(12), Now.AddHours(12), TriggerState.Normal);

        var dto = await Schedule().Get();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((dto.Paused, dto.Interval, dto.NextRunAt), Is.EqualTo((false, TimeSpan.FromHours(12), (DateTime?)Now.AddHours(12).UtcDateTime)));
            Assert.That((dto.LastJobID, dto.LastRunAt, dto.LastRunNote), Is.EqualTo((jobId, (DateTime?)Now.UtcDateTime, (string?)null)));
        }
    }

    [Test]
    public async Task Get_Paused_HasNoNextRun()
    {
        RefreshTrigger(TimeSpan.FromDays(1), Now.AddHours(3), TriggerState.Paused);

        var dto = await Schedule().Get();

        Assert.That((dto.Paused, dto.NextRunAt), Is.EqualTo((true, (DateTime?)null)));
    }

    [Test]
    public async Task Get_NoTriggerYet_ShowsTheDefaultInterval()
    {
        var dto = await Schedule().Get();

        Assert.That((dto.Paused, dto.Interval, dto.NextRunAt, dto.LastJobID), Is.EqualTo((false, TimeSpan.FromDays(1), (DateTime?)null, (int?)null)));
    }

    [Test]
    public async Task SetPaused_Resume_ResumesTheRetryToo()
    {
        _scheduler.ResumeTrigger(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Schedule().SetPaused(false, "admin"), Is.True);
        await _scheduler.Received(1).ResumeTrigger(HistoricalRefreshJob.RetryTriggerKey, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetPaused_ResumeWithNoTrigger_IsFalse_AndTouchesNoRetry()
    {
        _scheduler.ResumeTrigger(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(false);

        Assert.That(await Schedule().SetPaused(false, "admin"), Is.False);
        await _scheduler.DidNotReceive().ResumeTrigger(HistoricalRefreshJob.RetryTriggerKey, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetPaused_Pause_DropsAWaitingRetry_AndSaysWhoPausedIt()
    {
        _scheduler.PauseTriggerWith(HistoricalRefreshJob.TriggerKey, Arg.Any<PauseDetails?>(), Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Schedule().SetPaused(true, "admin"), Is.True);
        await _scheduler.Received(1).UnscheduleJob(HistoricalRefreshJob.RetryTriggerKey, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).PauseTriggerWith(HistoricalRefreshJob.TriggerKey,
            Arg.Is<PauseDetails?>(d => d != null && d.Reason == HistoricalRefreshJob.PausedReason && d.RequestedBy == "admin"), Arg.Any<CancellationToken>());
    }
}
