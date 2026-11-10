using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Infrastructure.Events;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Matchmaking;
using static GrifballWebApp.Infrastructure.Test.DiscordJobsTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>DiscordJobsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordJobsUnitTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void BothTick_Every10Seconds_SkippingMissedTicks_AndAreOffWhileDiscordIs(bool enabled)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var job in new[] { MatchmakingQueueJob.Definition(enabled), SeasonEventsJob.Definition(enabled) })
        {
            var trigger = (ISimpleTrigger)job.DefaultTrigger(now);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(job.Enabled, Is.EqualTo(enabled));
                Assert.That((trigger.RepeatInterval, trigger.RepeatCount, trigger.StartTimeUtc), Is.EqualTo((TimeSpan.FromSeconds(10), -1, now)));
                Assert.That(trigger.MisfireInstructionCode, Is.EqualTo((int)SimpleTriggerMisfireInstruction.NextWithRemainingCount));
            }
        }
        Assert.That(MatchmakingQueueJob.Key, Is.Not.EqualTo(SeasonEventsJob.Key));
    }

    private static IJobExecutionContext Firing(JobKey key)
    {
        var context = Substitute.For<IJobExecutionContext>();
        var detail = Substitute.For<IJobDetail>();
        detail.Key.Returns(key);
        context.JobDetail.Returns(detail);
        return context;
    }

    [Test]
    public async Task AJobFailingOnEveryTick_RecordsAFailureEvery10Minutes_AndSkippedRunsInBetween()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var failures = Failures(time);
        Task Fail(CancellationToken _) => throw new InvalidOperationException("Discord is down");

        var first = Firing(MatchmakingQueueJob.Key);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(first, Fail, CancellationToken.None), "The first failure is recorded as one");

        time.Advance(TimeSpan.FromSeconds(10));
        var again = Firing(MatchmakingQueueJob.Key);
        await failures.RunAsync(again, Fail, CancellationToken.None);
        Assert.That(again.Result, Is.InstanceOf<JobRunReport>());
        var report = (JobRunReport)again.Result!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Skipped));
            Assert.That(report.Summary, Does.Contain("Discord is down"));
        }

        // Per job: another job's first failure is recorded
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(Firing(SeasonEventsJob.Key), Fail, CancellationToken.None));

        time.Advance(RepeatedFailures.RecordEvery);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(Firing(MatchmakingQueueJob.Key), Fail, CancellationToken.None), "10 minutes on, one is recorded again");
    }

    [Test]
    public async Task AJobFailingEveryOtherTick_StillRecordsAFailureEvery10Minutes()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var failures = Failures(time);
        Task Fail(CancellationToken _) => throw new InvalidOperationException("Discord is down");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(Firing(MatchmakingQueueJob.Key), Fail, CancellationToken.None));
        time.Advance(TimeSpan.FromSeconds(10));
        await failures.RunAsync(Firing(MatchmakingQueueJob.Key), _ => Task.CompletedTask, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(10));
        var again = Firing(MatchmakingQueueJob.Key);
        await failures.RunAsync(again, Fail, CancellationToken.None);
        Assert.That(((JobRunReport)again.Result!).Result, Is.EqualTo(JobRunResult.Skipped), "A success in between doesn't reset the 10 minutes");

        time.Advance(RepeatedFailures.RecordEvery);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(Firing(MatchmakingQueueJob.Key), Fail, CancellationToken.None), "10 minutes on, one is recorded again");
    }

    /// <summary>A firing on a scheduler named Grif, whose history <paramref name="history"/> holds.</summary>
    private static IJobExecutionContext FiringOnTheCluster(JobKey key)
    {
        var context = Firing(key);
        var scheduler = Substitute.For<IScheduler>();
        scheduler.SchedulerName.Returns("Grif");
        context.Scheduler.Returns(scheduler);
        return context;
    }

    private static JobRunStatus FailedAt(JobKey key, DateTimeOffset at) => new("Grif", key, at, JobRunResult.Failed) { LastFailedAtUtc = at };

    [Test]
    public async Task AFailureAnotherWorkerRecorded_CountsHereToo()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var history = Substitute.For<Quartz.Extensibility.IExecutionHistoryStore>();
        // The other worker recorded one 2 minutes ago
        history.GetJobRunStatus("Grif", MatchmakingQueueJob.Key, Arg.Any<CancellationToken>()).Returns(FailedAt(MatchmakingQueueJob.Key, time.GetUtcNow() - TimeSpan.FromMinutes(2)));
        var failures = new RepeatedFailures(time, Substitute.For<ILogger<RepeatedFailures>>(), history);
        Task Fail(CancellationToken _) => throw new InvalidOperationException("Discord is down");

        var context = FiringOnTheCluster(MatchmakingQueueJob.Key);
        await failures.RunAsync(context, Fail, CancellationToken.None);
        Assert.That(((JobRunReport)context.Result!).Result, Is.EqualTo(JobRunResult.Skipped), "Within 10 minutes of the other worker's");

        // 10 minutes after the other worker's, one is recorded again
        time.Advance(TimeSpan.FromMinutes(8));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(FiringOnTheCluster(MatchmakingQueueJob.Key), Fail, CancellationToken.None));
    }

    [Test]
    public async Task WithNoFailureInTheHistory_OrAHistoryThatCantBeRead_ThisWorkersOwnRecordDecides()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var history = Substitute.For<Quartz.Extensibility.IExecutionHistoryStore>();
        history.GetJobRunStatus("Grif", MatchmakingQueueJob.Key, Arg.Any<CancellationToken>()).Returns((JobRunStatus?)null);
        history.GetJobRunStatus("Grif", SeasonEventsJob.Key, Arg.Any<CancellationToken>()).Returns<JobRunStatus?>(_ => throw new InvalidOperationException("The database is down"));
        var failures = new RepeatedFailures(time, Substitute.For<ILogger<RepeatedFailures>>(), history);
        Task Fail(CancellationToken _) => throw new InvalidOperationException("Discord is down");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(FiringOnTheCluster(MatchmakingQueueJob.Key), Fail, CancellationToken.None), "None recorded: this one is");
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(FiringOnTheCluster(SeasonEventsJob.Key), Fail, CancellationToken.None), "Unreadable: this one is");

        time.Advance(TimeSpan.FromSeconds(10));
        var again = FiringOnTheCluster(SeasonEventsJob.Key);
        await failures.RunAsync(again, Fail, CancellationToken.None);
        Assert.That(((JobRunReport)again.Result!).Result, Is.EqualTo(JobRunResult.Skipped), "This worker recorded one a moment ago");
    }

    [Test]
    public async Task AStoppingJob_IsNotTakenForAFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var failures = Failures();
        var context = Firing(MatchmakingQueueJob.Key);
        await Assert.CatchAsync<OperationCanceledException>(async () => await failures.RunAsync(context, ct => Task.FromCanceled(ct), cts.Token));
        await Assert.CatchAsync<OperationCanceledException>(async () => await failures.RunAsync(context, ct => Task.FromCanceled(ct), cts.Token), "Each time: it never turns into a skipped run");
    }
}

/// <summary>DiscordJobsTests' helpers, shared with its integration tests.</summary>
internal static class DiscordJobsTestData
{
    internal static RepeatedFailures Failures(TimeProvider? time = null) => new(time ?? TimeProvider.System, Substitute.For<ILogger<RepeatedFailures>>());
}
