using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Server.SignalR;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GrifballWebApp.Test;

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

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSignalR().AddAppBackplane(configuration);
        return services.BuildServiceProvider();
    }

    private static Type? LifetimeManager(ServiceProvider services)
        => services.GetRequiredService<HubLifetimeManager<TeamsHub>>().GetType().GetGenericTypeDefinition();

    [Test]
    public void WithoutAConnectionString_EachProcessKeepsItsOwnConnections()
    {
        using var services = Services(new() { ["ConnectionStrings:Redis"] = " " });

        Assert.That(LifetimeManager(services), Is.EqualTo(typeof(DefaultHubLifetimeManager<>)));
    }

    [Test]
    public void WithAConnectionString_UsesRedis_ConnectingInTheBackground()
    {
        // Nothing listens there: startup must not wait for it or fail.
        using var services = Services(new() { ["ConnectionStrings:Redis"] = "127.0.0.1:9" });

        Assert.That(LifetimeManager(services), Is.EqualTo(typeof(RedisHubLifetimeManager<>)));
        var options = services.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration;
        Assert.Multiple(() =>
        {
            Assert.That(options.AbortOnConnectFail, Is.False);
            Assert.That(options.ChannelPrefix.ToString(), Is.EqualTo(SignalRBackplaneExtensions.DefaultChannelPrefix));
            Assert.That(options.EndPoints.Single().ToString(), Does.EndWith(":9"));
        });
    }

    [Test]
    public void TheConnectionStringsOwnAbortConnect_AndAChannelPrefix_AreKept()
    {
        using var services = Services(new()
        {
            ["ConnectionStrings:Redis"] = "127.0.0.1:9,abortConnect=true",
            ["SignalR:RedisChannelPrefix"] = "grif-test",
        });

        var options = services.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration;
        Assert.Multiple(() =>
        {
            Assert.That(options.AbortOnConnectFail, Is.True);
            Assert.That(options.ChannelPrefix.ToString(), Is.EqualTo("grif-test"));
        });
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
}
