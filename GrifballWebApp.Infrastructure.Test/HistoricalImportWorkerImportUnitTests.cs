using System.Text.Json;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// HistoricalImportWorker running import jobs, and HistoricalImportRunner's import and preview runs, on a real
/// (temp-file SQLite) GrifballContext behind a real DI container, with the importer (IHistoricalImportService)
/// substituted: an uploaded archive goes to it as uploaded, its progress moves the job, and a failure fails the job; a
/// preview or a guarded import of the sources' snapshots runs it as a dry run or with the guardrail, and the seasons a
/// sysadmin decides on become the job's reviews. No source has a snapshot, so the archive the runner builds is empty
/// and nothing downloads.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportWorkerImportUnitTests : SqliteContextFixture
{
    private ServiceProvider _provider = null!;
    private HistoricalImportWorker _worker = null!;
    private IHistoricalImportService _import = null!;
    private readonly LogRecorder<HistoricalImportWorker> _workerLog = new();
    private readonly LogRecorder<HistoricalImportRunner> _runnerLog = new();

    [SetUp]
    public void Setup()
    {
        _import = Substitute.For<IHistoricalImportService>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GrifballContext>(o => o.UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<GrifballContext>());
        services.AddSingleton(new HistoricalImportSignal());
        services.AddSingleton(new XuidLookupSignal());
        services.AddSingleton(new HttpClient(new NeverCalledHandler()));
        services.AddScoped(sp => new HistoricalSourceFetcher(sp.GetRequiredService<HttpClient>()));
        services.AddScoped(sp => new HistoricalSourceService(sp.GetRequiredService<GrifballContext>(), sp.GetRequiredService<HistoricalSourceFetcher>(),
            TimeProvider.System, sp.GetRequiredService<ILogger<HistoricalSourceService>>()));
        services.AddScoped(sp => new HistoricalImportJobs(sp.GetRequiredService<IApplicationDbContext>(), sp.GetRequiredService<HistoricalImportSignal>()));
        services.AddSingleton(_import);
        services.AddSingleton(Options.Create(new HistoricalGuardrailOptions()));
        services.AddScoped(sp => new HistoricalImportRunner(sp.GetRequiredService<GrifballContext>(), sp.GetRequiredService<HistoricalSourceService>(),
            sp.GetRequiredService<IHistoricalImportService>(), sp.GetRequiredService<HistoricalImportJobs>(),
            sp.GetRequiredService<IOptions<HistoricalGuardrailOptions>>(), _runnerLog));
        _provider = services.BuildServiceProvider();

        _worker = new HistoricalImportWorker(_provider.GetRequiredService<IServiceScopeFactory>(), _provider.GetRequiredService<HistoricalImportSignal>(),
            Options.Create(new HistoricalImportWorkerOptions()), TimeProvider.System, _workerLog,
            _provider.GetRequiredService<XuidLookupSignal>());
    }

    [TearDown]
    public async Task TearDown() => await _provider.DisposeAsync();

    /// <summary>No source has a snapshot: the runner reads nothing, so nothing should ask the network.</summary>
    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("Nothing should ask the network");
    }

    private async Task<int> Queue(HistoricalImportJob job)
    {
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return job.HistoricalImportJobID;
    }

    private async Task<HistoricalImportJob> Reload(int jobId)
    {
        Context.ChangeTracker.Clear();
        return await Context.HistoricalImportJobs.AsNoTracking().Include(j => j.Reviews).SingleAsync(j => j.HistoricalImportJobID == jobId);
    }

    private static HistoricalImportReport ReportOf(params HistoricalSeasonRemoval[] removals)
    {
        var report = new HistoricalImportReport { SeasonsCreated = 1 };
        report.SeasonRemovals.AddRange(removals);
        return report;
    }

    private void ImportRunAnswers(HistoricalImportReport report)
        => _import.Run(Arg.Any<HistoricalArchiveInput>(), Arg.Any<HistoricalImportRunOptions>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(report);

    [Test]
    public async Task AnUploadedArchive_IsImportedAsUploaded_ItsProgressMovingTheJob()
    {
        var upload = new HistoricalArchiveInput { Seasons = new UnifiedArchive(), RecordedGames = [] };
        var jobId = await Queue(HistoricalImportJob.Upload(JsonSerializer.Serialize(upload, HistoricalArchiveInput.JsonOptions)));
        var report = new HistoricalImportReport { SeasonsCreated = 3 };
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await call.Arg<Func<HistoricalImportProgress, Task>?>()!(new HistoricalImportProgress("Importing seasons", 2, 5));
                return report;
            });

        Assert.That(await _worker.RunNextAsync(CancellationToken.None), Is.True);

        var job = await Reload(jobId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((job.Status, job.Stage), Is.EqualTo((HistoricalImportJobStatus.Succeeded, HistoricalImportJob.SucceededStage)));
            Assert.That((job.SeasonsDone, job.SeasonsTotal), Is.EqualTo((2, 5)), "the importer's progress, saved on the job");
            Assert.That(JsonSerializer.Deserialize<HistoricalImportReport>(job.Report!, HistoricalArchiveInput.JsonOptions)!.SeasonsCreated, Is.EqualTo(3));
            Assert.That(job.Payload, Is.Null, "the archive goes once the job ends");
            Assert.That(_workerLog.At(LogLevel.Information), Does.Contain($"Historical import job {jobId} finished"));
        }
        await _import.Received(1).Import(Arg.Is<HistoricalArchiveInput>(i => i.RecordedGames != null && i.RecordedGames.Count == 0),
            Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AnImportThatThrows_FailsTheJob_WithWhy()
    {
        var upload = new HistoricalArchiveInput { Seasons = new UnifiedArchive() };
        var jobId = await Queue(HistoricalImportJob.Upload(JsonSerializer.Serialize(upload, HistoricalArchiveInput.JsonOptions)));
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("outer", new InvalidDataException("the sheet has no header row")));

        Assert.That(await _worker.RunNextAsync(CancellationToken.None), Is.True);

        var job = await Reload(jobId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((job.Status, job.Stage), Is.EqualTo((HistoricalImportJobStatus.Failed, HistoricalImportJob.FailedStage)));
            Assert.That(job.Error, Is.EqualTo("the sheet has no header row"), "the innermost reason");
            Assert.That(job.Report, Is.Null);
        }
    }

    [Test]
    public async Task APreview_IsADryRun_AndEverySeasonItWouldTakeRowsFromIsListed()
    {
        var jobId = await Queue(HistoricalImportJob.Preview());
        ImportRunAnswers(ReportOf(
            new HistoricalSeasonRemoval { SeasonKey = "s2020/Stats", SeasonName = "Season 2020", StatsBefore = 10, StatsRemoved = 2 },
            new HistoricalSeasonRemoval { SeasonKey = "s2021/Stats", SeasonName = "Season 2021", StatsBefore = 10 }));

        Assert.That(await _worker.RunNextAsync(CancellationToken.None), Is.True);

        var job = await Reload(jobId);
        var review = job.Reviews.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(HistoricalImportJobStatus.Succeeded), job.Error);
            Assert.That((review.SeasonKey, review.SeasonName, review.Status, review.StatsBefore, review.StatsRemoved),
                Is.EqualTo(("s2020/Stats", (string?)"Season 2020", HistoricalSeasonReviewStatus.Preview, 10, 2)), "the season that removes nothing isn't listed");
            Assert.That(_runnerLog.At(LogLevel.Information), Does.Contain($"Historical Preview job {jobId} (Manual): 1 seasons, none need review"));
            Assert.That(job.SnapshotIDs, Is.Empty, "no snapshot was read: none was there");
        }
        await _import.Received(1).Run(Arg.Any<HistoricalArchiveInput>(), Arg.Is<HistoricalImportRunOptions>(o => o.DryRun && o.Guardrail == null && o.Seasons == null),
            Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AScheduledImport_IsGuarded_AndTheSeasonsTheGuardrailStoppedNeedReview()
    {
        var jobId = await Queue(HistoricalImportJob.ScheduledImport("", "2019-wl19-schedule-stats"));
        var report = ReportOf(new HistoricalSeasonRemoval { SeasonKey = "s2019/Stats", StatsBefore = 10, StatsRemoved = 9, Stopped = true });
        report.Errors.Add("s2018/Stats: rolled back");
        ImportRunAnswers(report);

        Assert.That(await _worker.RunNextAsync(CancellationToken.None), Is.True);

        var job = await Reload(jobId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(HistoricalImportJobStatus.Succeeded), job.Error);
            Assert.That(job.Reviews.Select(r => (r.SeasonKey, r.Status)), Is.EqualTo(new[] { ("s2019/Stats", HistoricalSeasonReviewStatus.NeedsReview) }));
        }
        await _import.Received(1).Run(Arg.Any<HistoricalArchiveInput>(), Arg.Is<HistoricalImportRunOptions>(o => !o.DryRun && o.Guardrail != null),
            Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AnApprovedSeasonsImport_RunsThatSeasonOnly_Unguarded()
    {
        var jobId = await Queue(HistoricalImportJob.ImportSnapshots("", "s2019/Stats\ns2019/Playoffs"));
        ImportRunAnswers(ReportOf());

        var ran = await _worker.RunNextAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ran, Is.True);
            Assert.That((await Reload(jobId)).Status, Is.EqualTo(HistoricalImportJobStatus.Succeeded));
        }
        await _import.Received(1).Run(Arg.Any<HistoricalArchiveInput>(),
            Arg.Is<HistoricalImportRunOptions>(o => !o.DryRun && o.Guardrail == null && o.Seasons != null && o.Seasons.Count == 2
                && o.Seasons.Contains("S2019/STATS") && o.Seasons.Contains("s2019/Playoffs")),
            Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>());
    }

    // ---- the worker's loop, its heartbeat, and a shutdown ----

    private HistoricalImportWorker Worker(TimeProvider time, HistoricalImportWorkerOptions options) => new(_provider.GetRequiredService<IServiceScopeFactory>(),
        _provider.GetRequiredService<HistoricalImportSignal>(), Options.Create(options), time, NullLogger<HistoricalImportWorker>.Instance,
        _provider.GetRequiredService<XuidLookupSignal>());

    private async Task<int> QueueUploadAsync() => await Queue(HistoricalImportJob.Upload(
        JsonSerializer.Serialize(new HistoricalArchiveInput { Seasons = new UnifiedArchive() }, HistoricalArchiveInput.JsonOptions)));

    /// <summary>An import that says it started, then runs until its token is cancelled.</summary>
    private TaskCompletionSource ImportRunsUntilCancelled()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                return new HistoricalImportReport();
            });
        return started;
    }

    [Test]
    public async Task AJobQueuedAgainUnderItsRun_StopsTheRunAtItsNextBeat_LeavingTheJobQueued()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
        using var worker = Worker(time, new HistoricalImportWorkerOptions { StaleAfter = TimeSpan.FromMinutes(3) });
        var jobId = await QueueUploadAsync();
        var started = ImportRunsUntilCancelled();

        var running = worker.RunNextAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(60));
        // Another replica found it stale and queued it again
        await Context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, HistoricalImportJobStatus.Queued));
        time.Advance(TimeSpan.FromMinutes(1));
        var ran = await running.WaitAsync(TimeSpan.FromSeconds(60));

        var job = await Reload(jobId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ran, Is.True);
            Assert.That((job.Status, job.Report, job.Error), Is.EqualTo((HistoricalImportJobStatus.Queued, (string?)null, (string?)null)), "left to the run that queued it again");
        }
    }

    [Test]
    public async Task AShutdownDuringAJob_StopsIt_LeavingItRunningForTheNextStart()
    {
        var jobId = await QueueUploadAsync();
        var started = ImportRunsUntilCancelled();
        using var shutdown = new CancellationTokenSource();

        var running = _worker.RunNextAsync(shutdown.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(60));
        await shutdown.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThatAsync(() => running, Throws.InstanceOf<OperationCanceledException>());
            Assert.That((await Reload(jobId)).Status, Is.EqualTo(HistoricalImportJobStatus.Running), "queued again once its heartbeat is stale");
            Assert.That(_workerLog.At(LogLevel.Information).Single(), Does.StartWith($"Historical import job {jobId} was stopped by a shutdown"));
        }
    }

    [Test]
    public async Task AJobLastStartedAfterNow_WaitsForALaterPass()
    {
        var jobId = await QueueUploadAsync();
        // The clock went back: its last start is after now
        await Context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.StartedAt, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var ran = await _worker.RunNextAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ran, Is.False, "not tried again at once");
            Assert.That((await Reload(jobId)).Status, Is.EqualTo(HistoricalImportJobStatus.Queued));
        }
        await _import.DidNotReceiveWithAnyArgs().Import(default!, default, default);
    }

    [Test]
    public async Task Hosted_RunsWhatIsQueued_AndStopsWithTheHost()
    {
        var jobId = await QueueUploadAsync();
        var report = new HistoricalImportReport { SeasonsCreated = 1 };
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>()).Returns(report);
        using var worker = Worker(TimeProvider.System, new HistoricalImportWorkerOptions { PollInterval = TimeSpan.FromMilliseconds(50) });

        await worker.StartAsync(CancellationToken.None);
        // The worker nudges the XUID lookups once a job has finished
        var finished = await _provider.GetRequiredService<XuidLookupSignal>().WaitAsync(TimeSpan.FromSeconds(60), CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finished, Is.True, "the job finished");
            Assert.That(worker.ExecuteTask?.IsCompletedSuccessfully, Is.True, "a shutdown ends the loop, not a failure");
            Assert.That((await Reload(jobId)).Status, Is.EqualTo(HistoricalImportJobStatus.Succeeded));
        }
    }
}
