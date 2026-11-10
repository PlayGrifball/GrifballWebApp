using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Infrastructure.Historical;

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
    private GamertagClaimsController _claims = null!;
    private XuidLookupSignal _lookupSignal = null!;
    private IHistoricalImportService _import;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _signal = new HistoricalImportSignal();
        _controller = new HistoricalImportController(new HistoricalImportJobs(_context, _signal),
            new GrifballWebApp.Infrastructure.Historical.Sources.HistoricalSourceService(_context,
                new GrifballWebApp.Infrastructure.Historical.Sources.HistoricalSourceFetcher(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(System.Net.HttpStatusCode.NotFound)))),
                TimeProvider.System, NullLogger<GrifballWebApp.Infrastructure.Historical.Sources.HistoricalSourceService>.Instance));
        _lookupSignal = new XuidLookupSignal();
        _claims = new GamertagClaimsController(new GamertagClaimService(_context, new UserMergeService(_context)),
            new XuidLookupService(_context, Substitute.For<Surprenant.Grunt.Core.IHaloInfiniteClient>(), new GamertagMatching(_context),
                Options.Create(new XuidLookupOptions()), TimeProvider.System),
            _lookupSignal);
        _import = Substitute.For<IHistoricalImportService>();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static IFormFile File(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "file.json");
    }

    private HistoricalImportWorker Worker(TimeProvider? time = null, Microsoft.Extensions.Logging.ILogger<HistoricalImportWorker>? logger = null)
    {
        // Its own contexts, from the connection string read once: the test uses _context meanwhile
        var connectionString = _context.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddScoped(_ => new GrifballContext(TestDatabase.Options(connectionString).Options));
        services.AddScoped(_ => _import);
        return new HistoricalImportWorker(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _signal,
            Options.Create(new HistoricalImportWorkerOptions { PollInterval = TimeSpan.FromMilliseconds(50) }), time ?? TimeProvider.System,
            logger ?? NullLogger<HistoricalImportWorker>.Instance, new XuidLookupSignal());
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
    public async Task FinishedJobs_AreDeletedAfterTheirRetention_AndAReportsListsAreCapped()
    {
        var now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        await _context.Finish(await _context.Queue(HistoricalImportJob.Seed()), succeeded: true, now.AddDays(-40));
        await _context.Finish(await _context.Queue(HistoricalImportJob.Seed()), succeeded: false, now.AddDays(-2));

        Assert.That(await Worker(new FixedTimeProvider(now)).RunNextAsync(CancellationToken.None), Is.False);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.HistoricalImportJobs.Select(j => j.Status).ToListAsync(), Is.EqualTo(new[] { HistoricalImportJobStatus.Failed }));
        var report = new HistoricalImportReport { Warnings = [.. Enumerable.Range(0, 7).Select(i => $"w{i}")], Skipped = ["one"] };
        HistoricalImportWorker.Cap(report, 5);
        Assert.That(report.Warnings, Is.EqualTo(new[] { "w0", "w1", "w2", "w3", "w4", "... and 2 more" }));
        Assert.That(report.Skipped, Is.EqualTo(new[] { "one" }));
    }

    [Test]
    public async Task AJobLeftRunningByAStoppedProcess_IsQueuedAgain_OnceItsHeartbeatIsStale()
    {
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>()).Returns(new HistoricalImportReport());
        var payload = System.Text.Json.JsonSerializer.Serialize(new HistoricalArchiveInput { Seasons = new UnifiedArchive() });
        var now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        await _context.Start(await _context.Queue(HistoricalImportJob.Upload(payload)), now.AddHours(-1));
        await _context.Start(await _context.Queue(HistoricalImportJob.Upload(payload)), now.AddMinutes(-1));

        await Worker(new FixedTimeProvider(now)).RunNextAsync(CancellationToken.None);

        var jobs = await _context.HistoricalImportJobs.AsNoTracking().OrderBy(j => j.HistoricalImportJobID).Select(j => j.Status).ToListAsync();
        Assert.That(jobs, Is.EqualTo(new[] { HistoricalImportJobStatus.Succeeded, HistoricalImportJobStatus.Running }), "The fresh one is still someone's");
    }

    [Test]
    public async Task ALiveJobsHeartbeat_IsBeatenOnATimer_WithoutProgress_SoAnotherWorkerLeavesItAlone()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // A long stage: no progress reported at all
                started.SetResult();
                await finish.Task;
                return new HistoricalImportReport();
            });
        var jobId = ((HistoricalImportQueued)((AcceptedAtActionResult)await _controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None)).Value!).JobID;
        var start = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(start);

        var running = Worker(time).RunNextAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // 40 minutes (StaleAfter is 30) in steps of a third of it: a beat each
        for (var step = 1; step <= 4; step++)
        {
            time.Advance(TimeSpan.FromMinutes(10));
            var expected = start.UtcDateTime.AddMinutes(10 * step);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            DateTime? heartbeat;
            do
            {
                await Task.Delay(20);
                heartbeat = await _context.HistoricalImportJobs.AsNoTracking().Where(j => j.HistoricalImportJobID == jobId).Select(j => j.HeartbeatAt).SingleAsync();
            }
            while (heartbeat != expected && DateTime.UtcNow < deadline);
            Assert.That(heartbeat, Is.EqualTo(expected), $"beat {step}");
        }

        // Another worker polling now finds the job alive: nothing queued again, nothing to run
        Assert.That(await Worker(time).RunNextAsync(CancellationToken.None), Is.False);

        finish.SetResult();
        Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(30)), Is.True);
        var job = (await _controller.Job(jobId, CancellationToken.None)).Value!;
        Assert.That(job.Status, Is.EqualTo("Succeeded"));
        await _import.Received(1).Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AHeartbeatThatCantBeWritten_IsLogged_AndTheJobGoesOn()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                started.SetResult();
                await finish.Task;
                return new HistoricalImportReport();
            });
        var jobId = ((HistoricalImportQueued)((AcceptedAtActionResult)await _controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None)).Value!).JobID;
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        // The database out of reach while the flag is set
        var unreachable = false;
        var connectionString = _context.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddScoped(_ => unreachable ? throw new InvalidOperationException("The database is down") : new GrifballContext(TestDatabase.Options(connectionString).Options));
        services.AddScoped(_ => _import);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<HistoricalImportWorker>>();
        var worker = new HistoricalImportWorker(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _signal,
            Options.Create(new HistoricalImportWorkerOptions()), time, logger, new XuidLookupSignal());

        var running = worker.RunNextAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        unreachable = true;
        time.Advance(TimeSpan.FromMinutes(10));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        bool Warned() => logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(Microsoft.Extensions.Logging.ILogger.Log)
            && (Microsoft.Extensions.Logging.LogLevel)c.GetArguments()[0]! == Microsoft.Extensions.Logging.LogLevel.Warning);
        while (!Warned() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.That(Warned(), Is.True, "The missed heartbeat is logged");

        unreachable = false;
        finish.SetResult();
        Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(30)), Is.True);
        Assert.That((await _controller.Job(jobId, CancellationToken.None)).Value!.Status, Is.EqualTo("Succeeded"));
    }

    /// <summary>What the worker logged at <paramref name="level"/>, as text.</summary>
    private static List<string> Logged(Microsoft.Extensions.Logging.ILogger<HistoricalImportWorker> logger, Microsoft.Extensions.Logging.LogLevel level)
        => logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(Microsoft.Extensions.Logging.ILogger.Log)
                && (Microsoft.Extensions.Logging.LogLevel)c.GetArguments()[0]! == level)
            .Select(c => c.GetArguments()[2]?.ToString() ?? "").ToList();

    /// <summary>Another replica queues the job again as stale and claims it at <paramref name="at"/>.</summary>
    private async Task ClaimedByAnotherReplica(int jobId, DateTime at)
    {
        Assert.That(await HistoricalImportJob.RequeueStaleAsync(_context.HistoricalImportJobs, at), Is.EqualTo(1));
        Assert.That(await HistoricalImportJob.ClaimAsync(_context.HistoricalImportJobs, jobId, at), Is.Not.Null);
    }

    [Test]
    public async Task ARunWhoseJobWasClaimedAgain_HasItsReportDropped_AndSaysSo()
    {
        var now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        var jobId = ((HistoricalImportQueued)((AcceptedAtActionResult)await _controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None)).Value!).JobID;
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // Its heartbeat went stale meanwhile: another replica has the job now
                await ClaimedByAnotherReplica(jobId, now.AddHours(1));
                return new HistoricalImportReport { SeasonsCreated = 1 };
            });
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<HistoricalImportWorker>>();

        Assert.That(await Worker(new FixedTimeProvider(now), logger).RunNextAsync(CancellationToken.None), Is.True);

        var row = await _context.HistoricalImportJobs.AsNoTracking().SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That((row.Status, row.StartedAt, row.Report, row.FinishedAt), Is.EqualTo((HistoricalImportJobStatus.Running, (DateTime?)now.AddHours(1), (string?)null, (DateTime?)null)),
                "The other replica's run is left alone");
            Assert.That(Logged(logger, Microsoft.Extensions.Logging.LogLevel.Warning), Has.Some.Contains($"job {jobId} was queued again under this run; its report is dropped"));
            Assert.That(Logged(logger, Microsoft.Extensions.Logging.LogLevel.Information), Has.None.Contains("finished"));
        });
    }

    [Test]
    public async Task ARunWhoseBeatIsRefused_Stops_WithoutEndingTheJob()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _import.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                }
                finally
                {
                    stopped.SetResult();
                }
                return new HistoricalImportReport();
            });
        var jobId = ((HistoricalImportQueued)((AcceptedAtActionResult)await _controller.Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None)).Value!).JobID;
        var start = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(start);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<HistoricalImportWorker>>();

        var running = Worker(time, logger).RunNextAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await ClaimedByAnotherReplica(jobId, start.UtcDateTime.AddHours(1));
        // The next beat, a third of StaleAfter on
        time.Advance(TimeSpan.FromMinutes(10));

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(30)), Is.True);
        var row = await _context.HistoricalImportJobs.AsNoTracking().SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That((row.Status, row.StartedAt, row.HeartbeatAt, row.Error), Is.EqualTo((HistoricalImportJobStatus.Running, (DateTime?)start.UtcDateTime.AddHours(1), (DateTime?)start.UtcDateTime.AddHours(1), (string?)null)),
                "The other replica's run is left alone: not beaten, not ended");
            Assert.That(Logged(logger, Microsoft.Extensions.Logging.LogLevel.Warning), Has.Some.Contains($"job {jobId} was queued again under this run; the run stops"));
            Assert.That(Logged(logger, Microsoft.Extensions.Logging.LogLevel.Error), Is.Empty, "Stopping isn't a failure");
        });
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
        var placeholder = User.Seed(userName: "historical:Old", isDummyUser: true);
        _context.Users.Add(placeholder);
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "A"), XboxUser.FromXbox(2, "B"));
        await _context.SaveChangesAsync();
        _context.GamertagClaims.AddRange(
            GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 1, "Old", "OLD", placeholder.Id),
            GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 2, "Other", "OTHER", placeholder.Id));
        await _context.SaveChangesAsync();

        var pending = await _claims.GetClaims(ct: CancellationToken.None);
        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(await _claims.RejectClaim(pending[1].GamertagClaimID, null, CancellationToken.None), Is.InstanceOf<NoContentResult>());
        Assert.That(await _claims.ApproveClaim(pending[0].GamertagClaimID, CancellationToken.None), Is.InstanceOf<NoContentResult>());
        Assert.That(await _claims.AddGamertagHistory(new AddGamertagHistoryRequest { XboxUserID = 2, Gamertag = "Older" }, CancellationToken.None), Is.InstanceOf<NoContentResult>());

        Assert.That(await _claims.GetClaims(ct: CancellationToken.None), Is.Empty);
        Assert.That(await _claims.GetClaims(GamertagClaimStatus.Approved, CancellationToken.None), Has.Count.EqualTo(1));
        Assert.That(await _context.Set<XboxGamertagHistory>().CountAsync(), Is.EqualTo(2));
    }


    [TestCase(nameof(HistoricalImportController.Import))]
    [TestCase(nameof(HistoricalImportController.Job))]
    [TestCase(nameof(HistoricalImportController.Sources))]
    [TestCase(nameof(HistoricalImportController.SetSourceEnabled))]
    [TestCase(nameof(HistoricalImportController.Refresh))]
    [TestCase(nameof(HistoricalImportController.Preview))]
    [TestCase(nameof(HistoricalImportController.ApplyPreview))]
    [TestCase(nameof(HistoricalImportController.ApproveReview))]
    [TestCase(nameof(HistoricalImportController.SkipReview))]
    [TestCase(nameof(HistoricalImportController.Jobs))]
    [TestCase(nameof(HistoricalImportController.Schedule))]
    [TestCase(nameof(HistoricalImportController.SetSchedulePaused))]
    public void EveryAction_IsSysadminOnly(string action)
    {
        var method = typeof(HistoricalImportController).GetMethod(action)!;
        var roles = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(a => a.Roles).ToList();
        Assert.That(roles, Is.EqualTo(new[] { "Sysadmin" }), "Approving a claim merges users, which only a sysadmin does");
    }

    [Test]
    public void TheClaimsController_IsSysadminOnly()
    {
        var roles = typeof(GamertagClaimsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(a => a.Roles).ToList();
        Assert.That(roles, Is.EqualTo(new[] { "Sysadmin" }), "Approving a claim merges users, which only a sysadmin does");
    }

    [Test]
    public async Task LookUpXuids_MakesTheNotFoundDueAgain_AndWakesTheWorker()
    {
        var placeholder = User.Seed(userName: "historical:Gone", normalizedUserName: "HISTORICAL:GONE", isDummyUser: true);
        _context.Users.Add(placeholder);
        await _context.SaveChangesAsync();
        _context.XuidLookups.Add(new XuidLookup { UserID = placeholder.Id, LookedUpAt = DateTime.UtcNow, Status = XuidLookupStatus.NotFound });
        await _context.SaveChangesAsync();
        Assert.That((await _claims.XuidLookupStatus(CancellationToken.None)).Due, Is.Zero);

        var result = await _claims.LookUpXuids(CancellationToken.None);

        var status = (XuidLookupStatusDto)((AcceptedResult)result.Result!).Value!;
        Assert.Multiple(async () =>
        {
            Assert.That((status.Placeholders, status.Due, status.NotFound), Is.EqualTo((1, 1, 0)));
            Assert.That(await _lookupSignal.WaitAsync(TimeSpan.Zero, CancellationToken.None), Is.True, "The worker is woken (in the worker role's pod)");
        });
    }

    [Test]
    public async Task PastGamertags_AreListedAndRemoved_ThroughTheController()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(77, "Now"));
        await _context.SaveChangesAsync();
        Assert.That((await _claims.PastGamertags(78, null, CancellationToken.None)).Result, Is.InstanceOf<NotFoundResult>());
        await _claims.AddGamertagHistory(new AddGamertagHistoryRequest { XboxUserID = 77, Gamertag = "Then", Note = "WL19" }, CancellationToken.None);

        var listed = (await _claims.PastGamertags(77, null, CancellationToken.None)).Value!;
        Assert.That(listed.PastGamertags.Select(p => (p.Gamertag, p.Note)), Is.EqualTo(new[] { ("Then", (string?)"WL19") }));

        Assert.That(await _claims.RemoveGamertagHistory(listed.PastGamertags[0].XboxGamertagHistoryID, CancellationToken.None), Is.InstanceOf<NoContentResult>());
        Assert.That(await _context.Set<XboxGamertagHistory>().AnyAsync(), Is.False);
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

    /// <summary>The compiled Waypoint matches (a few seasons of the real archive, Fixtures/HistoricalArchive) are keyed
    /// by Halo Infinite seasons' HistoricalSourceIDs and hold match IDs only, so the import uses all of it.</summary>
    [Test]
    public async Task TheCompiledWaypointMatches_KeyHaloInfiniteSeasons_AndAreMatchIds()
    {
        var compiled = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "HistoricalArchive");
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
            Assert.That(matches, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(ids.Select(HistoricalKeys.MatchId), Has.None.Null);
            Assert.That(ids, Is.Unique, "No match in two seasons, or twice in one");
        });
    }

    [Test]
    public void DummyUserName_HasThePrefix() => Assert.That(HistoricalKeys.DummyUserName(" Old  Tag"), Is.EqualTo("historical:Old Tag"));
}
