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
        var sources = new HistoricalSourceService(Context, new HistoricalSourceFetcher(new HttpClient(_handler)), _time, NullLogger<HistoricalSourceService>.Instance);
        var jobs = new HistoricalImportJobs(Context, new HistoricalImportSignal());
        _runner = new HistoricalImportRunner(Context, sources, Substitute.For<IHistoricalImportService>(), jobs,
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
}
