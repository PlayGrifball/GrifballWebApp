using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text;

namespace GrifballWebApp.Test;

/// <summary>The upload queues a job (202) that the worker runs in the background; the job endpoint reports on it.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportControllerTests
{
    private GrifballContext _context;
    private HistoricalImportSignal _signal;
    private HistoricalImportController _controller;
    private IHistoricalImportService _import;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _signal = new HistoricalImportSignal();
        _controller = new HistoricalImportController(new HistoricalImportJobs(_context, _signal), new GamertagClaimService(_context, new UserMergeService(_context)));
        _import = Substitute.For<IHistoricalImportService>();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static IFormFile File(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "file.json");
    }

    private HistoricalImportWorker Worker(TimeProvider? time = null)
    {
        // Its own contexts, from the connection string read once: the test uses _context meanwhile
        var connectionString = _context.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddScoped(_ => new GrifballContext(TestDatabase.Options(connectionString).Options));
        services.AddScoped(_ => _import);
        return new HistoricalImportWorker(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _signal,
            Options.Create(new HistoricalImportWorkerOptions { PollInterval = TimeSpan.FromMilliseconds(50) }), time ?? TimeProvider.System,
            NullLogger<HistoricalImportWorker>.Instance);
    }

    [Test]
    public async Task Import_QueuesTheUploadedArchive_AndTheWorkerRunsIt()
    {
        HistoricalArchiveInput? passed = null;
        _import.Import(Arg.Do<HistoricalArchiveInput>(i => passed = i), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await call.Arg<Func<HistoricalImportProgress, Task>>()(new HistoricalImportProgress("Importing S 2019", 0, 1));
                return new HistoricalImportReport { SeasonsCreated = 1, Warnings = ["look at this"] };
            });

        var result = await _controller.Import(
            File("""{ "seasons": [ { "seasonName": "S 2019", "gameVersion": "halo3", "league": "AGLA", "players": [ { "gamertag": "a", "kills": "3" } ] } ] }"""),
            File("""{ "champions": [ { "season": "S 2019", "league": "Pro", "team": "T", "roster": ["a"] } ] }"""),
            File("""{ "s/Stats": { "regular": ["x"] } }"""),
            File("""{ "sources": [ { "id": "w", "url": "https://example.org/w" } ] }"""),
            CancellationToken.None);

        var accepted = (AcceptedAtActionResult)result;
        var jobId = ((HistoricalImportQueued)accepted.Value!).JobID;
        Assert.That(accepted.ActionName, Is.EqualTo(nameof(HistoricalImportController.Job)));
        var queued = (await _controller.Job(jobId, CancellationToken.None)).Value!;
        Assert.That((queued.Status, queued.Report), Is.EqualTo(("Queued", (HistoricalImportReport?)null)));

        Assert.That(await Worker().RunNextAsync(CancellationToken.None), Is.True);

        var done = (await _controller.Job(jobId, CancellationToken.None)).Value!;
        Assert.Multiple(async () =>
        {
            Assert.That((done.Status, done.Stage, done.SeasonsTotal), Is.EqualTo(("Succeeded", "Done", 1)));
            Assert.That(done.Report!.SeasonsCreated, Is.EqualTo(1));
            Assert.That(done.Report.Warnings, Is.EqualTo(new[] { "look at this" }));
            Assert.That((done.StartedAt, done.FinishedAt), Is.Not.EqualTo(((DateTime?)null, (DateTime?)null)));
            Assert.That(passed!.Seasons.Seasons.Single().Players.Single().Kills, Is.EqualTo(3));
            Assert.That(passed.Championships!.Champions.Single().Tier, Is.EqualTo("Pro"));
            Assert.That(passed.WaypointMatches!["s/Stats"].Regular, Is.EqualTo(new[] { "x" }));
            Assert.That(passed.Catalog!.Sources.Single().Url, Is.EqualTo("https://example.org/w"));
            Assert.That(await _context.HistoricalImportJobs.Select(j => j.Payload).SingleAsync(), Is.Null, "The archive isn't kept once the job ends");
        });
        Assert.That(await Worker().RunNextAsync(CancellationToken.None), Is.False, "Nothing queued");
    }

    [Test]
    public async Task AJobThatThrows_IsFailed_WithTheReason()
    {
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns<HistoricalImportReport>(_ => throw new InvalidOperationException("boom"));
        var jobId = ((HistoricalImportQueued)((AcceptedAtActionResult)await _controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None)).Value!).JobID;

        await Worker().RunNextAsync(CancellationToken.None);

        var job = (await _controller.Job(jobId, CancellationToken.None)).Value!;
        Assert.That((job.Status, job.Error, job.Stage), Is.EqualTo(("Failed", "boom", "Failed")));
    }

    [Test]
    public async Task AJobLeftRunningByAStoppedProcess_IsQueuedAgain_OnceItsHeartbeatIsStale()
    {
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>()).Returns(new HistoricalImportReport());
        var payload = System.Text.Json.JsonSerializer.Serialize(new HistoricalArchiveInput { Seasons = new UnifiedArchive() });
        var now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        _context.HistoricalImportJobs.AddRange(
            new HistoricalImportJob { Status = HistoricalImportJobStatus.Running, Payload = payload, HeartbeatAt = now.AddHours(-1) },
            new HistoricalImportJob { Status = HistoricalImportJobStatus.Running, Payload = payload, HeartbeatAt = now.AddMinutes(-1) });
        await _context.SaveChangesAsync();

        await Worker(new FixedTimeProvider(now)).RunNextAsync(CancellationToken.None);

        var jobs = await _context.HistoricalImportJobs.AsNoTracking().OrderBy(j => j.HistoricalImportJobID).Select(j => j.Status).ToListAsync();
        Assert.That(jobs, Is.EqualTo(new[] { HistoricalImportJobStatus.Succeeded, HistoricalImportJobStatus.Running }), "The fresh one is still someone's");
    }

    [Test]
    public async Task TheWorker_RunsAQueuedJob_WhenWoken_AndStopsWithTheHost()
    {
        var ran = new TaskCompletionSource();
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { ran.TrySetResult(); return new HistoricalImportReport(); });
        var worker = Worker();
        await worker.StartAsync(CancellationToken.None);

        await _controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None);

        await ran.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Job_AnUnknownJob_IsNotFound()
        => Assert.That((await _controller.Job(999, CancellationToken.None)).Result, Is.InstanceOf<NotFoundResult>());

    [TestCase("not json")]
    [TestCase("null")]
    public async Task Import_AFileThatIsNotTheArchive_IsABadRequest(string json)
    {
        var result = await _controller.Import(File(json), null, null, null, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(await _context.HistoricalImportJobs.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Claims_CanBeListedApprovedAndRejected_AndHistoryAdded()
    {
        var placeholder = new User { UserName = "historical:Old", IsDummyUser = true };
        _context.Users.Add(placeholder);
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 1, Gamertag = "A" }, new XboxUser { XboxUserID = 2, Gamertag = "B" });
        await _context.SaveChangesAsync();
        _context.GamertagClaims.AddRange(
            new GamertagClaim { Kind = GamertagClaimKind.SetGamertag, XboxUserID = 1, Gamertag = "Old", NormalizedGamertag = "OLD", PlaceholderUserID = placeholder.Id },
            new GamertagClaim { Kind = GamertagClaimKind.SetGamertag, XboxUserID = 2, Gamertag = "Other", NormalizedGamertag = "OTHER" });
        await _context.SaveChangesAsync();

        var pending = await _controller.Claims(ct: CancellationToken.None);
        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(await _controller.RejectClaim(pending[1].GamertagClaimID, CancellationToken.None), Is.InstanceOf<OkResult>());
        Assert.That(await _controller.ApproveClaim(pending[0].GamertagClaimID, CancellationToken.None), Is.InstanceOf<OkResult>());
        Assert.That(await _controller.GamertagHistory(new AddGamertagHistoryRequest { XboxUserID = 2, Gamertag = "Older" }, CancellationToken.None), Is.InstanceOf<OkResult>());

        Assert.That(await _controller.Claims(ct: CancellationToken.None), Is.Empty);
        Assert.That(await _controller.Claims(GamertagClaimStatus.Approved, CancellationToken.None), Has.Count.EqualTo(1));
        Assert.That(await _context.XboxGamertagHistory.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task TheSignal_WakesOneWaiter_AndDoesntPileUp()
    {
        var signal = new HistoricalImportSignal();
        signal.Notify();
        signal.Notify();
        Assert.That(await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None), Is.True);
        Assert.That(await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None), Is.False);
    }
}

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalKeysTests
{
    [TestCase("  Shadow   S16 ", "Shadow S16")]
    [TestCase("x0 pie x0", "x0 pie x0")]
    public void CleanGamertag_TrimsAndCollapsesWhitespace(string raw, string clean)
    {
        Assert.That(HistoricalKeys.CleanGamertag(raw), Is.EqualTo(clean));
        Assert.That(HistoricalKeys.Normalize(raw), Is.EqualTo(clean.ToUpperInvariant()));
    }

    [TestCase("Summer League 2021 (Pro)", "summer-league-2021-pro")]
    [TestCase("Winter league 2020", "winter-league-2020")]
    [TestCase("Q2 Major Season 2026!", "q2-major-season-2026")]
    public void Slug(string text, string slug) => Assert.That(HistoricalKeys.Slug(text), Is.EqualTo(slug));

    [TestCase("Winter League 2019", 2019)]
    [TestCase("Original 8 Tounament", null)]
    public void Year(string name, int? year) => Assert.That(HistoricalKeys.Year(name), Is.EqualTo(year));

    [TestCase("MCC Halo 3", "halo3")]
    [TestCase("Halo Reach", "reach")]
    [TestCase("MCC Halo Reach: Dash", "reach-dash")]
    [TestCase("MCC Halo 4", "halo4")]
    [TestCase("Halo 5", "halo5")]
    [TestCase("Infinite", "infinite")]
    [TestCase("MCC Split", null)]
    [TestCase(null, null)]
    [TestCase(" ", null)]
    public void GameVersionSlug(string? game, string? slug) => Assert.That(HistoricalKeys.GameVersionSlug(game), Is.EqualTo(slug));

    [Test]
    public void MatchId_FromAnIdOrAWaypointUrl()
    {
        var id = Guid.NewGuid();
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalKeys.MatchId(id.ToString()), Is.EqualTo(id));
            Assert.That(HistoricalKeys.MatchId($"https://www.halowaypoint.com/halo-infinite/players/ill%20shrek%20u/matches/{id}"), Is.EqualTo(id));
            Assert.That(HistoricalKeys.MatchId($"https://www.halowaypoint.com/halo-infinite/players/a/matches/{id}/?x=1"), Is.EqualTo(id));
            Assert.That(HistoricalKeys.MatchId("https://www.halowaypoint.com/halo-infinite/players/a"), Is.Null);
        });
    }

    [Test]
    public void SeasonSourceId_IsTheWorkbookAndSheet()
    {
        Assert.That(HistoricalKeys.SeasonSourceId("2018-sml18-fantasy-rey", "Total Stats "), Is.EqualTo("2018-sml18-fantasy-rey/Total Stats"));
        Assert.That(HistoricalKeys.FallbackSeasonSourceId("agla", "Winter League 2019"), Is.EqualTo("agla/winter-league-2019"));
    }

    /// <summary>compiled/waypoint-matches.json (archive-tools/extract-waypoint-matches.mjs) is keyed by Halo Infinite
    /// seasons' HistoricalSourceIDs and holds match IDs only, so the import uses all of it.</summary>
    [Test]
    public async Task TheCompiledWaypointMatches_KeyHaloInfiniteSeasons_AndAreMatchIds()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "data", "historical-archive")))
            directory = directory.Parent;
        var compiled = Path.Combine(directory!.FullName, "data", "historical-archive", "compiled");
        await using var seasonsFile = File.OpenRead(Path.Combine(compiled, "unified-historical-seasons.json"));
        await using var matchesFile = File.OpenRead(Path.Combine(compiled, "waypoint-matches.json"));
        var seasons = await HistoricalArchiveInput.ReadAsync<UnifiedArchive>(seasonsFile);
        var matches = await HistoricalArchiveInput.ReadAsync<Dictionary<string, WaypointSeasonMatches>>(matchesFile);

        var infinite = seasons.Seasons.Where(s => s.GameVersion == "infinite" && !s.IsPlayoffs)
            .Select(s => HistoricalKeys.SeasonSourceId(s.SourceWorkbook!, s.SourceSheet!)).ToHashSet();
        var ids = matches.Values.SelectMany(m => m.Regular.Concat(m.Playoffs)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(matches.Keys, Is.SubsetOf(infinite));
            Assert.That(matches, Has.Count.GreaterThanOrEqualTo(6));
            Assert.That(ids.Select(HistoricalKeys.MatchId), Has.None.Null);
            Assert.That(ids, Is.Unique, "No match in two seasons, or twice in one");
        });
    }

    [Test]
    public void DummyUserName_HasThePrefix() => Assert.That(HistoricalKeys.DummyUserName(" Old  Tag"), Is.EqualTo("historical:Old Tag"));
}
