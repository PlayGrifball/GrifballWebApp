using GrifballWebApp.Server.Notifications;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NotificationsTests
{
    public record Ping(int Value) : INotification;
    public record Pong : INotification;
    public record Unhandled : INotification;

    /// <summary>What the handlers saw, in order.</summary>
    public class Log
    {
        public List<string> Entries { get; } = [];
    }

    /// <summary>A scoped dependency, to see which scope the handlers came from.</summary>
    public class ScopedThing;

    public class FirstPingHandler(Log log, ScopedThing scoped) : INotificationHandler<Ping>
    {
        public ScopedThing Scoped { get; } = scoped;

        public async Task Handle(Ping notification, CancellationToken cancellationToken)
        {
            log.Entries.Add($"first start {notification.Value}");
            await Task.Yield();
            log.Entries.Add($"first end {notification.Value}");
        }
    }

    public class SecondPingHandler(Log log) : INotificationHandler<Ping>
    {
        public Task Handle(Ping notification, CancellationToken cancellationToken)
        {
            log.Entries.Add($"second {notification.Value}");
            return Task.CompletedTask;
        }
    }

    /// <summary>Handles two notification types: registered for each.</summary>
    public class PingPongHandler(Log log) : INotificationHandler<Ping>, INotificationHandler<Pong>
    {
        public Task Handle(Ping notification, CancellationToken cancellationToken)
        {
            log.Entries.Add($"pingpong ping {notification.Value}");
            return Task.CompletedTask;
        }

        public Task Handle(Pong notification, CancellationToken cancellationToken)
        {
            log.Entries.Add("pingpong pong");
            return Task.CompletedTask;
        }
    }

    /// <summary>Like SignalRHandler and DiscordHandler: abstract, so not registered itself.</summary>
    public abstract class AbstractPongHandler : INotificationHandler<Pong>
    {
        public abstract Task Handle(Pong notification, CancellationToken cancellationToken);
    }

    /// <summary>An open generic handler: not registered (there is no type to close it with).</summary>
    public class GenericHandler<T> : INotificationHandler<Pong>
    {
        public Task Handle(Pong notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Log>();
        services.AddScoped<ScopedThing>();
        services.AddNotifications(typeof(NotificationsTests).Assembly);
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Test]
    public void AddNotifications_Should_RegisterEveryConcreteHandler_ForEachNotificationItHandles()
    {
        var services = new ServiceCollection();
        services.AddNotifications(typeof(NotificationsTests).Assembly);

        Type[] Handlers<T>() where T : INotification => services
            .Where(d => d.ServiceType == typeof(INotificationHandler<T>))
            .Select(d => d.ImplementationType!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Handlers<Ping>(), Is.EquivalentTo(new[] { typeof(FirstPingHandler), typeof(SecondPingHandler), typeof(PingPongHandler) }));
            Assert.That(Handlers<Pong>(), Is.EquivalentTo(new[] { typeof(PingPongHandler) }));
            Assert.That(Handlers<Unhandled>(), Is.Empty);
            Assert.That(services.Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>)),
                Has.All.Matches<ServiceDescriptor>(d => d.Lifetime == ServiceLifetime.Transient));
            Assert.That(services.Single(d => d.ServiceType == typeof(IPublisher)).Lifetime, Is.EqualTo(ServiceLifetime.Transient));
        });
    }

    [Test]
    public async Task Publish_Should_RunEveryHandler_OneAfterAnother_InRegistrationOrder()
    {
        await using var provider = Build();
        var log = provider.GetRequiredService<Log>();
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Ping(1));
        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Pong());

        // Registration order is the assembly's type order; what matters is that each handler finishes before the next.
        var order = scope.ServiceProvider.GetServices<INotificationHandler<Ping>>().Select(h => h.GetType()).ToList();
        var expected = order.SelectMany(t => t == typeof(FirstPingHandler) ? new[] { "first start 1", "first end 1" }
            : t == typeof(SecondPingHandler) ? new[] { "second 1" }
            : new[] { "pingpong ping 1" }).Append("pingpong pong");
        Assert.That(log.Entries, Is.EqualTo(expected));
    }

    [Test]
    public async Task Publish_Should_Complete_When_NothingHandlesTheNotification()
    {
        await using var provider = Build();
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Unhandled());

        Assert.That(provider.GetRequiredService<Log>().Entries, Is.Empty);
    }

    [Test]
    public async Task Publish_Should_StopAtTheFirstHandlerThatThrows_AndThrowItsException()
    {
        var failing = Substitute.For<INotificationHandler<Ping>>();
        failing.Handle(Arg.Any<Ping>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        var after = Substitute.For<INotificationHandler<Ping>>();

        var services = new ServiceCollection();
        services.AddTransient<IPublisher, Publisher>();
        services.AddSingleton(failing);
        services.AddSingleton(after);
        using var provider = services.BuildServiceProvider();

        await Assert.ThatAsync(() => provider.GetRequiredService<IPublisher>().Publish(new Ping(1)),
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("boom"));
        await after.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Test]
    public async Task Publish_Should_PassTheNotificationAndCancellationToken_ToTheHandlers()
    {
        var handler = Substitute.For<INotificationHandler<Ping>>();
        var services = new ServiceCollection();
        services.AddTransient<IPublisher, Publisher>();
        services.AddSingleton(handler);
        using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource();
        var ping = new Ping(5);

        await provider.GetRequiredService<IPublisher>().Publish(ping, cts.Token);

        await handler.Received(1).Handle(ping, cts.Token);
    }

    [Test]
    public async Task Publish_Should_RejectNull()
    {
        var services = new ServiceCollection();
        services.AddTransient<IPublisher, Publisher>();
        using var provider = services.BuildServiceProvider();

        await Assert.ThatAsync(() => provider.GetRequiredService<IPublisher>().Publish<Ping>(null!), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public async Task Publish_Should_BuildTheHandlers_InThePublishersScope()
    {
        // TeamService gets IPublisher in a request: its handlers must come from that request's scope.
        FirstPingHandler? seen = null;
        await using var provider = Build(services =>
        {
            services.AddTransient<INotificationHandler<Ping>>(sp => seen = ActivatorUtilities.CreateInstance<FirstPingHandler>(sp));
        });
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Ping(1));

        Assert.That(seen, Is.Not.Null);
        Assert.That(seen!.Scoped, Is.SameAs(scope.ServiceProvider.GetRequiredService<ScopedThing>()));
    }
}
