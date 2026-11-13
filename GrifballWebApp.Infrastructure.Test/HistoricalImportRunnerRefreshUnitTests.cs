using System.Net;
using System.Text;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using GrifballWebApp.Application.Historical.Vods;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// HistoricalImportRunner.Run's Refresh branch: downloads the sources (HistoricalSourceService, over a fake
/// HttpMessageHandler), and a scheduled run whose changed sources include one the import reads queues a guarded
/// import job; a manual one, or nothing changed, queues none. Import/Preview jobs (IHistoricalImportService) stay
/// GrifballWebApp.Integration.Test's: this only reaches the Refresh branch, on a real (temp-file SQLite)
/// GrifballContext and HistoricalImportJobs.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportRunnerRefreshUnitTests : SqliteContextFixture
{
    private FakeTimeProvider _time = null!;
    private Func<HttpRequestMessage, HttpResponseMessage> _respond = null!;
    private HistoricalSourcesFakeHandler _handler = null!;
    private HistoricalImportRunner _runner = null!;
    private HistoricalSourceService _sources = null!;
    private HistoricalImportJobs _jobs = null!;
    private readonly IHistoricalImportService _import = Substitute.For<IHistoricalImportService>();
    private static readonly byte[] Xlsx = HistoricalXlsx.Workbook(("Stats", HistoricalXlsx.StatsTab(("A", "T", 1))));
    private static readonly Func<HistoricalImportProgress, Task> NoProgress = _ => Task.CompletedTask;
    private static readonly string[] expectedChanged = ["2019-wl19-schedule-stats"];
    private static readonly string[] expectedFailed = ["2019-wl19-schedule-stats: no route"];

    [SetUp]
    public async Task Setup()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _respond = _ => HistoricalSourcesFakeHandler.Status(HttpStatusCode.NotFound);
        _handler = new HistoricalSourcesFakeHandler(request => _respond(request));
        var sources = _sources = new HistoricalSourceService(Context, new HistoricalSourceFetcher(new HttpClient(_handler)), _time, NullLogger<HistoricalSourceService>.Instance);
        var jobs = _jobs = new HistoricalImportJobs(Context, new HistoricalImportSignal());
        _runner = new HistoricalImportRunner(Context, sources, _import, jobs,
            Options.Create(new HistoricalGuardrailOptions()), NullLogger<HistoricalImportRunner>.Instance);

        // Only the one import-tracked source stays enabled: the test's one download
        await sources.EnsureCatalog();
        await Context.HistoricalSources.Where(s => s.HistoricalSourceID != "2019-wl19-schedule-stats")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false));
        Context.ChangeTracker.Clear();
    }

    private async Task<int> QueueRefresh(HistoricalImportJobTrigger trigger)
    {
        var job = HistoricalImportJob.Refresh(trigger);
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        return job.HistoricalImportJobID;
    }

    [Test]
    public async Task Scheduled_WithAChangedSourceTheImportReads_QueuesAGuardedImport()
    {
        _respond = _ => HistoricalSourcesFakeHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType);
        var jobId = await QueueRefresh(HistoricalImportJobTrigger.Scheduled);

        var report = await _runner.Run(jobId, DateTime.UtcNow, NoProgress, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesChanged, Is.EqualTo(expectedChanged));
            Assert.That(report.ImportJobID, Is.Not.Null);
        }
        var queued = await Context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == report.ImportJobID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((queued.Kind, queued.Trigger, queued.Guarded), Is.EqualTo((HistoricalImportJobKind.Import, HistoricalImportJobTrigger.Scheduled, true)));
            Assert.That(queued.ChangedSourceIDs, Is.EqualTo("2019-wl19-schedule-stats"));
        }
    }

    [Test]
    public async Task Manual_WithAChangedSourceTheImportReads_QueuesNoImport()
    {
        _respond = _ => HistoricalSourcesFakeHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType);
        var jobId = await QueueRefresh(HistoricalImportJobTrigger.Manual);

        var report = await _runner.Run(jobId, DateTime.UtcNow, NoProgress, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesChanged, Is.EqualTo(expectedChanged));
            Assert.That(report.ImportJobID, Is.Null, "A manual refresh never queues an import on its own");
        }
        Assert.That(await Context.HistoricalImportJobs.CountAsync(j => j.Kind == HistoricalImportJobKind.Import), Is.Zero);
    }

    [Test]
    public async Task NothingChanged_QueuesNoImport()
    {
        _respond = _ => HistoricalSourcesFakeHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType);
        await _runner.Run(await QueueRefresh(HistoricalImportJobTrigger.Scheduled), DateTime.UtcNow, NoProgress, CancellationToken.None);
        _time.Advance(TimeSpan.FromDays(1));

        var report = await _runner.Run(await QueueRefresh(HistoricalImportJobTrigger.Scheduled), DateTime.UtcNow, NoProgress, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesChanged, Is.Empty);
            Assert.That(report.SourcesUnchanged, Is.EqualTo(1));
            Assert.That(report.ImportJobID, Is.Null);
        }
    }

    [Test]
    public async Task AFailedDownload_IsReported_AndQueuesNoImport()
    {
        _respond = _ => throw new HttpRequestException("no route");
        var jobId = await QueueRefresh(HistoricalImportJobTrigger.Scheduled);

        var report = await _runner.Run(jobId, DateTime.UtcNow, NoProgress, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesFailed, Is.EqualTo(expectedFailed));
            Assert.That(report.ImportJobID, Is.Null);
        }
    }

    [Test]
    public async Task APreviewAfterARefresh_ReadsTheSnapshotItDownloaded_AsADryRun()
    {
        _respond = _ => HistoricalSourcesFakeHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType);
        await _runner.Run(await QueueRefresh(HistoricalImportJobTrigger.Manual), Claim, NoProgress, CancellationToken.None);
        var report = new HistoricalImportReport();
        _import.Run(Arg.Any<HistoricalArchiveInput>(), Arg.Any<HistoricalImportRunOptions>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(report);
        var preview = HistoricalImportJob.Preview();
        Context.HistoricalImportJobs.Add(preview);
        await Context.SaveChangesAsync();

        var previewed = await _runner.Run(preview.HistoricalImportJobID, Claim, NoProgress, CancellationToken.None);

        Assert.That(previewed, Is.SameAs(report));
        await _import.Received(1).Run(Arg.Is<HistoricalArchiveInput>(i => i.Catalog != null && i.Catalog.Sources.Any(c => c.Id == "2019-wl19-schedule-stats")),
            Arg.Is<HistoricalImportRunOptions>(o => o.DryRun), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The run's claim: a fixed time.</summary>
    private static readonly DateTime Claim = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The catalog's channel sources gone (dead), the one stats source answering: the refresh downloads it, and
    /// the VODs step has no channel to fetch.</summary>
    private async Task NoChannelsAsync()
    {
        _respond = _ => HistoricalSourcesFakeHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType);
        await Context.HistoricalSources.Where(s => s.Kind == HistoricalSourceKind.YouTubeChannel).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDead, true));
        Context.ChangeTracker.Clear();
    }

    /// <summary>A runner with the VODs step, its VodService on <paramref name="vodsContext"/>; no channel source is
    /// enabled, so it finds nothing to fetch.</summary>
    private HistoricalImportRunner RunnerWithVods(GrifballContext vodsContext) => new(Context, _sources, _import, _jobs,
        Options.Create(new HistoricalGuardrailOptions()), NullLogger<HistoricalImportRunner>.Instance,
        new VodService(vodsContext, new YouTubeClient(new HttpClient(_handler)), Options.Create(new YouTubeOptions()), NullLogger<VodService>.Instance));

    [Test]
    public async Task ARefreshsVodStep_WithNoChannelToFetch_FailsNothing()
    {
        await NoChannelsAsync();
        var report = await RunnerWithVods(Context).Run(await QueueRefresh(HistoricalImportJobTrigger.Manual), Claim, NoProgress, CancellationToken.None);

        Assert.That(report.SourcesFailed, Is.Empty);
    }

    [Test]
    public async Task AVodStepThatFails_IsReported_AndTheRefreshStands()
    {
        await NoChannelsAsync();
        var gone = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).Options);
        await gone.DisposeAsync();
        var jobId = await QueueRefresh(HistoricalImportJobTrigger.Manual);

        var report = await RunnerWithVods(gone).Run(jobId, Claim, NoProgress, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesFailed, Has.Count.EqualTo(1).And.Some.StartsWith("VODs: "));
            Assert.That(report.SourcesChanged, Is.EqualTo(expectedChanged), "the sources' refresh stands");
        }
    }
}
