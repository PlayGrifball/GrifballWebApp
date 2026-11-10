using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Infrastructure.Historical.Sources;

namespace GrifballWebApp.Integration.Test;

/// <summary>The sources catalog in the database, refreshes (snapshots kept by hash) and the fallback to the last snapshot.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalSourceServiceTests
{
    private GrifballContext _context = null!;
    private FakeTimeProvider _time = null!;
    private Func<HttpRequestMessage, HttpResponseMessage> _respond = null!;
    private FakeSourcesHandler _handler = null!;
    private HistoricalSourceService _service = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _respond = _ => FakeSourcesHandler.Status(HttpStatusCode.NotFound);
        _handler = new FakeSourcesHandler(request => _respond(request));
        _service = new HistoricalSourceService(_context, new HistoricalSourceFetcher(new HttpClient(_handler)), _time, NullLogger<HistoricalSourceService>.Instance);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static readonly byte[] Xlsx = HistoricalXlsx.Workbook(("Stats", HistoricalXlsx.StatsTab(("A", "T", 1))));
    private static readonly string[] expected = new[] { "2019-wl19-schedule-stats", "late-league-website" };
    private static readonly string[] expectedArray = new[] { "late-league-website" };
    private static readonly string[] expectedArray0 = new[] { "2019-wl19-schedule-stats", "late-league-website", "late-league-website" };
    private static readonly string[] expectedArray1 = new[] { "youtube-grifball-channel" };
    private static readonly string[] expectedArray2 = new[] { "https://www.youtube.com/channel/UCTVJapRl-GGwLCfioTb9N8w" };
    private static readonly string[] expectedArray3 = new[] { "https://web.archive.org/web/20250329022355id_/https://playgrifball.com/", "https://docs.google.com/spreadsheets/d/key/gviz/tq?tqx=out:csv" };

    /// <summary>Only these catalog sources stay enabled: the test's downloads.</summary>
    private async Task OnlyEnabled(params string[] ids)
    {
        await _service.EnsureCatalog();
        await _context.HistoricalSources.Where(s => !ids.Contains(s.HistoricalSourceID)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false));
        _context.ChangeTracker.Clear();
    }

    [Test]
    public async Task TheCatalog_IsWrittenAtRuntime_AndASysadminsChoiceIsKept()
    {
        await _service.EnsureCatalog();
        var source = await _context.HistoricalSources.SingleAsync(s => s.HistoricalSourceID == "2019-wl19-schedule-stats");
        source.SetEnabled(false);
        _context.Entry(source).Property(s => s.Title).CurrentValue = "edited";
        await _context.SaveChangesAsync();

        await _service.EnsureCatalog();
        var list = await _service.List();

        _context.ChangeTracker.Clear();
        var again = await _context.HistoricalSources.AsNoTracking().SingleAsync(s => s.HistoricalSourceID == "2019-wl19-schedule-stats");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(list, Has.Count.EqualTo(HistoricalSourceCatalog.Sources.Count));
            Assert.That((again.Enabled, again.Title), Is.EqualTo((false, "WL19 Schedule & Stats")), "Enabled is the sysadmin's, the rest the catalog's");
            Assert.That(again.Config, Does.Contain("Team and Player Stats"));
            var dead = list.Single(s => s.SourceID == "2018-fl18-schedule");
            Assert.That((dead.IsDead, dead.Enabled, dead.Imported), Is.EqualTo((true, false, false)));
            var stats = list.Single(s => s.SourceID == "2019-wl19-schedule-stats");
            Assert.That((stats.Imported, stats.Kind, stats.League, stats.GameVersion, stats.SnapshotCount, stats.LatestSnapshotSize), Is.EqualTo((true, "GoogleSheet", (string?)"AGLA", (string?)"halo3", 0, (long?)null)));
        }
    }

    [Test]
    public async Task ARefresh_KeepsASnapshotOnlyWhenTheContentChanged()
    {
        await OnlyEnabled("2019-wl19-schedule-stats", "late-league-website");
        var page = "<html>Late League</html>";
        _respond = request => request.RequestUri!.Host == "docs.google.com"
            ? FakeSourcesHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType)
            : FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes(page), "text/html");

        var first = await _service.Refresh();
        _time.Advance(TimeSpan.FromDays(1));
        var second = await _service.Refresh();
        page = "<html>Late League, season 9</html>";
        _time.Advance(TimeSpan.FromDays(1));
        var third = await _service.Refresh();

        var snapshots = await _context.Set<HistoricalSourceSnapshot>().AsNoTracking().OrderBy(s => s.HistoricalSourceSnapshotID)
            .Select(s => new { s.HistoricalSourceID, s.Sha256, s.Size, s.ContentType }).ToListAsync();
        var list = await _service.List();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Changed.Select(c => c.SourceID), Is.EquivalentTo(expected));
            Assert.That((second.Changed.Count, second.Unchanged, second.Failed.Count), Is.EqualTo((0, 2, 0)));
            Assert.That(third.Changed.Select(c => c.SourceID), Is.EqualTo(expectedArray));
            Assert.That(snapshots.Select(s => s.HistoricalSourceID), Is.EqualTo(expectedArray0));
            Assert.That(snapshots.Select(s => s.Sha256.Length), Is.All.EqualTo(64));
            Assert.That(snapshots[0].ContentType, Is.EqualTo(HistoricalSourceFetcher.XlsxContentType));
            Assert.That(snapshots[0].Size, Is.EqualTo(Xlsx.Length));
            var sheetKey = (await _context.HistoricalSources.AsNoTracking().SingleAsync(s => s.HistoricalSourceID == "2019-wl19-schedule-stats")).SheetKey;
            Assert.That(_handler.Requests, Does.Contain(HistoricalSourceFetcher.ExportUrl(sheetKey!)));
            var website = list.Single(s => s.SourceID == "late-league-website");
            Assert.That((website.SnapshotCount, website.LastFetchStatus, website.LastChangedAt, website.LastFetchedAt), Is.EqualTo((2, (string?)"Changed", (DateTime?)_time.GetUtcNow().UtcDateTime, (DateTime?)_time.GetUtcNow().UtcDateTime)));
            Assert.That(list.Single(s => s.SourceID == "2019-wl19-schedule-stats").LastFetchStatus, Is.EqualTo("Unchanged"));
            var latest = await _service.LatestSnapshotIds();
            Assert.That(latest, Is.EquivalentTo(new[] { first.Changed[0].SnapshotID, third.Changed[0].SnapshotID }), "Each source's newest");
        }
    }

    [Test]
    public async Task AFailedDownload_KeepsTheLastSnapshot_ForTheImport()
    {
        await OnlyEnabled("2019-wl19-schedule-stats");
        _respond = _ => FakeSourcesHandler.Ok(Xlsx, HistoricalSourceFetcher.XlsxContentType);
        var first = await _service.Refresh();

        // Gone private: Google answers with its sign-in page
        _respond = request =>
        {
            var response = FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes("<html>sign in</html>"), "text/html");
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://accounts.google.com/signin");
            return response;
        };
        var privateNow = await _service.Refresh();
        _respond = _ => throw new HttpRequestException("no route");
        var unreachable = await _service.Refresh();
        _respond = _ => throw new TaskCanceledException();
        var slow = await _service.Refresh();

        var latest = await _service.LatestSnapshotIds();
        var contents = await _service.Load(latest);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(privateNow.Failed.Single(), Is.EqualTo(("2019-wl19-schedule-stats", "the xlsx export answered 200")));
            Assert.That(unreachable.Failed.Single().Reason, Is.EqualTo("no route"));
            Assert.That(slow.Failed.Single().Reason, Is.EqualTo("timed out"));
            Assert.That(await _context.Set<HistoricalSourceSnapshot>().CountAsync(), Is.EqualTo(1));
            Assert.That((await _context.HistoricalSources.AsNoTracking().SingleAsync(s => s.HistoricalSourceID == "2019-wl19-schedule-stats")).LastFetchStatus, Is.EqualTo("Failed: timed out"));
            Assert.That(latest, Is.EqualTo(new[] { first.Changed.Single().SnapshotID }));
            Assert.That(contents.Single().Content, Is.EqualTo(Xlsx), "The import reads the last snapshot");
            Assert.That(contents[0].Source.HistoricalSourceID, Is.EqualTo("2019-wl19-schedule-stats"));
        }
    }

    [Test]
    public async Task ASheetThatRefusesTheExport_FallsBackToItsTabsCsv()
    {
        await OnlyEnabled("2026-sl26-major-schedule");
        var csv = "Week 1,https://www.halowaypoint.com/x\n";
        _respond = request => request.RequestUri!.AbsolutePath.EndsWith("/export")
            ? FakeSourcesHandler.Status(HttpStatusCode.Unauthorized)
            : FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes(csv), "text/csv");

        var ok = await _service.Refresh();
        _respond = _ => FakeSourcesHandler.Status(HttpStatusCode.Unauthorized);
        var refused = await _service.Refresh();

        var snapshot = await _context.Set<HistoricalSourceSnapshot>().AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok.Changed, Has.Count.EqualTo(1));
            Assert.That(snapshot.ContentType, Is.EqualTo("text/csv"));
            Assert.That(_handler.Requests, Does.Contain(HistoricalSourceFetcher.CsvUrl("1DBxPmiTg6yGyL7fjy5NmwSL0sr2ei1thnXD-vP32oNk", "1497794987")));
            Assert.That(refused.Failed.Single().Reason, Is.EqualTo("the CSV export answered 401 (text/html)"));
        }
    }

    [Test]
    public async Task DeadAndDisabledSources_AreNotDownloaded()
    {
        await OnlyEnabled("2018-fl18-schedule", "youtube-grifball-channel");
        await _context.HistoricalSources.Where(s => s.HistoricalSourceID == "2018-fl18-schedule").ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, true));
        _respond = _ => FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes("<html>channel</html>"), "text/html");

        var result = await _service.Refresh();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Changed.Select(c => c.SourceID), Is.EqualTo(expectedArray1));
            Assert.That(_handler.Requests, Is.EqualTo(expectedArray2));
        }
    }

    [Test]
    public async Task ASourceTheCatalogRevives_TakesTheCatalogsEnabledOnlyIfNeverDownloaded()
    {
        // Both were listed dead by an earlier catalog and are alive (and enabled) in this one. The website was dead from its
        // first sync (stored disabled, never downloaded); the stats sheet was downloaded, then turned off by a sysadmin.
        await OnlyEnabled("late-league-website");
        await _context.HistoricalSources.Where(s => s.HistoricalSourceID == "late-league-website")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDead, true).SetProperty(x => x.Enabled, false));
        await _context.HistoricalSources.Where(s => s.HistoricalSourceID == "2019-wl19-schedule-stats")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDead, true).SetProperty(x => x.LastFetchedAt, (DateTime?)_time.GetUtcNow().UtcDateTime.AddDays(-30)));
        _respond = _ => FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes("<html>Late League</html>"), "text/html");

        var result = await _service.Refresh();

        _context.ChangeTracker.Clear();
        var website = await _context.HistoricalSources.AsNoTracking().SingleAsync(s => s.HistoricalSourceID == "late-league-website");
        var stats = await _context.HistoricalSources.AsNoTracking().SingleAsync(s => s.HistoricalSourceID == "2019-wl19-schedule-stats");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((website.IsDead, website.Enabled, website.LastFetchStatus), Is.EqualTo((false, true, (string?)HistoricalSource.ChangedStatus)), "Never downloaded: the catalog's");
            Assert.That((stats.IsDead, stats.Enabled), Is.EqualTo((false, false)), "Downloaded before: the sysadmin's off is kept");
            Assert.That(result.Changed.Select(c => c.SourceID), Is.EqualTo(expectedArray));
        }
    }

    [Test]
    public async Task SetEnabled_ChangesOnlyAKnownSource_ThatIsntDeadWhenEnabling()
    {
        await _service.EnsureCatalog();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _service.SetEnabled("late-league-website", false), Is.EqualTo(HistoricalSourceSetEnabledResult.Set));
            Assert.That(await _service.SetEnabled("2018-fl18-schedule", true), Is.EqualTo(HistoricalSourceSetEnabledResult.Dead), "A dead source is never enabled");
            Assert.That(await _context.HistoricalSources.Where(s => s.HistoricalSourceID == "2018-fl18-schedule").Select(s => s.Enabled).SingleAsync(), Is.False);
            Assert.That(await _service.SetEnabled("nope", true), Is.Null);
            Assert.That(await _context.HistoricalSources.Where(s => s.HistoricalSourceID == "late-league-website").Select(s => s.Enabled).SingleAsync(), Is.False);
        }
    }

    [Test]
    public async Task EachKind_IsDownloadedFromItsUrl()
    {
        var fetcher = new HistoricalSourceFetcher(new HttpClient(_handler));
        _respond = request => request.RequestUri!.Host == "empty.example" ? FakeSourcesHandler.Ok([], "text/html") : FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes("a,b"), "");

        var wayback = await fetcher.Fetch(HistoricalTestData.Source(url: "https://playgrifball.com/", kind: HistoricalSourceKind.Wayback, waybackTimestamp: "20250329022355"), CancellationToken.None);
        var csv = await fetcher.Fetch(HistoricalTestData.Source(url: "x", kind: HistoricalSourceKind.GoogleSheetCsv, sheetKey: "key"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.Requests, Is.EqualTo(expectedArray3));
            Assert.That(wayback.ContentType, Is.EqualTo("text/html"), "A page without a content type is HTML");
            Assert.That(csv.ContentType, Is.EqualTo("text/csv"));
            await Assert.ThrowsAsync<HistoricalFetchException>(() => fetcher.Fetch(HistoricalTestData.Source(url: "x", kind: HistoricalSourceKind.GoogleSheet), CancellationToken.None));
            await Assert.ThrowsAsync<HistoricalFetchException>(() => fetcher.Fetch(HistoricalTestData.Source(url: "x", kind: HistoricalSourceKind.GoogleSheetCsv), CancellationToken.None));
            await Assert.ThrowsAsync<HistoricalFetchException>(() => fetcher.Fetch(HistoricalTestData.Source(url: "x", kind: HistoricalSourceKind.Wayback), CancellationToken.None));
            await Assert.ThrowsAsync<HistoricalFetchException>(() => fetcher.Fetch(HistoricalTestData.Source(url: "https://empty.example/", kind: HistoricalSourceKind.WebPage), CancellationToken.None));
        }
    }
}
