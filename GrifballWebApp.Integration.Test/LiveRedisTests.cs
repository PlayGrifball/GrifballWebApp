using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.Live;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The live updates scaled out, against a real Valkey: who is on the site across processes (RedisPresenceStore: each
/// process's tabs in one hash, a process gone quiet dropped, one leaving taken out), and a change published on one process
/// reaching another's pages (LiveUpdates through LiveRelayListener). Each test under a prefix of its own.
/// </summary>
[TestFixture]
[NonParallelizable]
public class LiveRedisTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly string[] BothTabs = ["a", "b"];
    private static readonly string[] SecondsTab = ["b"];

    private IContainer _redis = null!;
    private string _connectionString = null!;
    private string _prefix = null!;
    private readonly List<AppRedis> _connections = [];

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

    [SetUp]
    public void SetUp() => _prefix = $"live-test-{Guid.NewGuid():N}:";

    [TearDown]
    public async Task TearDown()
    {
        foreach (var connection in _connections)
            await connection.DisposeAsync();
        _connections.Clear();
    }

    /// <summary>A process's connection to Redis, under the test's prefix.</summary>
    private AppRedis Process()
    {
        var redis = new AppRedis(_connectionString, _prefix);
        _connections.Add(redis);
        return redis;
    }

    private RedisPresenceStore Store(TimeProvider? time = null)
        => new(Process(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [AppRedisExtensions.ChannelPrefixKey] = _prefix }).Build(),
            time ?? TimeProvider.System, NullLogger<RedisPresenceStore>.Instance);

    private async Task<long> StoredProcesses()
        => await (await Process().ConnectAsync()).GetDatabase().HashLengthAsync($"{_prefix}presence");

    [Test]
    public async Task TwoProcessesTabs_AreEveryonesTabs_UntilOneLeaves()
    {
        var first = Store();
        var second = Store();
        PresenceTab[] firstsTabs = [new("a", null, null, "/")];
        PresenceTab[] secondsTabs = [new("b", 7, "Seven", "/seasons")];

        await first.Exchange(firstsTabs);
        var all = await second.Exchange(secondsTabs);
        await first.Leave();
        var afterLeaving = await second.Exchange(secondsTabs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.Select(t => t.ConnectionID), Is.EquivalentTo(BothTabs));
            Assert.That(afterLeaving.Select(t => t.ConnectionID), Is.EquivalentTo(SecondsTab));
            Assert.That(await StoredProcesses(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AnEntryThatIsNoProcesssTabs_IsDropped()
    {
        var db = (await Process().ConnectAsync()).GetDatabase();
        await db.HashSetAsync($"{_prefix}presence", [new("empty", ""), new("null", "null"), new("garbled", "not json")]);
        var alive = Store();
        PresenceTab[] aliveTabs = [new("b", null, null, "/")];

        var all = await alive.Exchange(aliveTabs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.Select(t => t.ConnectionID), Is.EquivalentTo(SecondsTab));
            Assert.That(await StoredProcesses(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AProcessThatStoppedWithoutLeaving_IsDropped_OnceStale()
    {
        // The first stored its tabs long ago (killed since): the second's exchange drops them
        var longAgo = new FakeTimeProvider(DateTimeOffset.UtcNow - RedisPresenceStore.Stale - TimeSpan.FromMinutes(1));
        var killed = Store(longAgo);
        var alive = Store();
        PresenceTab[] killedTabs = [new("a", null, null, "/")];
        PresenceTab[] aliveTabs = [new("b", null, null, "/")];

        await killed.Exchange(killedTabs);
        var all = await alive.Exchange(aliveTabs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.Select(t => t.ConnectionID), Is.EquivalentTo(SecondsTab));
            Assert.That(await StoredProcesses(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AChangePublishedOnOneProcess_ReachesAnotherProcesssPages()
    {
        // The API process: its pages watching player:5, told through the relay
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watching = Substitute.For<ILiveHubClient>();
        watching.Changed(Arg.Any<string>()).Returns(_ =>
        {
            told.TrySetResult();
            return Task.CompletedTask;
        });
        var hub = Substitute.For<IHubContext<LiveHub, ILiveHubClient>>();
        hub.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watching);
        var watchers = new LiveWatchers();
        watchers.Watch(LiveTopics.Player(5), "conn", SeasonViewer.Public);
        var delivery = new LiveDelivery(watchers, new HubConnections(), hub, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LiveDelivery>.Instance);
        var apiRedis = Process();
        var listener = new LiveRelayListener(delivery, apiRedis);
        await listener.StartAsync(CancellationToken.None);
        await apiRedis.ConnectAsync();

        // The worker: publishes, with no pages of its own
        var worker = new LiveUpdates(NullLogger<LiveUpdates>.Instance, Process());
        worker.Publish([new LiveChange(LiveTopics.Player(5))]);
        await worker.SendPendingAsync();

        await told.Task.WaitAsync(Patience);
        await listener.StopAsync(CancellationToken.None);
        await watching.Received(1).Changed(LiveTopics.Player(5));
    }
}
