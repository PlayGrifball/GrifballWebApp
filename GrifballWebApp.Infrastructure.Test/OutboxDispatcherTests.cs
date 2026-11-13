using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// OutboxDispatcher, the background loop around OutboxProcessor, started and stopped as the host does, on a real
/// (temp-file SQLite) GrifballContext behind a small DI container: it sends what is due, a group's messages one after
/// another, and carries on after a pass that fails. Each test waits for the handler of the group's last message (a
/// TaskCompletionSource, with a generous limit), never for a fixed time.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OutboxDispatcherTests : SqliteContextFixture
{
    private const string Kind = "test.dispatch";
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private ServiceProvider _provider = null!;
    private readonly TaskCompletionSource<long> _lastHandled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [SetUp]
    public void SetUp()
    {
        var handler = Substitute.For<IOutboxHandler>();
        handler.Kind.Returns(Kind);
        handler.MaxAge.Returns(TimeSpan.FromHours(6));
        handler.DeadLetterSkipsGroup.Returns(true);
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<GrifballContext>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var message = call.Arg<OutboxMessage>();
            if (message.Sequence == 1)
                _lastHandled.TrySetResult(message.Id);
            return Task.FromResult(OutboxOutcome.Done);
        });
        var services = new ServiceCollection();
        services.AddDbContext<GrifballContext>(o => o.UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString));
        services.AddScoped<IOutbox, ContextOutbox>();
        services.AddSingleton(handler);
        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public async Task DisposeProvider() => await _provider.DisposeAsync();

    /// <summary>Scopes that fail the first time they're asked for one: a pass that can't reach the database.</summary>
    private sealed class FailingOnceScopes(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        public int Asked { get; private set; }

        public IServiceScope CreateScope()
        {
            Asked++;
            return Asked == 1 ? throw new InvalidOperationException("database down") : inner.CreateScope();
        }
    }

    private static OutboxDispatcher Dispatcher(IServiceScopeFactory scopes, Microsoft.Extensions.Logging.ILogger<OutboxProcessor>? log = null)
    {
        var time = new FakeTimeProvider(Now);
        var options = Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
        var processor = new OutboxProcessor(scopes, options, Options.Create(new DiscordOptions()), time, log ?? NullLogger<OutboxProcessor>.Instance);
        return new OutboxDispatcher(processor, new OutboxSignal(), options, time, NullLogger<OutboxDispatcher>.Instance);
    }

    /// <summary>Two messages of one group, due now: the second goes once the first is sent.</summary>
    private async Task<(long First, long Second)> QueueGroupAsync()
    {
        OutboxMessage Message(int sequence) => new()
        {
            Kind = Kind,
            Payload = "{}",
            Status = OutboxStatus.Pending,
            GroupKey = "group",
            Sequence = sequence,
            CreatedAt = Now.UtcDateTime,
            NextAttemptAt = Now.UtcDateTime,
        };
        var first = Message(0);
        var second = Message(1);
        Context.Outbox.AddRange(first, second);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return (first.Id, second.Id);
    }

    private async Task<OutboxStatus> StatusOf(long id)
    {
        Context.ChangeTracker.Clear();
        return (await Context.Outbox.AsNoTracking().SingleAsync(m => m.Id == id)).Status;
    }

    [Test]
    public async Task Started_SendsAGroupsMessagesInOrder_AndStopsWhenTheHostDoes()
    {
        var (first, second) = await QueueGroupAsync();
        using var dispatcher = Dispatcher(_provider.GetRequiredService<IServiceScopeFactory>());

        await dispatcher.StartAsync(CancellationToken.None);
        var handled = await _lastHandled.Task.WaitAsync(Limit);
        await dispatcher.StopAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handled, Is.EqualTo(second));
            Assert.That(await StatusOf(first), Is.EqualTo(OutboxStatus.Sent), "sent before the group's next was handled");
        }
    }

    [Test]
    public async Task APassThatFails_IsTriedAgain_AfterThePollInterval()
    {
        var (first, second) = await QueueGroupAsync();
        var scopes = new FailingOnceScopes(_provider.GetRequiredService<IServiceScopeFactory>());
        using var dispatcher = Dispatcher(scopes);

        await dispatcher.StartAsync(CancellationToken.None);
        var handled = await _lastHandled.Task.WaitAsync(Limit);
        await dispatcher.StopAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handled, Is.EqualTo(second));
            Assert.That(await StatusOf(first), Is.EqualTo(OutboxStatus.Sent));
            Assert.That(scopes.Asked, Is.GreaterThan(1), "the first pass failed and another followed");
        }
    }

    [Test]
    public async Task Started_DeletesSentMessagesPastTheirRetention_StraightAway()
    {
        var old = new OutboxMessage { Kind = Kind, Payload = "{}", Status = OutboxStatus.Sent, CreatedAt = Now.UtcDateTime.AddDays(-30), NextAttemptAt = Now.UtcDateTime.AddDays(-30), SentAt = Now.UtcDateTime.AddDays(-30) };
        var recent = new OutboxMessage { Kind = Kind, Payload = "{}", Status = OutboxStatus.Sent, CreatedAt = Now.UtcDateTime, NextAttemptAt = Now.UtcDateTime, SentAt = Now.UtcDateTime };
        Context.Outbox.AddRange(old, recent);
        await Context.SaveChangesAsync();
        var log = new LogRecorder<OutboxProcessor>();
        using var dispatcher = Dispatcher(_provider.GetRequiredService<IServiceScopeFactory>(), log);

        await dispatcher.StartAsync(CancellationToken.None);
        // The first pass cleans up before it waits for the next: done once its log line is there
        using (var limit = new CancellationTokenSource(Limit))
            while (log.Entries.Count == 0)
                await Task.Delay(10, limit.Token);
        await dispatcher.StopAsync(CancellationToken.None);

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Outbox.AsNoTracking().Select(m => m.Id).ToListAsync(), Is.EqualTo(new List<long> { recent.Id }));
            Assert.That(log.At(Microsoft.Extensions.Logging.LogLevel.Information), Is.EqualTo(OneDeleted));
        }
    }

    private static readonly string[] OneDeleted = ["Deleted 1 old outbox messages"];
}
