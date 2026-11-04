using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Historical.Sources;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Data.Common;
using System.Net;

namespace GrifballWebApp.Test;

/// <summary>
/// The alias consolidation as a step of the historical import worker's jobs: after an import and a refresh, never a
/// preview; its merges listed with the warnings, its failures reported without failing the job, a cancellation not
/// swallowed.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportRunnerConsolidationTests
{
    private GrifballContext _context = null!;

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task SetUp(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        _context = await SetUpFixture.NewGrifballContext(interceptors);
        _context.Users.AddRange(Placeholder("MenZ"), Placeholder("MenztrualCramps"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private static User Placeholder(string gamertag) => new()
    {
        UserName = HistoricalKeys.DummyUserName(gamertag),
        NormalizedUserName = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant(),
        DisplayName = gamertag,
        IsDummyUser = true,
    };

    private async Task<(HistoricalImportRunner Runner, HistoricalImportJobs Jobs)> Runner(IUserMergeService? merges)
    {
        var sources = new HistoricalSourceService(_context, new HistoricalSourceFetcher(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound)))),
            TimeProvider.System, NullLogger<HistoricalSourceService>.Instance);
        await sources.EnsureCatalog();
        // Nothing is downloaded
        await _context.HistoricalSources.ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false));
        var import = Substitute.For<IHistoricalImportService>();
        import.Run(Arg.Any<HistoricalArchiveInput>(), Arg.Any<HistoricalImportRunOptions>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new HistoricalImportReport()));
        var jobs = new HistoricalImportJobs(_context, new HistoricalImportSignal());
        return (new HistoricalImportRunner(_context, sources, import, jobs, Options.Create(new HistoricalGuardrailOptions()), NullLogger<HistoricalImportRunner>.Instance, userMerges: merges), jobs);
    }

    private async Task<HistoricalImportReport> Run(HistoricalImportJobKind kind, IUserMergeService? merges, CancellationToken ct = default)
    {
        var (runner, jobs) = await Runner(merges);
        var jobId = await jobs.Enqueue(new HistoricalImportJob { Kind = kind, SnapshotIDs = "" });
        return await runner.Run(jobId, _ => Task.CompletedTask, ct);
    }

    private Task<bool> AliasPlaceholderLeft() => _context.Users.AnyAsync(u => u.UserName == "historical:MenztrualCramps");

    [Test]
    public async Task AnImportJob_MergesTheAliasPlaceholders_AndListsTheMerges()
    {
        await SetUp();

        var report = await Run(HistoricalImportJobKind.Import, new UserMergeService(_context));

        Assert.That(report.Warnings, Has.One.StartsWith("Alias consolidation: merged MenztrualCramps into historical:MenZ"));
        Assert.That(report.SourcesFailed, Is.Empty);
        Assert.That(await AliasPlaceholderLeft(), Is.False);
    }

    [Test]
    public async Task ARefreshJob_MergesTheAliasPlaceholders()
    {
        await SetUp();

        var report = await Run(HistoricalImportJobKind.Refresh, new UserMergeService(_context));

        Assert.That(report.Warnings, Has.One.StartsWith("Alias consolidation: merged MenztrualCramps"));
        Assert.That(await AliasPlaceholderLeft(), Is.False);
    }

    [Test]
    public async Task APreview_ChangesNothing()
    {
        await SetUp();

        var report = await Run(HistoricalImportJobKind.Preview, new UserMergeService(_context));

        Assert.That(report.Warnings, Is.Empty);
        Assert.That(await AliasPlaceholderLeft(), Is.True);
    }

    [Test]
    public async Task WithoutAMergeService_NothingIsConsolidated()
    {
        await SetUp();

        var report = await Run(HistoricalImportJobKind.Import, null);

        Assert.That(report.Warnings, Is.Empty);
        Assert.That(await AliasPlaceholderLeft(), Is.True);
    }

    [Test]
    public async Task AFailedMerge_IsReported_AndTheJobSucceeds()
    {
        await SetUp();
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        merges.Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("clash")));

        var report = await Run(HistoricalImportJobKind.Import, merges);

        Assert.That(report.SourcesFailed, Is.EqualTo(new[] { "Alias consolidation: MenztrualCramps into MenZ: clash" }));
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public async Task AConsolidationThatCannotRun_IsReported_AndTheJobSucceeds()
    {
        // Reading the merges a sysadmin undid fails
        await SetUp(new FailingReads("UserMerges"));

        var report = await Run(HistoricalImportJobKind.Import, new UserMergeService(_context));

        Assert.That(report.SourcesFailed, Is.EqualTo(new[] { "Alias consolidation: no UserMerges today" }));
        Assert.That(await AliasPlaceholderLeft(), Is.True);
    }

    [Test]
    public async Task ACancelledConsolidation_CancelsTheJob()
    {
        await SetUp();
        using var cts = new CancellationTokenSource();
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        merges.Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cts.Cancel();
            return Task.FromCanceled(cts.Token);
        });

        await Assert.CatchAsync<OperationCanceledException>(() => Run(HistoricalImportJobKind.Import, merges, cts.Token));
    }

    /// <summary>Fails every query that reads the table.</summary>
    private sealed class FailingReads(string table) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => command.CommandText.Contains(table) && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                ? throw new InvalidOperationException($"no {table} today")
                : base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
