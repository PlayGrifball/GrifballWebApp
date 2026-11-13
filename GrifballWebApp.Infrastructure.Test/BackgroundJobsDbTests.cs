using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The game variant backfill's runs (MatchVariantBackfillJob, its batch size from configuration, the data pull
/// substituted) and the outbox's health (OutboxHealthCheck), on a real (temp-file SQLite) GrifballContext.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackgroundJobsDbTests : SqliteContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private readonly IJobExecutionContext _run = Substitute.For<IJobExecutionContext>();

    private MatchVariantBackfillJob Backfill(int? batchSize = null) => new(_dataPull, Context,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MatchVariantBackfill:BatchSize"] = batchSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) }).Build(),
        NullLogger<MatchVariantBackfillJob>.Instance);

    private JobRunReport Report() => (JobRunReport)_run.Result!;

    [Test]
    public async Task Backfill_NothingMissing_IsSkipped()
    {
        var nothing = new VariantBackfillResult(0, 0, 0, 0, []);
        _dataPull.BackfillGameVariants(default).ReturnsForAnyArgs(nothing);

        await Backfill().Execute(_run);

        Assert.That((Report().Result, Report().Summary), Is.EqualTo((JobRunResult.Skipped, "Every match has its game variant and is decoded")));
        await _dataPull.Received(1).BackfillGameVariants(MatchVariantBackfillJob.DefaultBatchSize, Arg.Any<Action<int, int>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Backfill_SomeFilled_ReportsTheBatch_ItsProgressOnTheRun()
    {
        var filled = new VariantBackfillResult(10, 5, 5, 0, []);
        _dataPull.BackfillGameVariants(default).ReturnsForAnyArgs(call =>
        {
            call.Arg<Action<int, int>?>()!(2, 5);
            return Task.FromResult(filled);
        });

        await Backfill(batchSize: 5).Execute(_run);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Report().Result, Is.EqualTo(JobRunResult.Succeeded));
            Assert.That((Report().Metrics!["missing"], Report().Metrics!["filled"], Report().Metrics!["decoded"]), Is.EqualTo(((object?)10, (object?)5, (object?)0)));
        }
        await _dataPull.Received(1).BackfillGameVariants(5, Arg.Any<Action<int, int>?>(), Arg.Any<CancellationToken>());
        _run.Received(1).ReportProgress(40, "2 of 5 matches looked up");
    }

    [Test]
    public async Task Backfill_SomeHaloDidntGive_IsAFailure()
    {
        var failed = new VariantBackfillResult(10, 5, 3, 0, [Guid.Empty, Guid.Empty]);
        _dataPull.BackfillGameVariants(default).ReturnsForAnyArgs(failed);

        await Backfill().Execute(_run);

        Assert.That((Report().Result, Report().Metrics!["failed"]), Is.EqualTo((JobRunResult.Failed, (object?)2)));
    }

    private static HealthCheckContext HealthContext(OutboxHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("outbox", check, HealthStatus.Degraded, null),
    };

    [Test]
    public async Task Health_NoDeadLetters_IsHealthy()
    {
        Context.Outbox.Add(new OutboxMessage { Kind = "k", Payload = "{}", Status = OutboxStatus.Sent, CreatedAt = Now, NextAttemptAt = Now });
        await Context.SaveChangesAsync();
        var check = new OutboxHealthCheck(Context);

        var result = await check.CheckHealthAsync(HealthContext(check));

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task Health_DeadLetters_AreTheRegistrationsFailure_CountingThem()
    {
        Context.Outbox.AddRange(
            new OutboxMessage { Kind = "k", Payload = "{}", Status = OutboxStatus.DeadLettered, CreatedAt = Now, NextAttemptAt = Now },
            new OutboxMessage { Kind = "k", Payload = "{}", Status = OutboxStatus.DeadLettered, CreatedAt = Now, NextAttemptAt = Now },
            new OutboxMessage { Kind = "k", Payload = "{}", Status = OutboxStatus.Pending, CreatedAt = Now, NextAttemptAt = Now });
        await Context.SaveChangesAsync();
        var check = new OutboxHealthCheck(Context);

        var result = await check.CheckHealthAsync(HealthContext(check));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((result.Status, result.Description), Is.EqualTo((HealthStatus.Degraded, (string?)"2 Discord outbox messages were dead-lettered")));
            Assert.That(result.Data["dead_letters"], Is.EqualTo(2));
        }
    }
}
