using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using static GrifballWebApp.Server.Test.SignalRBackplaneTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// SignalR's Redis backplane, on only when <c>ConnectionStrings:Redis</c> is set; with it, a message sent from any
/// process (the worker, for a pick made in Discord) is published for every API pod. Against a real Valkey.
/// </summary>
[TestFixture]
[NonParallelizable]
public class SignalRBackplaneTests
{
    private IContainer _redis = null!;
    private string _connectionString = null!;

    [OneTimeSetUp]
    public async Task StartRedis()
    {
        _redis = new ContainerBuilder("docker.io/valkey/valkey:9.1-alpine")
            .WithPortBinding(6379, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await _redis.StartAsync();
        _connectionString = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";
    }

    [OneTimeTearDown]
    public async Task StopRedis() => await _redis.DisposeAsync();

    [Test]
    public void WithAConnectionString_UsesRedis_ConnectingInTheBackground()
    {
        // Nothing listens there: startup must not wait for it or fail.
        using var services = Services(new() { ["ConnectionStrings:Redis"] = "127.0.0.1:9" });

        Assert.That(LifetimeManager(services), Is.EqualTo(typeof(RedisHubLifetimeManager<>)));
        var options = services.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.AbortOnConnectFail, Is.False);
            Assert.That(options.ChannelPrefix.ToString(), Is.EqualTo(AppRedisExtensions.DefaultChannelPrefix));
            Assert.That(options.EndPoints.Single().ToString(), Does.EndWith(":9"));
        }
    }

    [Test]
    public async Task AMessageSentFromOneProcess_IsPublishedForTheOthers()
    {
        // Another process listening, as an API pod's backplane does: everything on the prefix's channels.
        await using var listener = await ConnectionMultiplexer.ConnectAsync(_connectionString);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await listener.GetSubscriber().SubscribeAsync(RedisChannel.Pattern("grif-backplane-test*"),
            (channel, _) => received.TrySetResult(channel.ToString()));

        // The sender, say the worker: no connections of its own.
        await using var sender = Services(new()
        {
            ["ConnectionStrings:Redis"] = _connectionString,
            ["SignalR:RedisChannelPrefix"] = "grif-backplane-test",
        });
        var hub = sender.GetRequiredService<IHubContext<TeamsHub, ITeamsHubClient>>();
        await hub.Clients.All.LockCaptains(7);

        var channel = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(channel, Is.EqualTo($"grif-backplane-test{typeof(TeamsHub).FullName}:all"));
    }

    [Test]
    public async Task EndingAUsersSessionsOnOneProcess_ClosesTheirConnectionsOnTheOthers()
    {
        // Two API pods on one Redis: a connection of user 7's on the first, their roles changed through the second
        await using var first = new AppRedis(_connectionString, "grif-sessions-test");
        await using var second = new AppRedis(_connectionString, "grif-sessions-test");
        var firstsConnections = new GrifballWebApp.Server.SignalR.HubConnections();
        var connection = NSubstitute.Substitute.For<HubCallerContext>();
        connection.ConnectionId.Returns("on-the-first-pod");
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.When(c => c.Abort()).Do(_ => aborted.TrySetResult());
        firstsConnections.Add(connection, new GrifballWebApp.Server.SignalR.HubUserState(7, ["Commissioner"]));
        // Connected first: the listener subscribes at once
        await first.ConnectAsync();
        await new GrifballWebApp.Server.SignalR.HubUserSessionsListener(firstsConnections, first).StartAsync(CancellationToken.None);

        new GrifballWebApp.Server.SignalR.HubUserSessions(new GrifballWebApp.Server.SignalR.HubConnections(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GrifballWebApp.Server.SignalR.HubUserSessions>.Instance, second).End([7]);

        Assert.That(async () => await aborted.Task.WaitAsync(TimeSpan.FromSeconds(30)), Throws.Nothing, "the first pod closed it");
    }

    [Test]
    public async Task EndingAUsersSessions_WithRedisDown_StillClosesThisProcesssConnections()
    {
        await using var down = new AppRedis("127.0.0.1:9,abortConnect=true,connectTimeout=500", "grif-sessions-test");
        var connections = new GrifballWebApp.Server.SignalR.HubConnections();
        var connection = NSubstitute.Substitute.For<HubCallerContext>();
        connection.ConnectionId.Returns("here");
        connections.Add(connection, new GrifballWebApp.Server.SignalR.HubUserState(7, []));
        var logger = new GrifballWebApp.Server.Test.RecordingLogger<GrifballWebApp.Server.SignalR.HubUserSessions>();

        var sessions = new GrifballWebApp.Server.SignalR.HubUserSessions(connections, logger, down);

        sessions.End([7]);

        // Closed at once, before Redis is even tried: a save never waits on it
        connection.Received(1).Abort();
        await sessions.Published;
        Assert.That(logger.Entries, Has.Count.EqualTo(1), "the other pods not told: logged, not thrown");
    }
}
