using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>The outbox itself (<see cref="IOutbox"/>, <see cref="OutboxProcessor"/>, the dispatcher), with a test handler.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OutboxTests
{
    private GrifballContext _context;
    private OutboxTestDriver _outbox;
    private TestHandler _handler;
    private DiscordOptions _discordOptions;

    public sealed record TestPayload(string Name);

    /// <summary>Records what it handled, in order, and does what the test sets for each name.</summary>
    public sealed class TestHandler : OutboxHandler<TestPayload>
    {
        public const string TestKind = "test.kind";

        public List<string> Handled { get; } = [];

        public List<string> DeadLettered { get; } = [];

        public Func<OutboxMessage, TestPayload, OutboxOutcome> Behaviour { get; set; } = (_, _) => OutboxOutcome.Done;

        public TimeSpan Age { get; set; } = TimeSpan.FromHours(6);

        public bool SkipsGroup { get; set; } = true;

        public override string Kind => TestKind;

        public override TimeSpan MaxAge => Age;

        public override bool DeadLetterSkipsGroup => SkipsGroup;

        protected override Task<OutboxOutcome> HandleAsync(OutboxMessage message, TestPayload payload, GrifballContext context, CancellationToken ct)
        {
            Handled.Add(payload.Name);
            return Task.FromResult(Behaviour(message, payload));
        }

        protected override async Task OnDeadLetteredAsync(OutboxMessage message, TestPayload payload, GrifballContext context, IOutbox outbox, CancellationToken ct)
        {
            DeadLettered.Add(payload.Name);
            await outbox.EnqueueAsync(TestKind, new TestPayload($"{payload.Name}-after"), message.GroupKey, ct);
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _handler = new TestHandler();
        _discordOptions = new DiscordOptions();
        _outbox = new OutboxTestDriver(_context, Substitute.For<IDiscordRestClient>(), _discordOptions,
            configure: services => services.AddSingleton<IOutboxHandler>(_handler));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _outbox.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private async Task Enqueue(params (string Name, string? Group)[] messages)
    {
        var outbox = new ContextOutbox(_context);
        foreach (var (name, group) in messages)
            await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload(name), group);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Test]
    public async Task Enqueue_IsCommittedWithTheChange_AndRolledBackWithIt()
    {
        var user = new User { UserName = "queued" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var outbox = new ContextOutbox(_context);

        await using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            _context.QueuedPlayer.Add(new QueuedPlayer { UserID = user.Id });
            await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("rolled back"), "g");
            await _context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        _context.ChangeTracker.Clear();

        await using (var fresh = _context.NewContextLike())
        {
            Assert.That(await fresh.Outbox.AnyAsync(), Is.False, "The message went with the change");
            Assert.That(await fresh.QueuedPlayer.AnyAsync(), Is.False);
        }

        await using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            _context.QueuedPlayer.Add(new QueuedPlayer { UserID = user.Id });
            await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("committed"), "g");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var saved = (await _outbox.MessagesAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Kind, Is.EqualTo(TestHandler.TestKind));
            Assert.That(saved.Payload, Is.EqualTo("""{"name":"committed"}"""));
            Assert.That(saved.Status, Is.EqualTo(OutboxStatus.Pending));
            Assert.That(saved.Attempts, Is.Zero);
            Assert.That(saved.Sequence, Is.Zero, "The rolled back one took no number");
        });
    }

    [Test]
    public async Task Enqueue_InAGroup_GoesAfterItsSavedAndUnsavedMessages()
    {
        await Enqueue(("a", "g"), ("b", "g"), ("x", null), ("other", "h"));
        var outbox = new ContextOutbox(_context);
        await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("c"), "g");
        await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("d"), "g");
        await _context.SaveChangesAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.That(messages.Select(x => (x.GroupKey, x.Sequence)), Is.EqualTo(new (string?, int)[] { ("g", 0), ("g", 1), (null, 0), ("h", 0), ("g", 2), ("g", 3) }));
    }

    [Test]
    public async Task Process_SendsAGroupInOrder_OneAtATime_AndNeverALaterStepBeforeAnEarlierOneIsSent()
    {
        // b's first attempt fails: c, after it in the group, waits; the other group goes ahead.
        var failedOnce = false;
        _handler.Behaviour = (_, p) =>
        {
            if (p.Name == "b" && !failedOnce)
            {
                failedOnce = true;
                throw new InvalidOperationException("b failed");
            }
            return OutboxOutcome.Done;
        };
        await Enqueue(("a", "g"), ("b", "g"), ("c", "g"), ("x", "h"), ("y", null));

        var claimed = await _outbox.Processor.ProcessDueAsync(CancellationToken.None);
        Assert.That(claimed, Is.EqualTo(3), "The first of each group, and the one on its own");
        await _outbox.RunDueAsync();
        Assert.That(_handler.Handled, Is.EqualTo(new[] { "a", "x", "y", "b" }), "c waits for b's retry");

        await _outbox.DrainAsync();

        Assert.That(_handler.Handled, Is.EqualTo(new[] { "a", "x", "y", "b", "b", "c" }));
        Assert.That((await _outbox.MessagesAsync()).Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Sent));
    }

    [Test]
    public async Task Process_FailedAttempt_BacksOffExponentially_UpToTheCap()
    {
        _handler.Behaviour = (_, _) => throw new HttpRequestException("503");
        await Enqueue(("a", null));
        await _outbox.RunDueAsync();
        var start = _outbox.Time.Now.UtcDateTime;
        var first = (await _outbox.MessagesAsync()).Single();
        _outbox.Time.Now = new DateTimeOffset(first.NextAttemptAt, TimeSpan.Zero);
        await _outbox.RunDueAsync();
        var second = (await _outbox.MessagesAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(OutboxStatus.Failed));
            Assert.That(first.Attempts, Is.EqualTo(1));
            Assert.That(first.LastError, Is.EqualTo("503"));
            Assert.That(first.NextAttemptAt, Is.EqualTo(start + TimeSpan.FromSeconds(5)).Within(TimeSpan.FromMilliseconds(5)));
            Assert.That(second.Attempts, Is.EqualTo(2));
            Assert.That(second.NextAttemptAt, Is.EqualTo(first.NextAttemptAt + TimeSpan.FromSeconds(10)).Within(TimeSpan.FromMilliseconds(5)));
            Assert.That(_outbox.Entries.Count(x => x.Level == LogLevel.Warning), Is.EqualTo(2));
        });

        var options = new OutboxOptions();
        Assert.That(Enumerable.Range(1, 9).Select(options.RetryDelayAfter).Select(x => (int)x.TotalSeconds),
            Is.EqualTo(new[] { 5, 10, 20, 40, 80, 160, 300, 300, 300 }));
    }

    [Test]
    public async Task Process_AfterTheLastAttempt_DeadLetters_SkipsTheRestOfTheGroup_AndRunsTheHook()
    {
        _handler.Behaviour = (_, p) => p.Name == "a" ? throw new HttpRequestException("503") : OutboxOutcome.Done;
        await Enqueue(("a", "g"), ("b", "g"), ("c", "g"));

        await _outbox.DrainAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages.Select(x => (x.Status, x.Sequence)), Is.EqualTo(new[]
            {
                (OutboxStatus.DeadLettered, 0), (OutboxStatus.Skipped, 1), (OutboxStatus.Skipped, 2), (OutboxStatus.Sent, 3),
            }));
            Assert.That(messages[0].Attempts, Is.EqualTo(_outbox.Options.MaxAttempts));
            Assert.That(messages[1].LastError, Is.EqualTo($"Outbox message {messages[0].Id} before it was dead-lettered"));
            Assert.That(_handler.Handled.Count(x => x == "a"), Is.EqualTo(_outbox.Options.MaxAttempts));
            Assert.That(_handler.Handled.Where(x => x != "a"), Is.EqualTo(new[] { "a-after" }), "Skipped ones are never sent; what the hook queued is");
            Assert.That(_handler.DeadLettered, Is.EqualTo(new[] { "a" }));
            Assert.That(_outbox.Entries.Single(x => x.Level == LogLevel.Error).Exception, Is.TypeOf<HttpRequestException>());
        });
    }

    [Test]
    public async Task Process_TooOld_DeadLettersBeforeTheLastAttempt_AndAHandlerThatAllowsIt_LetsItsGroupGoOn()
    {
        _handler.Age = TimeSpan.FromSeconds(30);
        _handler.SkipsGroup = false;
        _handler.Behaviour = (_, p) => p.Name == "a" ? throw new HttpRequestException("503") : OutboxOutcome.Done;
        await Enqueue(("a", "g"), ("b", "g"));

        await _outbox.DrainAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages[0].Status, Is.EqualTo(OutboxStatus.DeadLettered));
            Assert.That(messages[0].Attempts, Is.LessThan(_outbox.Options.MaxAttempts));
            Assert.That(messages[1].Status, Is.EqualTo(OutboxStatus.Sent));
        });
    }

    [Test]
    public async Task Process_KindWithoutAHandler_FailsAndIsRetried()
    {
        _context.Outbox.Add(new OutboxMessage { Kind = "nobody.handles", Payload = "{}" });
        await _context.SaveChangesAsync();

        await _outbox.RunDueAsync();

        var message = (await _outbox.MessagesAsync()).Single();
        Assert.That(message.Status, Is.EqualTo(OutboxStatus.Failed));
        Assert.That(message.LastError, Is.EqualTo("No outbox handler for nobody.handles"));
    }

    [Test]
    public async Task Process_ContinueLater_WaitsWithoutCountingAnAttempt_KeepingWhatItSaved()
    {
        _handler.Behaviour = (m, _) =>
        {
            if (m.Result is null)
            {
                m.Result = "step 1";
                return OutboxOutcome.ContinueLater(_outbox.Time.Now.UtcDateTime.AddSeconds(10));
            }
            return OutboxOutcome.Done;
        };
        await Enqueue(("a", null));

        await _outbox.RunDueAsync();
        var waiting = (await _outbox.MessagesAsync()).Single();
        await _outbox.DrainAsync();
        var sent = (await _outbox.MessagesAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(waiting.Status, Is.EqualTo(OutboxStatus.Pending));
            Assert.That(waiting.Attempts, Is.Zero);
            Assert.That(waiting.Result, Is.EqualTo("step 1"));
            Assert.That(sent.Status, Is.EqualTo(OutboxStatus.Sent));
            Assert.That(sent.SentAt, Is.Not.Null);
            Assert.That(_handler.Handled, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Process_PreviewWithoutDiscord_SkipsEverything_WithoutWarningsOrErrors()
    {
        _discordOptions.DisableGlobally = true;
        await Enqueue(("a", "g"), ("b", "g"), ("c", null));

        await _outbox.DrainAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages.Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Skipped));
            Assert.That(messages.Select(x => x.LastError), Is.All.EqualTo(OutboxProcessor.PreviewSkipReason));
            Assert.That(_handler.Handled, Is.Empty);
            Assert.That(_outbox.Entries.Where(x => x.Level >= LogLevel.Warning), Is.Empty);
        });
    }

    [Test]
    public async Task Process_PreviewDiscordDisabledException_SkipsTheMessage()
    {
        _handler.Behaviour = (_, _) => throw new PreviewDiscordDisabledException("Discord is disabled in preview mode; not sending POST /x");
        await Enqueue(("a", null));

        await _outbox.DrainAsync();

        var message = (await _outbox.MessagesAsync()).Single();
        Assert.That(message.Status, Is.EqualTo(OutboxStatus.Skipped));
        Assert.That(_outbox.Entries.Where(x => x.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task Claim_TakesNothingTwice_AndLeasesWhatItTakes()
    {
        await Enqueue(("a", null), ("b", null));
        var now = DateTime.UtcNow.AddSeconds(1);
        await using var context = _context.NewContextLike();

        var first = await OutboxProcessor.ClaimAsync(context, now, now.AddMinutes(2), 10, CancellationToken.None);
        var again = await OutboxProcessor.ClaimAsync(context, now, now.AddMinutes(2), 10, CancellationToken.None);
        var afterLease = await OutboxProcessor.ClaimAsync(context, now.AddMinutes(3), now.AddMinutes(5), 1, CancellationToken.None);

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(messages.Select(x => x.Id)));
            Assert.That(again, Is.Empty, "Leased");
            Assert.That(afterLease, Is.EqualTo(new[] { messages[0].Id }), "Taken again once the lease runs out (the dispatcher stopped), oldest first, up to the batch");
            Assert.That(messages.Select(x => x.Attempts), Is.EqualTo(new[] { 2, 1 }));
        });
    }

    [Test]
    public async Task Claim_PassesOverRowsAnotherDispatcherIsTaking_WithoutWaiting()
    {
        await Enqueue(("a", "g1"), ("b", "g2"));
        var now = DateTime.UtcNow.AddSeconds(1);
        await using var first = _context.NewContextLike();
        await using var second = _context.NewContextLike();

        // The first dispatcher's claim, held open: the second runs meanwhile. (SQL Server may pass over more than the row
        // being taken, the rows the first one read; Postgres passes over just that one.)
        List<long> taking, meanwhile;
        await using (var transaction = await first.Database.BeginTransactionAsync())
        {
            taking = await OutboxProcessor.ClaimAsync(first, now, now.AddMinutes(2), 1, CancellationToken.None);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            meanwhile = await OutboxProcessor.ClaimAsync(second, now, now.AddMinutes(2), 10, timeout.Token);
            await transaction.CommitAsync();
        }
        var after = await OutboxProcessor.ClaimAsync(second, now, now.AddMinutes(2), 10, CancellationToken.None);

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(taking, Is.EqualTo(new[] { messages[0].Id }));
            Assert.That(meanwhile, Does.Not.Contain(messages[0].Id), "Passed over, not waited for or taken twice");
            Assert.That(meanwhile.Concat(after), Is.EqualTo(new[] { messages[1].Id }), "The other row is taken once");
            Assert.That(messages.Select(x => x.Attempts), Is.EqualTo(new[] { 1, 1 }));
        });
    }

    [Test]
    public async Task Cleanup_DeletesSentAndSkippedPastRetention_AndOldDeadLetters()
    {
        var now = _outbox.Time.Now.UtcDateTime;
        OutboxMessage Row(OutboxStatus status, TimeSpan ago) => new() { Kind = "k", Payload = "{}", Status = status, SentAt = status is OutboxStatus.Pending ? null : now - ago, CreatedAt = now - ago };
        _context.Outbox.AddRange(
            Row(OutboxStatus.Sent, TimeSpan.FromDays(8)),
            Row(OutboxStatus.Skipped, TimeSpan.FromDays(8)),
            Row(OutboxStatus.Sent, TimeSpan.FromDays(6)),
            Row(OutboxStatus.DeadLettered, TimeSpan.FromDays(8)),
            Row(OutboxStatus.DeadLettered, TimeSpan.FromDays(31)),
            Row(OutboxStatus.Pending, TimeSpan.FromDays(40)));
        await _context.SaveChangesAsync();

        var deleted = await _outbox.Processor.CleanupAsync(CancellationToken.None);

        var left = await _outbox.MessagesAsync();
        Assert.That(deleted, Is.EqualTo(3));
        Assert.That(left.Select(x => x.Status), Is.EqualTo(new[] { OutboxStatus.Sent, OutboxStatus.DeadLettered, OutboxStatus.Pending }));
    }

    [Test]
    public async Task HealthCheck_IsDegraded_WhileThereAreDeadLetters()
    {
        var registration = new HealthCheckRegistration("discord_outbox", _ => null!, HealthStatus.Degraded, null);
        var check = new OutboxHealthCheck(_context);

        var healthy = await check.CheckHealthAsync(new HealthCheckContext { Registration = registration });
        _context.Outbox.Add(new OutboxMessage { Kind = "k", Payload = "{}", Status = OutboxStatus.DeadLettered });
        await _context.SaveChangesAsync();
        var degraded = await check.CheckHealthAsync(new HealthCheckContext { Registration = registration });

        Assert.Multiple(() =>
        {
            Assert.That(healthy.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(degraded.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(degraded.Description, Is.EqualTo("1 Discord outbox messages were dead-lettered"));
            Assert.That(degraded.Data["dead_letters"], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SignalInterceptor_SignalsOnceCommitted_NotOnRollbackOrOtherSaves()
    {
        var signal = new OutboxSignal();
        await using var context = new GrifballContext(TestDatabase.Options(_context.Database.GetConnectionString()!)
            .AddInterceptors(new OutboxSignalInterceptor(signal)).Options);
        var outbox = new ContextOutbox(context);
        async Task<bool> Signaled() => await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None);

        context.Users.Add(new User { UserName = "nothing queued" });
        await context.SaveChangesAsync();
        var plainSave = await Signaled();

        await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("a"));
        await context.SaveChangesAsync();
        var outsideTransaction = await Signaled();

        bool beforeCommit, afterCommit;
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("b"));
            await context.SaveChangesAsync();
            beforeCommit = await Signaled();
            await transaction.CommitAsync();
            afterCommit = await Signaled();
        }

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await outbox.EnqueueAsync(TestHandler.TestKind, new TestPayload("c"));
            context.SaveChanges();
            transaction.Rollback();
        }
        var afterRollback = await Signaled();

        signal.Notify();
        signal.Notify();
        var once = await Signaled();
        var twice = await Signaled();

        Assert.Multiple(() =>
        {
            Assert.That(plainSave, Is.False);
            Assert.That(outsideTransaction, Is.True);
            Assert.That(beforeCommit, Is.False);
            Assert.That(afterCommit, Is.True);
            Assert.That(afterRollback, Is.False);
            Assert.That((once, twice), Is.EqualTo((true, false)), "Wake-ups don't pile up");
        });
    }

    [Test]
    public async Task Dispatcher_SendsWhenSignaled_CleansUp_AndStopsCleanly()
    {
        var signal = new OutboxSignal();
        var scopes = new ServiceCollection()
            .AddScoped(_ => _context.NewContextLike())
            .AddScoped<IOutbox, ContextOutbox>()
            .AddSingleton<IOutboxHandler>(_handler)
            .BuildServiceProvider();
        var options = Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromMinutes(10) });
        var logger = new RecordingLogger<OutboxDispatcher>();
        var processor = new OutboxProcessor(scopes.GetRequiredService<IServiceScopeFactory>(), options, Options.Create(_discordOptions), TimeProvider.System, new RecordingLogger<OutboxProcessor>());
        var dispatcher = new OutboxDispatcher(processor, signal, options, TimeProvider.System, logger);
        var sent = new TaskCompletionSource();
        _handler.Behaviour = (_, _) => { sent.TrySetResult(); return OutboxOutcome.Done; };

        await dispatcher.StartAsync(CancellationToken.None);
        await Enqueue(("a", null));
        signal.Notify();
        var woke = await Task.WhenAny(sent.Task, Task.Delay(TimeSpan.FromSeconds(10))) == sent.Task;
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.That(woke, Is.True, "Sent on the signal, long before the next poll");
        Assert.That(logger.Entries.Where(x => x.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task Dispatcher_KeepsRunning_WhenProcessingFails()
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(_ => throw new InvalidOperationException("database down"));
        var options = Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
        var logger = new RecordingLogger<OutboxDispatcher>();
        var processor = new OutboxProcessor(scopes, options, Options.Create(_discordOptions), TimeProvider.System, new RecordingLogger<OutboxProcessor>());
        var dispatcher = new OutboxDispatcher(processor, new OutboxSignal(), options, TimeProvider.System, logger);

        await dispatcher.StartAsync(CancellationToken.None);
        var logged = await logger.WaitForEntries(2, TimeSpan.FromSeconds(10));
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.That(logged, Is.True, "Failed, and tried again");
        Assert.That(logger.Entries.Select(x => x.Level), Is.All.EqualTo(LogLevel.Error));
    }
}
