using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.Live;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StackExchange.Redis;

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

    /// <summary>The app's configuration, as every process here shares it: its prefix in Redis is this test's own.</summary>
    private IConfiguration Configuration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [AppRedisExtensions.ChannelPrefixKey] = _prefix }).Build();

    private RedisPresenceStore Store(TimeProvider? time = null)
        => new(Process(), Configuration(), time ?? TimeProvider.System, NullLogger<RedisPresenceStore>.Instance);

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
        var delivery = new LiveDelivery(watchers, hub, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LiveDelivery>.Instance, Configuration());
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

    [Test]
    public async Task TwoIdenticalChanges_PublishedSeparately_EachBuildFreshData()
    {
        // Two draft picks, moments apart, both touching the same topic alone: the same message, published twice
        var counts = new Queue<int>([1, 2]);
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<object?>(new QueueLiveDto(counts.Dequeue())));
        var client = Substitute.For<ILiveHubClient>();
        var watchers = new LiveWatchers();
        watchers.Watch(LiveTopics.Queue, "conn", SeasonViewer.Public);
        var hub = Substitute.For<IHubContext<LiveHub, ILiveHubClient>>();
        hub.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(client);
        var services = new ServiceCollection();
        services.AddScoped(_ => snapshots);
        var delivery = new LiveDelivery(watchers, hub, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LiveDelivery>.Instance, Configuration(), Process());
        var apiRedis = Process();
        var listener = new LiveRelayListener(delivery, apiRedis);
        await listener.StartAsync(CancellationToken.None);
        await apiRedis.ConnectAsync(); // the subscription only takes effect once connected
        var worker = new LiveUpdates(NullLogger<LiveUpdates>.Instance, Process());

        worker.Publish([new LiveChange(LiveTopics.Queue)]);
        await worker.SendPendingAsync();
        worker.Publish([new LiveChange(LiveTopics.Queue)]); // the exact same change, again
        await worker.SendPendingAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // pub/sub delivery and both builds

        using (Assert.EnterMultipleScope())
        {
            await client.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":1}");
            await client.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":2}");
        }
        await listener.StopAsync(CancellationToken.None);
    }

    /// <summary>A pod's LiveDelivery, its own LiveWatchers and Redis connection, with one connection watching <paramref name="topic"/>
    /// as <paramref name="viewer"/> (its own single connection: "conn" for every pod, since each has its own LiveWatchers).</summary>
    private LiveDelivery Pod(ILiveHubClient client, LiveSnapshots snapshots, string topic = LiveTopics.Queue, SeasonViewer viewer = SeasonViewer.Public)
    {
        var watchers = new LiveWatchers();
        watchers.Watch(topic, "conn", viewer);
        var hub = Substitute.For<IHubContext<LiveHub, ILiveHubClient>>();
        hub.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(client);
        var services = new ServiceCollection();
        services.AddScoped(_ => snapshots);
        return new LiveDelivery(watchers, hub, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LiveDelivery>.Instance, Configuration(), Process());
    }

    [Test]
    public async Task TwoPodsSharingRedis_ForTheSameChange_BuildTheTopicsDataOnce()
    {
        var changeID = Guid.NewGuid();
        var built = 0;
        async Task<object?> SlowBuild(NSubstitute.Core.CallInfo _)
        {
            Interlocked.Increment(ref built);
            await Task.Delay(500);
            return new QueueLiveDto(3);
        }
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        // Slow enough that the pod that loses the lock race is still waiting, not building its own, when this finishes
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(SlowBuild);
        var clientA = Substitute.For<ILiveHubClient>();
        var clientB = Substitute.For<ILiveHubClient>();
        var podA = Pod(clientA, snapshots);
        var podB = Pod(clientB, snapshots);

        await Task.WhenAll(
            podA.Deliver([new LiveChange(LiveTopics.Queue)], changeID),
            podB.Deliver([new LiveChange(LiveTopics.Queue)], changeID));

        // A third pod, once the data is already sitting in Redis: reads it too, without building or taking the lock
        var clientC = Substitute.For<ILiveHubClient>();
        await Pod(clientC, snapshots).Deliver([new LiveChange(LiveTopics.Queue)], changeID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(built, Is.EqualTo(1), "one pod built it, the others read it from Redis");
            await clientA.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":3}");
            await clientB.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":3}");
            await clientC.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":3}");
        }
    }

    [Test]
    public async Task AChangeWithNoDataToShow_IsCachedToo_SoOnlyOnePodBuildsIt()
    {
        var changeID = Guid.NewGuid();
        var built = 0;
        async Task<object?> Build(NSubstitute.Core.CallInfo _)
        {
            Interlocked.Increment(ref built);
            // Slow enough that the pod that loses the lock race is still polling, not finding it already there, when this finishes
            await Task.Delay(500);
            return null;
        }
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(Build);
        var clientA = Substitute.For<ILiveHubClient>();
        var clientB = Substitute.For<ILiveHubClient>();
        var podA = Pod(clientA, snapshots);
        var podB = Pod(clientB, snapshots);

        await Task.WhenAll(
            podA.Deliver([new LiveChange(LiveTopics.Queue)], changeID),
            podB.Deliver([new LiveChange(LiveTopics.Queue)], changeID));

        // A third pod, once the (empty) result is already sitting in Redis: reads that too, without building
        var clientC = Substitute.For<ILiveHubClient>();
        await Pod(clientC, snapshots).Deliver([new LiveChange(LiveTopics.Queue)], changeID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(built, Is.EqualTo(1), "nothing to show is cached too, so the other pods don't rebuild it");
            await clientA.Received(1).Changed(LiveTopics.Queue);
            await clientB.Received(1).Changed(LiveTopics.Queue);
            await clientC.Received(1).Changed(LiveTopics.Queue);
        }
    }

    [Test]
    public async Task AFailedBuild_IsNotCachedAsNothingToShow_TheOtherPodBuildsItsOwn()
    {
        var changeID = Guid.NewGuid();
        var attempt = 0;
        Task<object?> Build(NSubstitute.Core.CallInfo _)
        {
            attempt++;
            return attempt == 1
                ? Task.FromException<object?>(new InvalidOperationException("down"))
                : Task.FromResult<object?>(new QueueLiveDto(7));
        }
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(Build);
        var clientA = Substitute.For<ILiveHubClient>();
        var clientB = Substitute.For<ILiveHubClient>();
        var podA = Pod(clientA, snapshots);
        var podB = Pod(clientB, snapshots);

        // Sequential, so B definitely looks after A's failed build would otherwise have been cached
        await podA.Deliver([new LiveChange(LiveTopics.Queue)], changeID);
        await podB.Deliver([new LiveChange(LiveTopics.Queue)], changeID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempt, Is.EqualTo(2), "the failed build was not cached, so the second pod tried its own");
            await clientA.Received(1).Changed(LiveTopics.Queue);
            await clientB.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":7}");
        }
    }

    [Test]
    public async Task APodThatTakesTooLongToBuild_IsNotWaitedOnForever()
    {
        var changeID = Guid.NewGuid();
        var calls = 0;
        async Task<object?> Build(NSubstitute.Core.CallInfo _)
        {
            // The first call here is the lock's winner; make it take longer than BuildWait, as if that pod died mid-build
            if (Interlocked.Increment(ref calls) == 1)
                await Task.Delay(LiveDelivery.BuildWait + TimeSpan.FromSeconds(1));
            return new QueueLiveDto(calls);
        }
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(Build);
        var clientA = Substitute.For<ILiveHubClient>();
        var clientB = Substitute.For<ILiveHubClient>();
        var podA = Pod(clientA, snapshots);
        var podB = Pod(clientB, snapshots);

        await Task.WhenAll(
            podA.Deliver([new LiveChange(LiveTopics.Queue)], changeID),
            podB.Deliver([new LiveChange(LiveTopics.Queue)], changeID));

        using (Assert.EnterMultipleScope())
        {
            // The lock's winner builds the slow data; the other gives up waiting for it and builds its own
            await clientA.Received(1).Snapshot(LiveTopics.Queue, Arg.Is<string>(json => json == "{\"count\":1}" || json == "{\"count\":2}"));
            await clientB.Received(1).Snapshot(LiveTopics.Queue, Arg.Is<string>(json => json == "{\"count\":1}" || json == "{\"count\":2}"));
            Assert.That(calls, Is.EqualTo(2), "the slow winner's build, and the one the pod that gave up on it built for itself");
        }
    }

    [Test]
    public async Task ALateArrivingPod_DoesNotTakeAFreshLock_WhileTheOriginalBuilderIsStillWorking()
    {
        var changeID = Guid.NewGuid();
        var built = 0;
        async Task<object?> SlowBuild(NSubstitute.Core.CallInfo _)
        {
            Interlocked.Increment(ref built);
            // Longer than BuildWait (so an old, equally-short lock would have expired by the time the late pod arrives),
            // short enough to still be running when the late pod's own fresh attempt would land
            await Task.Delay(TimeSpan.FromSeconds(5));
            return new QueueLiveDto(3);
        }
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(SlowBuild);
        var clientA = Substitute.For<ILiveHubClient>();
        var clientC = Substitute.For<ILiveHubClient>();
        var podA = Pod(clientA, snapshots);

        var taskA = podA.Deliver([new LiveChange(LiveTopics.Queue)], changeID);
        await Task.Delay(TimeSpan.FromSeconds(4)); // past the old equal LockTtl/BuildWait, before the winner finishes at 5s

        await Pod(clientC, snapshots).Deliver([new LiveChange(LiveTopics.Queue)], changeID);
        await taskA;

        Assert.That(built, Is.EqualTo(1), "the late pod found the lock still held, not a fresh one to take");
    }

    [Test]
    public async Task Deliver_BuildsDifferentTopicsConcurrently_SoStalledWaitsDoNotAddUp()
    {
        var changeID = Guid.NewGuid();
        var configuration = Configuration();
        // Simulate another pod already building both topics (never finishing): pre-take every lock
        var raw = (await Process().ConnectAsync()).GetDatabase();
        string LockKeyOf(string topic) => $"{AppRedisExtensions.Key(configuration, "livebuild",
            LiveSnapshots.DependsOnViewer(topic) ? $"{changeID:N}:{topic}:{SeasonViewer.Public}" : $"{changeID:N}:{topic}")}:lock";
        foreach (var topic in new[] { LiveTopics.Queue, LiveTopics.Seasons })
            await raw.StringSetAsync(LockKeyOf(topic), RedisValue.EmptyString, LiveDelivery.LockTtl, When.NotExists);

        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(Arg.Any<string>(), Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(new QueueLiveDto(1));
        var client = Substitute.For<ILiveHubClient>();
        var watchers = new LiveWatchers();
        watchers.Watch(LiveTopics.Queue, "a", SeasonViewer.Public);
        watchers.Watch(LiveTopics.Seasons, "b", SeasonViewer.Public);
        var hub = Substitute.For<IHubContext<LiveHub, ILiveHubClient>>();
        hub.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(client);
        var services = new ServiceCollection();
        services.AddScoped(_ => snapshots);
        var delivery = new LiveDelivery(watchers, hub, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LiveDelivery>.Instance, configuration, Process());

        var sw = Stopwatch.StartNew();
        await delivery.Deliver([new LiveChange(LiveTopics.Queue), new LiveChange(LiveTopics.Seasons)], changeID);
        sw.Stop();

        Assert.That(sw.Elapsed, Is.LessThan(LiveDelivery.BuildWait + TimeSpan.FromSeconds(2)),
            "both topics waited out BuildWait alongside each other, not one after the other");
    }

    [Test]
    public async Task TwoPodsSharingRedis_ForAViewerDependentTopic_BuildOncePerViewer()
    {
        var changeID = Guid.NewGuid();
        var built = 0;
        async Task<object?> Build(NSubstitute.Core.CallInfo _)
        {
            Interlocked.Increment(ref built);
            await Task.Delay(300);
            return new SeasonsLiveDto([]);
        }
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Seasons, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(Build);
        var clientPublic = Substitute.For<ILiveHubClient>();
        var clientStaff = Substitute.For<ILiveHubClient>();
        var podPublic = Pod(clientPublic, snapshots, LiveTopics.Seasons, SeasonViewer.Public);
        var podStaff = Pod(clientStaff, snapshots, LiveTopics.Seasons, SeasonViewer.Staff);

        await Task.WhenAll(
            podPublic.Deliver([new LiveChange(LiveTopics.Seasons)], changeID),
            podStaff.Deliver([new LiveChange(LiveTopics.Seasons)], changeID));

        Assert.That(built, Is.EqualTo(2), "a different viewer is a different build, even for the same change");
    }

    [Test]
    public async Task ALaterChange_NeverReadsAnEarlierChangesData()
    {
        var counts = new Queue<int>([1, 2]);
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<object?>(new QueueLiveDto(counts.Dequeue())));
        var client = Substitute.For<ILiveHubClient>();
        var pod = Pod(client, snapshots);

        // Two changes moments apart, well inside the cached data's TTL: each must get its own build, under its own ID
        await pod.Deliver([new LiveChange(LiveTopics.Queue)], Guid.NewGuid());
        await pod.Deliver([new LiveChange(LiveTopics.Queue)], Guid.NewGuid());

        using (Assert.EnterMultipleScope())
        {
            await client.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":1}");
            await client.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":2}");
        }
    }
}
