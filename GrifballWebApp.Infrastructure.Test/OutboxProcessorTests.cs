using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// OutboxProcessor, on a real (temp-file SQLite) GrifballContext behind a real DI container: no database server, host
/// or network. Each test gets its own file (SqliteContextFixture) and a small container on it; the fixture's context
/// seeds and checks rows.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OutboxProcessorTests : SqliteContextFixture
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The test's containers (one per <see cref="Build"/>), disposed before the base fixture deletes the file.</summary>
    private readonly List<ServiceProvider> _providers = [];

    [TearDown]
    public async Task DisposeProviders()
    {
        foreach (var provider in _providers)
            await provider.DisposeAsync();
    }

    /// <summary>The processor under test, and the fixture's context (Reader) to seed and check rows.</summary>
    private sealed class Harness
    {
        public required OutboxProcessor Processor { get; init; }
        public required GrifballContext Reader { get; init; }
        public required FakeTimeProvider Time { get; init; }
        public required OutboxOptions Options { get; init; }

        public async Task<OutboxMessage> AddAsync(string kind, OutboxStatus status = OutboxStatus.Pending, string? groupKey = null,
            int sequence = 0, int attempts = 0, TimeSpan? dueIn = null)
        {
            var message = new OutboxMessage
            {
                Kind = kind,
                Payload = "{}",
                Status = status,
                GroupKey = groupKey,
                Sequence = sequence,
                Attempts = attempts,
                CreatedAt = Now.UtcDateTime,
                NextAttemptAt = (Now + (dueIn ?? TimeSpan.Zero)).UtcDateTime,
            };
            Reader.Outbox.Add(message);
            await Reader.SaveChangesAsync();
            Reader.ChangeTracker.Clear();
            return message;
        }

        public async Task<OutboxMessage> ReloadAsync(long id)
        {
            Reader.ChangeTracker.Clear();
            return await Reader.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
        }
    }

    /// <summary>An OutboxProcessor behind a small DI container on the fixture's database (its schema already made).</summary>
    private Harness Build(IEnumerable<IOutboxHandler>? handlers = null, OutboxOptions? options = null, DiscordOptions? discordOptions = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<GrifballContext>(o => o.UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString));
        services.AddScoped<IOutbox, ContextOutbox>();
        foreach (var handler in handlers ?? [])
            services.AddSingleton(handler);
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        var time = new FakeTimeProvider(Now);
        var resolvedOptions = options ?? new OutboxOptions();
        var processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(resolvedOptions),
            Microsoft.Extensions.Options.Options.Create(discordOptions ?? new DiscordOptions()),
            time,
            NullLogger<OutboxProcessor>.Instance);

        return new Harness { Processor = processor, Reader = Context, Time = time, Options = resolvedOptions };
    }
    }

    private static IOutboxHandler FakeHandler(string kind, bool deadLetterSkipsGroup = true, TimeSpan? maxAge = null)
    {
        var handler = Substitute.For<IOutboxHandler>();
        handler.Kind.Returns(kind);
        handler.DeadLetterSkipsGroup.Returns(deadLetterSkipsGroup);
        handler.MaxAge.Returns(maxAge ?? TimeSpan.FromHours(6));
        return handler;
    }

    [Test]
    public async Task ProcessDueAsync_NoMessagesDue_ReturnsZero()
    {
        var h = Build();

        var taken = await h.Processor.ProcessDueAsync(CancellationToken.None);

        Assert.That(taken, Is.Zero);
    }

    [Test]
    public async Task ProcessDueAsync_HandlerDone_MarksSent()
    {
        var handler = FakeHandler("test.kind");
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>()).Returns(OutboxOutcome.Done);
        var h = Build([handler]);
        var message = await h.AddAsync("test.kind");

        var taken = await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(taken, Is.EqualTo(1));
            Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.Sent));
            Assert.That(reloaded.SentAt, Is.EqualTo(Now.UtcDateTime));
            Assert.That(reloaded.LastError, Is.Null);
        }
        await handler.Received(1).HandleAsync(Arg.Is<OutboxMessage>(m => m.Id == message.Id), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueAsync_HandlerContinuesLater_RevertsTheAttemptAndReschedules()
    {
        var handler = FakeHandler("test.kind");
        var continueAt = (Now + TimeSpan.FromMinutes(10)).UtcDateTime;
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>()).Returns(OutboxOutcome.ContinueLater(continueAt));
        var h = Build([handler]);
        var message = await h.AddAsync("test.kind");

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.Pending));
            Assert.That(reloaded.Attempts, Is.Zero, "The attempt taken to claim it is handed back");
            Assert.That(reloaded.NextAttemptAt, Is.EqualTo(continueAt));
        }
    }

    [Test]
    public async Task ProcessDueAsync_DiscordDisabledGlobally_SkipsWithoutCallingAHandler()
    {
        var h = Build(discordOptions: new DiscordOptions { DisableGlobally = true });
        var message = await h.AddAsync("anything");

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.Skipped));
            Assert.That(reloaded.LastError, Is.EqualTo(OutboxProcessor.PreviewSkipReason));
        }
    }

    [Test]
    public async Task ProcessDueAsync_HandlerThrowsPreviewDiscordDisabledException_SkipsTheMessage()
    {
        var handler = FakeHandler("test.kind");
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PreviewDiscordDisabledException("preview"));
        var h = Build([handler]);
        var message = await h.AddAsync("test.kind");

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.Skipped));
            Assert.That(reloaded.LastError, Is.EqualTo(OutboxProcessor.PreviewSkipReason));
        }
    }

    [Test]
    public async Task ProcessDueAsync_HandlerThrows_FailsAndSchedulesARetry()
    {
        var handler = FakeHandler("test.kind");
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var options = new OutboxOptions { MaxAttempts = 10, RetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromMinutes(5) };
        var h = Build([handler], options);
        var message = await h.AddAsync("test.kind");

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.Failed));
            Assert.That(reloaded.Attempts, Is.EqualTo(1));
            Assert.That(reloaded.LastError, Is.EqualTo("boom"));
            Assert.That(reloaded.NextAttemptAt, Is.EqualTo((Now + TimeSpan.FromSeconds(5)).UtcDateTime), "The 1st retry's delay, undoubled");
        }
    }

    [Test]
    public async Task ProcessDueAsync_HandlerThrows_LongMessage_IsTruncatedTo2000Characters()
    {
        var handler = FakeHandler("test.kind");
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(new string('x', 2500)));
        var h = Build([handler]);
        var message = await h.AddAsync("test.kind");

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        Assert.That(reloaded.LastError, Has.Length.EqualTo(2000));
    }

    [Test]
    public async Task ProcessDueAsync_HandlerThrows_TooManyAttempts_DeadLettersAndSkipsTheRestOfTheGroup()
    {
        var handler = FakeHandler("test.kind", deadLetterSkipsGroup: true);
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var options = new OutboxOptions { MaxAttempts = 1 };
        var h = Build([handler], options);
        var first = await h.AddAsync("test.kind", groupKey: "g1", sequence: 0);
        var second = await h.AddAsync("test.kind", groupKey: "g1", sequence: 1);

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloadedFirst = await h.ReloadAsync(first.Id);
        var reloadedSecond = await h.ReloadAsync(second.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloadedFirst.Status, Is.EqualTo(OutboxStatus.DeadLettered));
            Assert.That(reloadedSecond.Status, Is.EqualTo(OutboxStatus.Skipped));
            Assert.That(reloadedSecond.LastError, Does.Contain(first.Id.ToString()).And.Contain("dead-lettered"));
        }
        await handler.Received(1).OnDeadLetteredAsync(Arg.Is<OutboxMessage>(m => m.Id == first.Id), Arg.Any<GrifballContext>(), Arg.Any<IOutbox>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueAsync_HandlerThrows_TooManyAttempts_HandlerAllowsTheGroupToContinue()
    {
        var handler = FakeHandler("test.kind", deadLetterSkipsGroup: false);
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var options = new OutboxOptions { MaxAttempts = 1 };
        var h = Build([handler], options);
        var first = await h.AddAsync("test.kind", groupKey: "g1", sequence: 0);
        var second = await h.AddAsync("test.kind", groupKey: "g1", sequence: 1);

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloadedSecond = await h.ReloadAsync(second.Id);
        Assert.That(reloadedSecond.Status, Is.EqualTo(OutboxStatus.Pending), "Not blocked on a dead letter that doesn't skip its group");
    }

    [Test]
    public async Task ProcessDueAsync_HandlerThrows_PastItsMaxAge_DeadLettersEvenUnderMaxAttempts()
    {
        var handler = FakeHandler("test.kind", maxAge: TimeSpan.FromMinutes(1));
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var options = new OutboxOptions { MaxAttempts = 10 };
        var h = Build([handler], options);
        var message = new OutboxMessage
        {
            Kind = "test.kind",
            Payload = "{}",
            Status = OutboxStatus.Pending,
            CreatedAt = (Now - TimeSpan.FromHours(1)).UtcDateTime,
            NextAttemptAt = Now.UtcDateTime,
        };
        h.Reader.Outbox.Add(message);
        await h.Reader.SaveChangesAsync();
        h.Reader.ChangeTracker.Clear();

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.DeadLettered));
    }

    [Test]
    public async Task ProcessDueAsync_NoHandlerRegisteredForTheKind_FailsWithADescriptiveError()
    {
        var h = Build();
        var message = await h.AddAsync("mystery.kind");

        await h.Processor.ProcessDueAsync(CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Status, Is.EqualTo(OutboxStatus.Failed));
            Assert.That(reloaded.LastError, Does.Contain("No outbox handler for mystery.kind"));
        }
    }

    [Test]
    public async Task ClaimAsync_TakesOnlyUpToBatchSize_OldestIdsFirst()
    {
        var h = Build();
        var a = await h.AddAsync("k");
        var b = await h.AddAsync("k");
        var c = await h.AddAsync("k");

        var claimed = await OutboxProcessor.ClaimAsync(h.Reader, Now.UtcDateTime, (Now + TimeSpan.FromMinutes(2)).UtcDateTime, batchSize: 2, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(claimed, Is.EqualTo(new List<long> { a.Id, b.Id }));
            Assert.That((await h.ReloadAsync(c.Id)).Attempts, Is.Zero, "Past the batch size: left alone");
        }
    }

    [Test]
    public async Task ClaimAsync_LeasesTheClaimedRowsAndCountsTheAttempt()
    {
        var h = Build();
        var message = await h.AddAsync("k");
        var leaseUntil = (Now + TimeSpan.FromMinutes(2)).UtcDateTime;

        await OutboxProcessor.ClaimAsync(h.Reader, Now.UtcDateTime, leaseUntil, batchSize: 20, CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Attempts, Is.EqualTo(1));
            Assert.That(reloaded.NextAttemptAt, Is.EqualTo(leaseUntil));
        }
    }

    [Test]
    public async Task ClaimAsync_NotYetDue_IsNotClaimed()
    {
        var h = Build();
        var message = await h.AddAsync("k", dueIn: TimeSpan.FromMinutes(5));

        var claimed = await OutboxProcessor.ClaimAsync(h.Reader, Now.UtcDateTime, (Now + TimeSpan.FromMinutes(2)).UtcDateTime, batchSize: 20, CancellationToken.None);

        var reloaded = await h.ReloadAsync(message.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(claimed, Is.Empty);
            Assert.That(reloaded.Attempts, Is.Zero, "Left alone: not due yet");
        }
    }

    [TestCase(OutboxStatus.Sent)]
    [TestCase(OutboxStatus.Skipped)]
    [TestCase(OutboxStatus.DeadLettered)]
    public async Task ClaimAsync_AlreadyFinished_IsNotClaimedAgain(OutboxStatus status)
    {
        var h = Build();
        await h.AddAsync("k", status: status);

        var claimed = await OutboxProcessor.ClaimAsync(h.Reader, Now.UtcDateTime, (Now + TimeSpan.FromMinutes(2)).UtcDateTime, batchSize: 20, CancellationToken.None);

        Assert.That(claimed, Is.Empty);
    }

    [Test]
    public async Task ClaimAsync_ABlockedGroupMember_IsSkippedUntilTheEarlierOneFinishes()
    {
        var h = Build();
        var first = await h.AddAsync("k", groupKey: "g1", sequence: 0);
        var second = await h.AddAsync("k", groupKey: "g1", sequence: 1);

        var claimed = await OutboxProcessor.ClaimAsync(h.Reader, Now.UtcDateTime, (Now + TimeSpan.FromMinutes(2)).UtcDateTime, batchSize: 20, CancellationToken.None);

        var reloadedSecond = await h.ReloadAsync(second.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(claimed, Is.EqualTo(new List<long> { first.Id }));
            Assert.That(reloadedSecond.Attempts, Is.Zero, "Blocked behind the first: not claimed");
        }
    }

    [Test]
    public async Task CleanupAsync_DeletesPastRetention_KeepsEverythingElse()
    {
        var options = new OutboxOptions { SentRetention = TimeSpan.FromHours(1), DeadLetterRetention = TimeSpan.FromHours(2) };
        var h = Build(options: options);
        var oldSent = await h.AddAsync("k", status: OutboxStatus.Sent);
        oldSent.SentAt = (Now - TimeSpan.FromHours(2)).UtcDateTime;
        var freshSent = await h.AddAsync("k", status: OutboxStatus.Sent);
        freshSent.SentAt = (Now - TimeSpan.FromMinutes(10)).UtcDateTime;
        var oldDead = await h.AddAsync("k", status: OutboxStatus.DeadLettered);
        oldDead.SentAt = (Now - TimeSpan.FromHours(3)).UtcDateTime;
        var stillPending = await h.AddAsync("k");
        h.Reader.Outbox.UpdateRange(oldSent, freshSent, oldDead);
        await h.Reader.SaveChangesAsync();
        h.Reader.ChangeTracker.Clear();

        var deleted = await h.Processor.CleanupAsync(CancellationToken.None);

        var remainingIds = await h.Reader.Outbox.Select(x => x.Id).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.EqualTo(2));
            Assert.That(remainingIds, Is.EquivalentTo(new List<long> { freshSent.Id, stillPending.Id }));
        }
    }
}
