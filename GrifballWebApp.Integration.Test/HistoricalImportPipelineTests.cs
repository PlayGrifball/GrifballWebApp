using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using System.Net;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;
using GrifballWebApp.Application.Database;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The import of the sources end to end, through the job queue and the worker: previews (dry runs), the guardrail and its
/// reviews, the scheduled refresh (only changed sources' seasons, nothing when nothing changed, never alongside another
/// run, paused). Downloads are answered by a fake handler; the workbooks are built here.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportPipelineTests
{
    private static readonly string[] SourcesAAndB = ["test-a", "test-b"];
    private static readonly string[] SourceB = ["test-b"];
    private static readonly string[] SourceC = ["test-c"];
    private static readonly int[] DriftedKills = [77];
    private static readonly int[] ReimportedKills = [9];

    private GrifballContext _context = null!;
    private FakeTimeProvider _time = null!;
    private ServiceProvider _services = null!;
    private HistoricalImportSignal _signal = null!;
    /// <summary>What each sheet key's export answers.</summary>
    private readonly Dictionary<string, byte[]> _exports = [];

    private static readonly HistoricalSourceConfig SeasonA = new()
    {
        Season = new SeasonDefaults { Name = "Winter League 2019", Game = "halo3" },
        Tabs = [new() { Name = "Stats" }],
    };
    private static readonly HistoricalSourceConfig SeasonB = new()
    {
        Season = new SeasonDefaults { Name = "Spring League 2019", Game = "halo3" },
        Tabs = [new() { Name = "Stats" }],
    };

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _signal = new HistoricalImportSignal();
        var connectionString = _context.Database.GetConnectionString()!;
        var handler = new FakeSourcesHandler(request =>
        {
            var key = request.RequestUri!.Segments.Reverse().Skip(1).First().TrimEnd('/');
            return _exports.TryGetValue(key, out var xlsx) ? FakeSourcesHandler.Ok(xlsx, HistoricalSourceFetcher.XlsxContentType) : FakeSourcesHandler.Status(HttpStatusCode.NotFound);
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new GrifballContext(TestDatabase.Options(connectionString).Options));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<GrifballContext>());
        services.AddSingleton(_signal);
        services.AddSingleton(new XuidLookupSignal());
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Substitute.For<IDataPullService>());
        services.AddScoped<ChampionshipService>();
        services.AddScoped<GamertagMatching>();
        services.AddScoped<IHistoricalImportService>(provider => new HistoricalImportService(provider.GetRequiredService<GrifballContext>(), provider.GetRequiredService<IDataPullService>(),
            Options.Create(new HistoricalImportOptions()), NullLogger<HistoricalImportService>.Instance,
            provider.GetRequiredService<GamertagMatching>(), TimeProvider.System));
        services.AddScoped(provider => new HistoricalSourceFetcher(new HttpClient(handler)));
        services.AddScoped<HistoricalSourceService>();
        services.AddScoped<HistoricalImportJobs>();
        services.AddScoped<HistoricalImportRunner>();
        services.AddSingleton(Options.Create(new HistoricalGuardrailOptions()));
        services.AddSingleton(Options.Create(new HistoricalImportWorkerOptions { PollInterval = TimeSpan.FromMilliseconds(50) }));
        _services = services.BuildServiceProvider();

        // Only the test's sources are downloaded
        await Jobs(async (_, sources) => await sources.EnsureCatalog());
        await _context.HistoricalSources.ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false));
        _context.HistoricalSources.AddRange(Source("test-a", "A", SeasonA), Source("test-b", "B", SeasonB));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private static HistoricalSource Source(string id, string sheetKey, HistoricalSourceConfig? config)
        => HistoricalTestData.Source(id, $"https://docs.google.com/spreadsheets/d/{sheetKey}/edit", config?.ToJson(), sheetKey: sheetKey);

    private static byte[] Stats(params (string Gamertag, string? Team, int Kills)[] players) => Workbook(("Stats", StatsTab(players)));

    private static readonly (string, string?, int)[] FivePlayers = [("A", "Tuna", 1), ("B", "Tuna", 1), ("C", "Tuna", 1), ("D", "Bandits", 1), ("E", "Bandits", 1)];

    private async Task<T> Jobs<T>(Func<HistoricalImportJobs, HistoricalSourceService, Task<T>> use)
    {
        await using var scope = _services.CreateAsyncScope();
        return await use(scope.ServiceProvider.GetRequiredService<HistoricalImportJobs>(), scope.ServiceProvider.GetRequiredService<HistoricalSourceService>());
    }

    private Task<bool> Jobs(Func<HistoricalImportJobs, HistoricalSourceService, Task> use) => Jobs<bool>(async (jobs, sources) => { await use(jobs, sources); return true; });

    private HistoricalImportWorker Worker() => new(_services.GetRequiredService<IServiceScopeFactory>(), _signal, _services.GetRequiredService<IOptions<HistoricalImportWorkerOptions>>(),
        _time, NullLogger<HistoricalImportWorker>.Instance, new XuidLookupSignal());

    /// <summary>A run of the scheduled refresh (HistoricalRefreshJob's, without Quartz): the refresh job it queued, or null when it waited.</summary>
    private async Task<int?> RunScheduled()
    {
        await using var scope = _services.CreateAsyncScope();
        return await new HistoricalRefreshJob(scope.ServiceProvider.GetRequiredService<HistoricalImportJobs>(), scope.ServiceProvider.GetRequiredService<GrifballContext>(),
            _time, NullLogger<HistoricalRefreshJob>.Instance).RunAsync(CancellationToken.None);
    }

    /// <summary>Runs every queued job.</summary>
    private async Task RunJobs()
    {
        var worker = Worker();
        while (await worker.RunNextAsync(CancellationToken.None))
        {
        }
    }

    private Task<int> Enqueue(HistoricalImportJob job) => Jobs((jobs, _) => jobs.Enqueue(job));

    private Task<HistoricalImportJobDto> Job(int jobId) => Jobs(async (jobs, _) => (await jobs.Get(jobId))!);

    private async Task<int> StatRows(string seasonName)
        => await _context.PlayerSeasonStats.AsNoTracking().CountAsync(s => s.Season.SeasonName == seasonName);

    /// <summary>Downloads the sources and imports them, unguarded.</summary>
    private async Task RefreshAndImport()
    {
        await Enqueue(HistoricalImportJob.Refresh());
        await Enqueue(HistoricalImportJob.ImportSnapshots());
        await RunJobs();
    }

    [Test]
    public async Task APreview_ChangesNothing_AndApplyingItImportsWhatWasPreviewed()
    {
        _exports["A"] = Stats(FivePlayers);
        await RefreshAndImport();
        Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(5));

        _exports["A"] = Stats(("A", "Tuna", 9), ("B", "Tuna", 9));
        await Enqueue(HistoricalImportJob.Refresh());
        var previewId = await Enqueue(HistoricalImportJob.Preview());
        await RunJobs();
        var preview = await Job(previewId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((preview.Status, preview.Kind), Is.EqualTo(("Succeeded", "Preview")));
            Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(5), "A dry run");
            var review = preview.Reviews.Single();
            Assert.That((review.SeasonName, review.Status, review.StatsBefore, review.StatsRemoved, review.TeamsBefore, review.TeamsRemoved), Is.EqualTo(((string?)"Winter League 2019", "Preview", 5, 3, 2, 1)));
            Assert.That(preview.Report!.SeasonRemovals.Single(r => r.SeasonKey == "test-a/Stats").Stopped, Is.False);
        }

        // Changed again after the preview: applying it imports what was previewed
        _exports["A"] = Stats(("A", "Tuna", 9));
        await Enqueue(HistoricalImportJob.Refresh());
        await RunJobs();
        var (decision, applyId) = await Jobs((jobs, _) => jobs.ApplyPreview(previewId));
        await RunJobs();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Done));
            Assert.That((await Job(applyId!.Value)).Status, Is.EqualTo("Succeeded"));
            Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(2));
            Assert.That((await Jobs((jobs, _) => jobs.ApplyPreview(applyId.Value))).Decision, Is.EqualTo(HistoricalJobDecision.Conflict), "Only a preview is applied");
            Assert.That((await Jobs((jobs, _) => jobs.ApplyPreview(999))).Decision, Is.EqualTo(HistoricalJobDecision.NotFound));
        }
    }

    [Test]
    public async Task TheGuardrail_StopsALargeRemoval_AndApprovingItAppliesIt()
    {
        _exports["A"] = Stats(FivePlayers);
        _exports["B"] = Stats(FivePlayers);
        await RefreshAndImport();

        // A loses most of its rows, B one in five (not more than the threshold)
        _exports["A"] = Stats(("A", "Tuna", 9), ("B", "Tuna", 9));
        _exports["B"] = Stats(FivePlayers[..4]);
        await Enqueue(HistoricalImportJob.Refresh());
        var guardedId = await Enqueue(HistoricalImportJob.Seed(guarded: true));
        await RunJobs();

        var guarded = await Job(guardedId);
        var list = await Jobs((jobs, _) => jobs.List(10));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(5), "Stopped: left as it was");
            Assert.That(await StatRows("Spring League 2019"), Is.EqualTo(4), "Within the threshold: imported");
            var review = guarded.Reviews.Single();
            Assert.That((review.SeasonKey, review.Status, review.StatsRemoved, review.StatsBefore), Is.EqualTo(("test-a/Stats", "NeedsReview", 3, 5)));
            Assert.That(guarded.Report!.Warnings, Has.Some.StartsWith("test-a/Stats: stopped for review"));
            Assert.That(list.First(j => j.JobID == guardedId).NeedsReview, Is.EqualTo(1));
        }

        var (decision, approvalId) = await Jobs((jobs, _) => jobs.Decide(guarded.Reviews[0].ReviewID, approve: true));
        await RunJobs();

        var decided = (await Job(guardedId)).Reviews.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Done));
            Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(2), "Approved: applied");
            Assert.That((decided.Status, decided.ApprovalJobID, decided.DecidedAt.HasValue), Is.EqualTo(("Approved", approvalId, true)));
            Assert.That((await Jobs((jobs, _) => jobs.Decide(decided.ReviewID, approve: false))).Decision, Is.EqualTo(HistoricalJobDecision.Conflict), "Decided once");
            Assert.That((await Jobs((jobs, _) => jobs.Decide(999, approve: true))).Decision, Is.EqualTo(HistoricalJobDecision.NotFound));
        }
    }

    [Test]
    public async Task ASkippedSeason_IsLeftAsItWas()
    {
        _exports["A"] = Stats(FivePlayers);
        await RefreshAndImport();
        _exports["A"] = Stats(("A", "Tuna", 9));
        await Enqueue(HistoricalImportJob.Refresh());
        var guardedId = await Enqueue(HistoricalImportJob.Seed(guarded: true));
        await RunJobs();

        var review = (await Job(guardedId)).Reviews.Single();
        var (decision, jobId) = await Jobs((jobs, _) => jobs.Decide(review.ReviewID, approve: false));
        await RunJobs();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((decision, jobId), Is.EqualTo((HistoricalJobDecision.Done, (int?)null)));
            Assert.That((await Job(guardedId)).Reviews.Single().Status, Is.EqualTo("Skipped"));
            Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(5));
        }
    }

    [Test]
    public async Task TheGuardrail_StopsAnyRemovedTitle()
    {
        var champions = new HistoricalSourceConfig { ChampionsSheet = "AGLA Champions" };
        _context.HistoricalSources.Add(Source("test-champions", "C", champions));
        await _context.SaveChangesAsync();
        _exports["A"] = Stats(FivePlayers);
        _exports["C"] = Workbook(("AGLA Champions", [Row("x"), Row("y"), Row("Winter League 2019", "MCC Halo 3", "-", "Tuna", "A", "B")]));
        await RefreshAndImport();
        Assert.That(await _context.Set<PlayerChampionship>().CountAsync(), Is.EqualTo(2));

        _exports["C"] = Workbook(("AGLA Champions", [Row("x"), Row("y"), Row("Winter League 2019", "MCC Halo 3", "-", "Tuna", "A")]));
        await Enqueue(HistoricalImportJob.Refresh());
        var guardedId = await Enqueue(HistoricalImportJob.Seed(guarded: true));
        await RunJobs();

        var review = (await Job(guardedId)).Reviews.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((review.TitlesBefore, review.TitlesRemoved, review.StatsRemoved), Is.EqualTo((2, 1, 0)));
            Assert.That(await _context.Set<PlayerChampionship>().CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task TheSchedule_ImportsOnlyTheChangedSourcesSeasons_AndNothingWhenNothingChanged()
    {
        _exports["A"] = Stats(FivePlayers);
        _exports["B"] = Stats(FivePlayers);

        // First run: everything is new
        var firstId = await RunScheduled();
        await RunJobs();
        var first = await Job(firstId!.Value);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((first.Kind, first.Trigger), Is.EqualTo(("Refresh", "Scheduled")));
            Assert.That(first.Report!.SourcesChanged, Is.EquivalentTo(SourcesAAndB));
            Assert.That(first.Report.ImportJobID, Is.Not.Null);
            var import = await Job(first.Report.ImportJobID!.Value);
            Assert.That((import.Kind, import.Trigger, import.Guarded, import.Status), Is.EqualTo(("Import", "Scheduled", true, "Succeeded")));
            var row = await _context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == first.Report.ImportJobID);
            Assert.That((row.StartedAt.HasValue, row.SeasonsDone, row.Error), Is.EqualTo((true, row.SeasonsTotal, (string?)null)), "Its progress and outcome are stored");
            Assert.That(row.Report, Does.Contain("seasonsCreated").IgnoreCase);
            var snapshot = await _context.Set<HistoricalSourceSnapshot>().AsNoTracking().Include(x => x.Source).FirstAsync(x => x.HistoricalSourceID == "test-a");
            Assert.That(snapshot.Source.HistoricalSourceID, Is.EqualTo("test-a"));
            Assert.That(await StatRows("Winter League 2019"), Is.EqualTo(5));
            Assert.That(await StatRows("Spring League 2019"), Is.EqualTo(5));
        }

        // A's season drifts in the database; only B's source changes: A isn't imported again
        await _context.PlayerSeasonStats.Where(s => s.Season.SeasonName == "Winter League 2019").ExecuteUpdateAsync(s => s.SetProperty(x => x.Kills, 77));
        _exports["B"] = Stats(("A", "Tuna", 9), ("B", "Tuna", 9), ("C", "Tuna", 9), ("D", "Bandits", 9), ("E", "Bandits", 9));
        _time.Advance(TimeSpan.FromDays(1));
        var secondId = await RunScheduled();
        await RunJobs();
        var second = await Job(secondId!.Value);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Report!.SourcesChanged, Is.EqualTo(SourceB));
            Assert.That(await _context.PlayerSeasonStats.Where(s => s.Season.SeasonName == "Winter League 2019").Select(s => s.Kills).Distinct().ToListAsync(), Is.EqualTo(DriftedKills));
            Assert.That(await _context.PlayerSeasonStats.Where(s => s.Season.SeasonName == "Spring League 2019").Select(s => s.Kills).Distinct().ToListAsync(), Is.EqualTo(ReimportedKills));
        }

        // Nothing changed: no import
        _time.Advance(TimeSpan.FromDays(1));
        var thirdId = await RunScheduled();
        await RunJobs();
        var third = await Job(thirdId!.Value);
        var settings = await _context.HistoricalImportSettings.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((third.Report!.SourcesChanged.Count, third.Report.SourcesUnchanged, third.Report.ImportJobID), Is.EqualTo((0, 2, (int?)null)));
            Assert.That(await _context.HistoricalImportJobs.CountAsync(j => j.Kind == HistoricalImportJobKind.Import), Is.EqualTo(2));
            Assert.That((settings.LastScheduledRunAt, settings.LastScheduledJobID, settings.LastScheduledRunNote),
                Is.EqualTo(((DateTime?)_time.GetUtcNow().UtcDateTime, (int?)thirdId, (string?)null)));
        }
    }

    [Test]
    public async Task TheSchedule_QueuesNoImport_WhenOnlyASourceTheImportDoesntReadChanged()
    {
        // A page the import never reads (no config), e.g. the YouTube channel page, which differs on every download
        await _context.HistoricalSources.Where(x => x.HistoricalSourceID != "test-c").ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        var page = Source("test-c", "C", null);
        _context.HistoricalSources.Add(page);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        _exports["C"] = Stats(FivePlayers);

        var refreshId = await RunScheduled();
        await RunJobs();

        var refresh = await Job(refreshId!.Value);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(refresh.Report!.SourcesChanged, Is.EqualTo(SourceC));
            Assert.That(refresh.Report.ImportJobID, Is.Null);
        }
    }
}
