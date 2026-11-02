using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Test;

/// <summary>
/// The scheduled historical refresh as a Quartz job: its default trigger, a pause carried over from before the move, the
/// Historical Import page's view and pause, and a due run that waits for a queued job.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalScheduleTests
{
    private GrifballContext _context = null!;
    private FakeTimeProvider _time = null!;
    private InMemoryJobs? _jobs;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _time = new FakeTimeProvider(DateTimeOffset.UtcNow);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_jobs is not null)
            await _jobs.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<InMemoryJobs> Jobs() => _jobs = await InMemoryJobs.Create(_time, s => s.AddSingleton(_context), HistoricalRefreshJob.Definition);

    private HistoricalImportJobs ImportJobs() => new(_context, new HistoricalImportSignal(), _time);

    private HistoricalImportSchedule Schedule() => new(_jobs!.Factory, ImportJobs());

    private HistoricalRefreshJob RefreshJob() => new(ImportJobs(), _context, _time, NullLogger<HistoricalRefreshJob>.Instance);

    private IJobExecutionContext Firing()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.Scheduler.Returns(_jobs!.Scheduler);
        return context;
    }

    private Task<HistoricalImportSettings> Settings() => _context.HistoricalImportSettings.AsNoTracking().SingleAsync();

    [Test]
    public async Task TheWorker_MakesADailyTrigger_TenMinutesOut_ThatThePageShows()
    {
        await Jobs();
        var before = await Schedule().Get();
        await _jobs!.Seed();
        var after = await Schedule().Get();

        Assert.Multiple(async () =>
        {
            Assert.That((before.Paused, before.NextRunAt, before.Interval), Is.EqualTo((false, (DateTime?)null, TimeSpan.FromDays(1))), "No trigger yet: the default");
            Assert.That((after.Paused, after.Interval), Is.EqualTo((false, TimeSpan.FromDays(1))));
            Assert.That(after.NextRunAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime.AddMinutes(10)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(await _jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey), Is.EqualTo(TriggerState.Normal));
        });
    }

    [Test]
    public async Task AScheduleThatWasPausedBeforeTheMove_StartsPaused()
    {
        _context.HistoricalImportSettings.Add(new HistoricalImportSettings { HistoricalImportSettingsID = HistoricalImportSettings.SingletonID, SchedulePaused = true });
        await _context.SaveChangesAsync();
        await Jobs();

        await _jobs!.Seed();

        var schedule = await Schedule().Get();
        Assert.Multiple(async () =>
        {
            Assert.That(await _jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey), Is.EqualTo(TriggerState.Paused));
            Assert.That((await _jobs.Scheduler.GetTriggerPause(HistoricalRefreshJob.TriggerKey))?.Reason, Is.EqualTo(HistoricalRefreshJob.PausedReason));
            Assert.That((schedule.Paused, schedule.NextRunAt), Is.EqualTo((true, (DateTime?)null)));
            // Carried over once: a converted database (no Quartz tables) mustn't pause it again after a resume
            Assert.That((await _context.HistoricalImportSettings.AsNoTracking().SingleAsync()).SchedulePaused, Is.False);
        });
    }

    [Test]
    public async Task ARunDue_WhileAJobIsQueued_NotesItAndTriesAgainInFiveMinutes_ThenQueuesTheRefresh()
    {
        var manual = await ImportJobs().Enqueue(new HistoricalImportJob { Kind = HistoricalImportJobKind.Preview });
        await Jobs();
        await _jobs!.Seed();

        await RefreshJob().Execute(Firing());

        var retry = await _jobs.Scheduler.GetTrigger(HistoricalRefreshJob.RetryTriggerKey);
        Assert.Multiple(async () =>
        {
            Assert.That(retry, Is.Not.Null);
            Assert.That(retry!.JobKey, Is.EqualTo(HistoricalRefreshJob.Key));
            Assert.That(retry.NextFireTimeUtc, Is.EqualTo(_time.GetUtcNow() + HistoricalRefreshJob.RetryAfter).Within(TimeSpan.FromSeconds(1)));
            Assert.That((await Settings()).LastScheduledRunNote, Is.EqualTo(HistoricalRefreshJob.WaitingNote));
            Assert.That((await Schedule().Get()).LastRunNote, Is.EqualTo(HistoricalRefreshJob.WaitingNote));
            Assert.That(await _context.HistoricalImportJobs.CountAsync(), Is.EqualTo(1), "Nothing queued alongside it");
        });

        await _context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == manual)
            .ExecuteUpdateAsync(j => j.SetProperty(x => x.Status, HistoricalImportJobStatus.Succeeded));
        await RefreshJob().Execute(Firing());

        var settings = await Settings();
        var queued = await _context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID != manual);
        Assert.Multiple(() =>
        {
            Assert.That((queued.Kind, queued.Trigger), Is.EqualTo((HistoricalImportJobKind.Refresh, HistoricalImportJobTrigger.Scheduled)));
            Assert.That((settings.LastScheduledJobID, settings.LastScheduledRunNote), Is.EqualTo(((int?)queued.HistoricalImportJobID, (string?)null)));
            Assert.That(settings.LastScheduledRunAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime).Within(TimeSpan.FromSeconds(1)));
        });
    }

    [Test]
    public async Task PausingFromThePage_PausesTheTrigger_AndDropsAWaitingRetry_AndResumingResumesIt()
    {
        await Jobs();
        Assert.That(await Schedule().SetPaused(true, "sysadmin"), Is.False, "No trigger until a worker makes it");
        Assert.That(await Schedule().SetPaused(false, "sysadmin"), Is.False, "Nor to resume");
        await _jobs!.Seed();
        await ImportJobs().Enqueue(new HistoricalImportJob { Kind = HistoricalImportJobKind.Preview });
        await RefreshJob().Execute(Firing());
        Assert.That(await _jobs.Scheduler.Exists(HistoricalRefreshJob.RetryTriggerKey), Is.True);

        Assert.That(await Schedule().SetPaused(true, "sysadmin"), Is.True);
        var paused = await Schedule().Get();
        var pause = await _jobs.Scheduler.GetTriggerPause(HistoricalRefreshJob.TriggerKey);
        Assert.Multiple(async () =>
        {
            Assert.That((paused.Paused, paused.NextRunAt), Is.EqualTo((true, (DateTime?)null)));
            Assert.That((pause?.Reason, pause?.RequestedBy), Is.EqualTo(((string?)HistoricalRefreshJob.PausedReason, (string?)"sysadmin")));
            Assert.That(await _jobs.Scheduler.Exists(HistoricalRefreshJob.RetryTriggerKey), Is.False, "Paused, it doesn't try again");
        });

        Assert.That(await Schedule().SetPaused(false, "sysadmin"), Is.True);
        Assert.Multiple(async () =>
        {
            Assert.That((await Schedule().Get()).Paused, Is.False);
            Assert.That(await _jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey), Is.EqualTo(TriggerState.Normal));
        });
    }

    [Test]
    public async Task ResumingFromThePage_AfterAPauseOnTheJobsPage_ResumesTheWaitingRetryToo()
    {
        await Jobs();
        await _jobs!.Seed();
        await ImportJobs().Enqueue(new HistoricalImportJob { Kind = HistoricalImportJobKind.Preview });
        await RefreshJob().Execute(Firing());
        // The Jobs page pauses every trigger of the job, the retry's too
        await _jobs.Scheduler.PauseJobWith(HistoricalRefreshJob.Key, new PauseDetails { Reason = "Paused on the Jobs page" });
        Assert.That(await _jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.RetryTriggerKey), Is.EqualTo(TriggerState.Paused));

        Assert.That(await Schedule().SetPaused(false, "sysadmin"), Is.True);
        Assert.Multiple(async () =>
        {
            Assert.That(await _jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey), Is.EqualTo(TriggerState.Normal));
            Assert.That(await _jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.RetryTriggerKey), Is.EqualTo(TriggerState.Normal));
        });
    }
}
