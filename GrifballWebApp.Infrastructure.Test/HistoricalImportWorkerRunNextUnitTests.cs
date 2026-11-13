using System.Net;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// HistoricalImportWorker.RunNextAsync/RunJobAsync's Refresh path, on a real (temp-file SQLite) GrifballContext
/// behind a real DI container (as the app wires it): claims the oldest queued job, runs it, and finishes it.
/// Every catalog source stays disabled, so the refresh itself makes no HTTP call; HistoricalSourceService's own
/// downloads stay HistoricalSourceServiceUnitTests' and HistoricalImportRunnerRefreshUnitTests'. Import jobs
/// (IHistoricalImportService) stay GrifballWebApp.Integration.Test's.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportWorkerRunNextUnitTests : SqliteContextFixture
{
    private ServiceProvider _provider = null!;
    private HistoricalImportWorker _worker = null!;

    [SetUp]
    public async Task Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GrifballContext>(o => o.UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<GrifballContext>());
        services.AddSingleton(new HistoricalImportSignal());
        services.AddSingleton(new XuidLookupSignal());
        // Never actually called: every catalog source is disabled before each test runs.
        services.AddSingleton(new HttpClient(new NeverCalledHandler()));
        services.AddScoped(sp => new HistoricalSourceFetcher(sp.GetRequiredService<HttpClient>()));
        services.AddScoped(sp => new HistoricalSourceService(sp.GetRequiredService<GrifballContext>(), sp.GetRequiredService<HistoricalSourceFetcher>(),
            TimeProvider.System, sp.GetRequiredService<ILogger<HistoricalSourceService>>()));
        services.AddScoped(sp => new HistoricalImportJobs(sp.GetRequiredService<IApplicationDbContext>(), sp.GetRequiredService<HistoricalImportSignal>()));
        services.AddSingleton(Substitute.For<IHistoricalImportService>());
        services.AddSingleton(Options.Create(new HistoricalGuardrailOptions()));
        services.AddScoped(sp => new HistoricalImportRunner(sp.GetRequiredService<GrifballContext>(), sp.GetRequiredService<HistoricalSourceService>(),
            sp.GetRequiredService<IHistoricalImportService>(), sp.GetRequiredService<HistoricalImportJobs>(),
            sp.GetRequiredService<IOptions<HistoricalGuardrailOptions>>(), sp.GetRequiredService<ILogger<HistoricalImportRunner>>()));
        _provider = services.BuildServiceProvider();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            // Nothing downloadable: the refresh itself is a no-op, no HTTP call
            await scope.ServiceProvider.GetRequiredService<HistoricalSourceService>().EnsureCatalog();
            await context.HistoricalSources.ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false));
        }

        _worker = new HistoricalImportWorker(_provider.GetRequiredService<IServiceScopeFactory>(), _provider.GetRequiredService<HistoricalImportSignal>(),
            Options.Create(new HistoricalImportWorkerOptions()), TimeProvider.System, NullLogger<HistoricalImportWorker>.Instance,
            _provider.GetRequiredService<XuidLookupSignal>());
    }

    /// <summary>Never answers: every catalog source is disabled, so HistoricalSourceService.Refresh makes no request.</summary>
    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("No source is enabled: nothing should ask the network");
    }

    [TearDown]
    public async Task TearDown()
    {
        await _provider.DisposeAsync();
    }

    private async Task<int> QueueRefresh()
    {
        var job = HistoricalImportJob.Refresh();
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return job.HistoricalImportJobID;
    }

    [Test]
    public async Task RunNextAsync_NothingQueued_ReturnsFalse()
        => Assert.That(await _worker.RunNextAsync(CancellationToken.None), Is.False);

    [Test]
    public async Task RunNextAsync_AQueuedRefresh_ClaimsRunsAndFinishesIt_ReturningTrue()
    {
        var jobId = await QueueRefresh();

        var ran = await _worker.RunNextAsync(CancellationToken.None);

        var job = await Context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == jobId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ran, Is.True);
            Assert.That(job.Status, Is.EqualTo(HistoricalImportJobStatus.Succeeded));
            Assert.That(job.Report, Is.Not.Null.And.Contains("SourcesChanged"));
            Assert.That(job.FinishedAt, Is.Not.Null);
        }
    }

    [Test]
    public async Task RunNextAsync_TwoQueuedJobs_RunsOneAtATime_OldestFirst()
    {
        var first = await QueueRefresh();
        var second = await QueueRefresh();

        var ranFirst = await _worker.RunNextAsync(CancellationToken.None);
        var statusAfterFirst = (await Context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == second)).Status;
        var ranSecond = await _worker.RunNextAsync(CancellationToken.None);
        var ranThird = await _worker.RunNextAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((ranFirst, ranSecond, ranThird), Is.EqualTo((true, true, false)));
            Assert.That(statusAfterFirst, Is.EqualTo(HistoricalImportJobStatus.Queued), "The second waited for the first");
            Assert.That((await Context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == first)).Status, Is.EqualTo(HistoricalImportJobStatus.Succeeded));
        }
    }
}
