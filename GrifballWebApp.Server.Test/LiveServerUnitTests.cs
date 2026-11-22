using System.Diagnostics;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.Live;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The live updates' and presence's running parts: the tracker and the publisher as hosted services (started, a change
/// sent, stopped), a signed-in tab's name, LiveHub's page and leaving, and Redis being down (this process's own tabs and
/// pages at least). Redis itself: GrifballWebApp.Integration.Test's LiveRedisTests.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class LiveServerUnitTests : SqliteGrifballContextFixture
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    private readonly IHubContext<LiveHub, ILiveHubClient> _hubContext = Substitute.For<IHubContext<LiveHub, ILiveHubClient>>();
    private readonly ILiveHubClient _everyone = Substitute.For<ILiveHubClient>();
    private readonly LiveWatchers _watchers = new();

    [SetUp]
    public void SetUp() => _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(_everyone);

    /// <summary>Scopes whose IApplicationDbContext is a context of their own on the test's database, as a request's is;
    /// <paramref name="snapshots"/>'s too, when a test builds a topic's data.</summary>
    private IServiceScopeFactory Scopes(LiveSnapshots? snapshots = null)
    {
        var connectionString = Db.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, connectionString).Options));
        if (snapshots is not null)
            services.AddScoped(_ => snapshots);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private PresenceTracker Tracker(IPresenceStore? store = null, TimeProvider? time = null)
        => new(store ?? new MemoryPresenceStore(), _hubContext, Scopes(), time ?? TimeProvider.System, NullLogger<PresenceTracker>.Instance);

    /// <summary>A Redis that is down: every connection attempt fails.</summary>
    private static AppRedis DownRedis()
        => new(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down"));

    [Test]
    public async Task TheTracker_Running_SendsWhoIsOnOnceItSettles_AndLeavesTheStoreWhenStopped()
    {
        var sent = new TaskCompletionSource<PresenceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _everyone.Presence(Arg.Any<PresenceSnapshot>()).Returns(call =>
        {
            sent.TrySetResult(call.Arg<PresenceSnapshot>());
            return Task.CompletedTask;
        });
        var store = Substitute.For<IPresenceStore>();
        store.Exchange(Arg.Any<IReadOnlyList<PresenceTab>>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<PresenceTab>>(call => call.Arg<IReadOnlyList<PresenceTab>>());
        var tracker = Tracker(store);

        await tracker.StartAsync(CancellationToken.None);
        await tracker.Viewing("a", null, "/seasons");
        var snapshot = await sent.Task.WaitAsync(Wait);
        await tracker.StopAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((snapshot.Online, snapshot.Guests), Is.EqualTo((1, 1)));
            Assert.That(snapshot.Pages.Single().Path, Is.EqualTo("/seasons"));
            await store.Received(1).Leave(Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task TheTracker_WithNoChange_StoresItsTabsAgainEachHeartbeat()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var stored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Substitute.For<IPresenceStore>();
        store.Exchange(Arg.Any<IReadOnlyList<PresenceTab>>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<PresenceTab>>(call =>
        {
            stored.TrySetResult();
            return call.Arg<IReadOnlyList<PresenceTab>>();
        });
        var tracker = Tracker(store, time);

        await tracker.StartAsync(CancellationToken.None);
        // ExecuteAsync starts on its own thread: its heartbeat may be set after a first advance, so keep the clock going
        using (var giveUp = new CancellationTokenSource(Wait))
        {
            while (!stored.Task.IsCompleted)
            {
                time.Advance(PresenceTracker.Heartbeat);
                await Task.WhenAny(stored.Task, Task.Delay(TimeSpan.FromMilliseconds(50), giveUp.Token));
                giveUp.Token.ThrowIfCancellationRequested();
            }
        }
        await tracker.StopAsync(CancellationToken.None);

        await store.Received().Exchange(Arg.Any<IReadOnlyList<PresenceTab>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ASignedInTab_IsNamedAsTheSiteShowsTheUser_NeverByTheirLogin()
    {
        var user = User.Seed(userName: "secret-login", displayName: "Seven");
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        var tracker = Tracker();

        await tracker.Viewing("a", user.Id, "/seasons");
        await tracker.Viewing("a", user.Id, "/leaderboards");
        await tracker.SendAsync();

        var member = tracker.Current.Pages.Single().Members.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracker.Current.Pages.Single().Path, Is.EqualTo("/leaderboards"), "the tab moved on: one tab still");
            Assert.That((member.UserID, member.Name), Is.EqualTo((user.Id, (string?)"Seven")));
        }
    }

    [Test]
    public async Task TheHub_Viewing_RecordsThePageAsEveryoneMaySeeIt_AndLeaving_DropsTheTab()
    {
        var tracker = Tracker();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn");
        context.Items.Returns(new Dictionary<object, object?>());
        var visibility = new SeasonVisibility(Db);
        var hub = new LiveHub(new LiveTopicAccess(visibility, Db), new HubUser(), _watchers, tracker, new PresencePages(visibility, Db)) { Context = context };

        var returned = await hub.Viewing("/seasonmanager?tab=2");
        await tracker.SendAsync();
        var whileThere = tracker.Current;
        await hub.OnDisconnectedAsync(null);
        await tracker.SendAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(returned, Is.SameAs(PresenceSnapshot.Empty), "who was on before this tab's page was sent");
            Assert.That(whileThere.Pages.Single().Path, Is.EqualTo(PresenceSnapshot.StaffArea));
            Assert.That(tracker.Current.Online, Is.Zero);
        }
    }

    [Test]
    public async Task RedisDown_TheStoreShowsThisProcesssTabs_AndLeavingDoesNotThrow()
    {
        var store = new RedisPresenceStore(DownRedis(), new ConfigurationBuilder().Build(), TimeProvider.System, NullLogger<RedisPresenceStore>.Instance);
        PresenceTab[] tabs = [new("a", null, null, "/")];

        var all = await store.Exchange(tabs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Is.SameAs(tabs));
            Assert.That(async () => await store.Leave(), Throws.Nothing);
        }
    }

    private LiveDelivery Delivery(IServiceScopeFactory? scopes = null, AppRedis? redis = null, IConfiguration? configuration = null)
        => new(_watchers, _hubContext, scopes ?? new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LiveDelivery>.Instance, configuration ?? new ConfigurationBuilder().Build(), redis);

    /// <summary>A Redis whose reads succeed (nothing cached yet); its first write (the lock) succeeds, its second (the
    /// data, once a build is done) fails with <paramref name="storeFailure"/> (a <see cref="RedisException"/> or a
    /// <see cref="TimeoutException"/>). Sequential by call order. The callbacks return <c>bool</c> (NSubstitute's
    /// Task-unwrapping overload - the same form <c>LiveSnapshots.For</c> doubles elsewhere in this file use to throw);
    /// three earlier versions passed already-built <c>Task&lt;bool&gt;</c> values instead (the plain generic overload,
    /// not the Task-aware one) and the lock was never actually won in any of them.</summary>
    private static AppRedis FlakyStoreRedis(Exception storeFailure)
    {
        var db = Substitute.For<IDatabase>();
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(_ => true, _ => throw storeFailure);
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        return new AppRedis(_ => Task.FromResult(connection));
    }

    /// <summary>A Redis whose first read is a cache miss, whose lock is already taken (by "someone else"), and whose
    /// second read (WaitForBuild's) finds the data: no real Redis latency, so a real <c>Task.Delay</c> before the first
    /// check (the bug) is the only thing that could make this slow.</summary>
    private static AppRedis LockLostRedis(RedisValue data)
    {
        var db = Substitute.For<IDatabase>();
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null, data);
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>(), Arg.Any<CommandFlags>()).Returns(false);
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        return new AppRedis(_ => Task.FromResult(connection));
    }

    /// <summary>The pages watching player:5 here, told through <paramref name="told"/> once they are.</summary>
    private ILiveHubClient WatchingPlayerFive(TaskCompletionSource told)
    {
        var watching = Substitute.For<ILiveHubClient>();
        watching.Changed(Arg.Any<string>()).Returns(_ =>
        {
            told.TrySetResult();
            return Task.CompletedTask;
        });
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watching);
        _watchers.Watch(LiveTopics.Player(5), "conn", SeasonViewer.Public);
        return watching;
    }

    [Test]
    public async Task ThePublisher_Running_SendsAChangeOnceItsWindowIsOver()
    {
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watching = WatchingPlayerFive(told);
        var live = new LiveUpdates(NullLogger<LiveUpdates>.Instance, delivery: Delivery());

        await live.StartAsync(CancellationToken.None);
        live.Publish([new LiveChange(LiveTopics.Player(5))]);
        await told.Task.WaitAsync(Wait);
        await live.StopAsync(CancellationToken.None);

        await watching.Received(1).Changed(LiveTopics.Player(5));
    }

    [Test]
    public async Task ThePublisher_RedisDown_TellsThisProcesssPagesAtLeast()
    {
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watching = WatchingPlayerFive(told);
        var live = new LiveUpdates(NullLogger<LiveUpdates>.Instance, DownRedis(), Delivery());

        live.Publish([new LiveChange(LiveTopics.Player(5))]);
        await live.SendPendingAsync();

        await watching.Received(1).Changed(LiveTopics.Player(5));
    }

    [Test]
    public async Task APageThatCantBeTold_IsLogged_AndTheDeliveryGoesOn()
    {
        var watching = Substitute.For<ILiveHubClient>();
        watching.Changed(Arg.Any<string>()).Returns(Task.FromException(new IOException("connection gone")));
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watching);
        _watchers.Watch(LiveTopics.Player(5), "conn", SeasonViewer.Public);

        Assert.That(async () => await Delivery().Deliver([new LiveChange(LiveTopics.Player(5))], ct: CancellationToken.None), Throws.Nothing);
        await watching.Received(1).Changed(LiveTopics.Player(5));
    }

    [Test]
    public async Task Delivery_RedisUnreachable_StillBuildsItsOwnData()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(new QueueLiveDto(3));
        _watchers.Watch(LiveTopics.Queue, "conn", SeasonViewer.Public);

        // A change ID, as it would come scaled out through the relay: with Redis unreachable, caching across pods is
        // skipped and this pod builds it anyway, rather than leave its own watchers with nothing
        await Delivery(Scopes(snapshots), DownRedis()).Deliver([new LiveChange(LiveTopics.Queue)], Guid.NewGuid());

        await _everyone.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":3}");
    }

    [TestCase(typeof(RedisException))]
    [TestCase(typeof(TimeoutException))]
    public async Task AStoreFailureAfterABuild_ReturnsTheDataAlreadyBuilt_RatherThanBuildingAgain(Type exceptionType)
    {
        var built = 0;
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Interlocked.Increment(ref built);
            return Task.FromResult<object?>(new QueueLiveDto(5));
        });
        _watchers.Watch(LiveTopics.Queue, "conn", SeasonViewer.Public);
        var storeFailure = (Exception)Activator.CreateInstance(exceptionType, "could not store")!;

        await Delivery(Scopes(snapshots), FlakyStoreRedis(storeFailure)).Deliver([new LiveChange(LiveTopics.Queue)], Guid.NewGuid());

        using (Assert.EnterMultipleScope())
        {
            await _everyone.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":5}");
            Assert.That(built, Is.EqualTo(1), "a store failure after a successful build must not cost a second one");
        }
    }

    [Test]
    public async Task WaitForBuild_ChecksTheCacheBeforeSleeping_SoAnAlreadyFinishedBuildIsNotDelayed()
    {
        _watchers.Watch(LiveTopics.Queue, "conn", SeasonViewer.Public);
        var redis = LockLostRedis("{\"count\":3}");

        // Lost the lock race (LockLostRedis), then found the data on WaitForBuild's own first check. A real Task.Delay
        // runs regardless of mocking, so a check-then-sleep bug would show up here as a genuine ~100ms (PollInterval).
        var sw = Stopwatch.StartNew();
        await Delivery(redis: redis).Deliver([new LiveChange(LiveTopics.Queue)], Guid.NewGuid());
        sw.Stop();

        using (Assert.EnterMultipleScope())
        {
            await _everyone.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":3}");
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(50)), "found on the first check, not after a sleep");
        }
    }
}
