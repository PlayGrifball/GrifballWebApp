using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Test;

/// <summary>
/// The Discord jobs Quartz runs every 10 seconds (the matchmaking queue, the season announcements): their schedule, off
/// while Discord is, and a run against the database.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordJobsTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BothTick_Every10Seconds_SkippingMissedTicks_AndAreOffWhileDiscordIs(bool enabled)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var job in new[] { MatchmakingQueueJob.Definition(enabled), SeasonEventsJob.Definition(enabled) })
        {
            var trigger = (ISimpleTrigger)job.DefaultTrigger(now);
            Assert.Multiple(() =>
            {
                Assert.That(job.Enabled, Is.EqualTo(enabled));
                Assert.That((trigger.RepeatInterval, trigger.RepeatCount, trigger.StartTimeUtc), Is.EqualTo((TimeSpan.FromSeconds(10), -1, now)));
                Assert.That(trigger.MisfireInstructionCode, Is.EqualTo((int)SimpleTriggerMisfireInstruction.NextWithRemainingCount));
            });
        }
        Assert.That(MatchmakingQueueJob.Key, Is.Not.EqualTo(SeasonEventsJob.Key));
    }

    [Test]
    public async Task TheMatchmakingJob_RunsTheQueue()
    {
        var me = Substitute.For<IDiscordCurrentUser>();
        _discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => Substitute.For<IDiscordRestMessage>());
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), Options.Create(new DiscordOptions { QueueChannel = 55 }),
            new QueueRepository(_context), _discordClient, _context, Substitute.For<IDataPullService>(), new ContextOutbox(_context));

        await new MatchmakingQueueJob(queueService, Failures()).Execute(Substitute.For<IJobExecutionContext>());

        await _discordClient.Received(1).SendMessageAsync(55ul, Arg.Is<MessageProperties>(m => m.Content == "Matchmaking Queue"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheSeasonEventsJob_QueuesTheAnnouncement()
    {
        var now = DateTime.UtcNow;
        _context.Seasons.Add(new Season
        {
            SeasonName = "BG Season",
            PublicAt = now.AddDays(-1),
            SignupsOpen = now.AddDays(-1),
            SignupsClose = now.AddDays(1),
            DraftStart = now.AddDays(2),
            SeasonStart = now.AddDays(3),
            SeasonEnd = now.AddDays(30),
        });
        await _context.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://example.test" }).Build();
        var eventsService = new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(new DiscordOptions { EventsChannel = 66 }),
            new ContextOutbox(_context), _context, new UrlService(config));

        await new SeasonEventsJob(eventsService, Failures()).Execute(Substitute.For<IJobExecutionContext>());

        // The announcement is queued on the outbox, which sends it.
        await using var check = _context.NewContextLike();
        var queued = await check.Outbox.SingleOrDefaultAsync();
        Assert.That(queued, Is.Not.Null);
        Assert.That(queued!.Kind, Is.EqualTo(EventsOutbox.SeasonUpsert));
        Assert.That(queued.Payload, Does.Contain("\"seasonName\":\"BG Season\""));
    }

    private static RepeatedFailures Failures(TimeProvider? time = null) => new(time ?? TimeProvider.System, Substitute.For<ILogger<RepeatedFailures>>());

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
        Assert.Multiple(() =>
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Skipped));
            Assert.That(report.Summary, Does.Contain("Discord is down"));
        });

        // Per job: another job's first failure is recorded
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(Firing(SeasonEventsJob.Key), Fail, CancellationToken.None));

        time.Advance(RepeatedFailures.RecordEvery);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failures.RunAsync(Firing(MatchmakingQueueJob.Key), Fail, CancellationToken.None), "10 minutes on, one is recorded again");
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
