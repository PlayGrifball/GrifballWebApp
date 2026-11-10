using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>ButtonInteractionsTests' unit tests: the queue display refresh, which only publishes in a scope of its own. Its other
/// tests stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ButtonInteractionsUnitTests
{
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly RecordingLogger<MatchmakingButtonHandler> _handlerLogger = new();
    private readonly MatchmakingButtonHandler _handler;

    public ButtonInteractionsUnitTests()
    {
        // The refresh reaches neither the queue, the database, Discord nor the display service
        _handler = new MatchmakingButtonHandler(null!, new ServiceCollection().AddScoped(_ => _publisher).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), null!, null!, null!, _handlerLogger);
    }

    [Test]
    public async Task RefreshQueueDisplay_PublishesInItsOwnScope()
    {
        await _handler.RefreshQueueDisplay();

        await _publisher.Received(1).Publish(Arg.Any<UpdateDisplayNotification>(), Arg.Any<CancellationToken>());
        Assert.That(_handlerLogger.Entries, Is.Empty);
    }

    [Test]
    public async Task RefreshQueueDisplay_LogsFailures_InsteadOfThrowing()
    {
        _publisher.Publish(Arg.Any<UpdateDisplayNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("discord down")));

        await _handler.RefreshQueueDisplay();

        var entry = _handlerLogger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
        }
    }
}
