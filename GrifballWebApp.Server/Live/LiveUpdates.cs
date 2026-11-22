using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GrifballWebApp.Server.Live;

/// <summary>
/// The pages are told what changed (<see cref="ILiveUpdates"/>), from any process. Changes wait <see cref="Window"/> and go
/// out once a topic: a save that changes a season twice, or an import that changes it a thousand times, is one. They go to
/// the API processes, where the pages' connections are (<see cref="LiveDelivery"/>), a user's own (their queue place)
/// with them: through Redis when scaled out (<see cref="RelayChannel"/>, <see cref="LiveRelayListener"/>), else to this
/// process's own. Nothing here uses the hub, so a worker never joins its backplane channels. Publishing never waits; a failure to send is logged: the
/// change is made, and a page loads it again whenever its connection comes back.
/// </summary>
public sealed class LiveUpdates(ILogger<LiveUpdates> logger, AppRedis? redis = null, LiveDelivery? delivery = null) : BackgroundService, ILiveUpdates
{
    /// <summary>How long changes gather before they go out.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(250);

    /// <summary>The channel (under the app's prefix) the changes are published on, for every API process.</summary>
    public static readonly RedisChannel RelayChannel = RedisChannel.Literal("live-changed");

    /// <summary>
    /// What goes out on <see cref="RelayChannel"/> now: the changes, and this publish's own ID. The ID, not the changes'
    /// content, is this change's identity (<see cref="LiveDelivery.Deliver"/>'s cache key): two publishes with the very
    /// same topics (two draft picks moments apart, both touching <c>draft:4</c>) are still two changes, each its own build.
    /// </summary>
    private sealed record Relayed(Guid ChangeID, List<LiveChange> Changes);

    private readonly Lock _gate = new();
    private HashSet<LiveChange> _pending = [];
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Publish(IEnumerable<LiveChange> changes)
    {
        lock (_gate)
        {
            var before = _pending.Count;
            _pending.UnionWith(changes);
            if (_pending.Count == before)
                return;
        }
        _signal.Writer.TryWrite(true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await NextAsync(stoppingToken))
            await SendPendingAsync(stoppingToken);
    }

    /// <summary>Waits for a change, then for its window to close; false once the process stops.</summary>
    private async Task<bool> NextAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _signal.Reader.WaitToReadAsync(stoppingToken);
            _signal.Reader.TryRead(out _);
            await Task.Delay(Window, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Sends what gathered so far to the API processes, as one change of its own (<see cref="Relayed.ChangeID"/>).</summary>
    public async Task SendPendingAsync(CancellationToken ct = default)
    {
        HashSet<LiveChange> changes;
        lock (_gate)
        {
            changes = _pending;
            _pending = [];
        }
        if (changes.Count == 0)
            return;
        var list = changes.ToList();
        if (redis is not null)
        {
            try
            {
                await (await redis.ConnectAsync()).GetSubscriber().PublishAsync(RelayChannel, Serialize(Guid.NewGuid(), list));
                return;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                // This process's own pages, at least
                logger.LogWarning(ex, "Could not tell the other processes that {Topics} changed", list.Select(c => c.Topic));
            }
        }
        if (delivery is not null)
            await delivery.Deliver(list, ct: ct);
    }

    /// <summary>A message of <see cref="RelayChannel"/> for <paramref name="changeID"/>'s change (a test's own, or this class's).</summary>
    public static string Serialize(Guid changeID, IReadOnlyCollection<LiveChange> changes) => JsonSerializer.Serialize(new Relayed(changeID, [.. changes]));

    /// <summary>The changes in a message of <see cref="RelayChannel"/>; none for one that's neither shape <see cref="ParseRelayed"/> knows.</summary>
    public static List<LiveChange> Parse(string? message) => ParseRelayed(message)?.Changes ?? [];

    /// <summary>
    /// The change a message of <see cref="RelayChannel"/> is: the same on every pod that gets it, and different from every
    /// other message's, whatever its changes (<see cref="LiveDelivery.Deliver"/>'s cache key); null for one with none - an
    /// older message (before this went out, or a worker still on an older image mid-rollout) as well as one that's neither
    /// shape <see cref="ParseRelayed"/> knows.
    /// </summary>
    public static Guid? ChangeIDOf(string? message) => ParseRelayed(message)?.ChangeID;

    /// <summary>
    /// A relayed message's changes and its own ID, parsed once for both (a caller after only one of them still parses
    /// once, through <see cref="Parse"/> or <see cref="ChangeIDOf"/> alone). Accepts the current shape (an object with its
    /// own ID) and the older one from before this (a plain array, so it always builds its own - there's no ID to share a
    /// build by); null for a message that's neither, which a caller with somewhere to log it should.
    /// </summary>
    public static RelayedMessage? ParseRelayed(string? message)
    {
        if (message is null)
            return new RelayedMessage([], null);
        try
        {
            var envelope = JsonSerializer.Deserialize<Relayed>(message);
            return new RelayedMessage(envelope?.Changes ?? [], envelope?.ChangeID);
        }
        catch (JsonException)
        {
            try
            {
                // No change ID in this shape: it always builds its own, never shares another pod's cached build. Never
                // actually null: the only JSON that deserializes to null without throwing, for any type, is the literal
                // "null" token - and that already returns above, deserializing successfully as the other shape first.
                return new RelayedMessage(JsonSerializer.Deserialize<List<LiveChange>>(message)!, null);
            }
            catch (JsonException)
            {
                // Neither shape: the caller logs it, with the message itself
                return null;
            }
        }
    }
}

/// <summary>A relayed message's own changes, and its own ID when it has one (<see cref="LiveUpdates.ParseRelayed"/>).</summary>
public sealed record RelayedMessage(List<LiveChange> Changes, Guid? ChangeID);

/// <summary>Scaled out, on an API process: the changes any process published (<see cref="LiveUpdates.RelayChannel"/>), to this one's pages.</summary>
public sealed class LiveRelayListener(LiveDelivery delivery, ILogger<LiveRelayListener> logger, AppRedis? redis = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Every message delivered in the background, not awaited here: this subscription's own queue would otherwise
        // hold the next message back for however long this one's slowest topic takes (a lost lock race, waiting out
        // BuildWait). LiveDelivery.Deliver still keeps each topic's own changes in the order they were relayed.
        redis?.WhenConnected(connection => connection.GetSubscriber().Subscribe(LiveUpdates.RelayChannel)
            .OnMessage(message =>
            {
                _ = DeliverAsync(message.Message);
                return Task.CompletedTask;
            }));
        return Task.CompletedTask;
    }

    private async Task DeliverAsync(string? message)
    {
        var relayed = LiveUpdates.ParseRelayed(message);
        if (relayed is null)
        {
            logger.LogWarning("A relayed change was neither the current shape nor the older one: {Message}", message);
            return;
        }
        try
        {
            await delivery.Deliver(relayed.Changes, relayed.ChangeID, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not deliver a relayed change");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// This process's <see cref="LiveHub"/> connections: those watching each topic (<see cref="LiveHub.Watch"/>), and as which
/// viewer, and each signed-in user's (a user's own changes go to theirs, not to their other hubs' connections).
/// </summary>
public sealed class LiveWatchers
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Dictionary<string, SeasonViewer>> _byTopic = new(StringComparer.Ordinal);
    private readonly Dictionary<int, HashSet<string>> _byUser = [];
    private readonly Dictionary<string, int> _userOf = new(StringComparer.Ordinal);

    /// <summary>A connection opened, <paramref name="userID"/>'s when signed in.</summary>
    public void Connected(string connectionID, int? userID)
    {
        if (userID is not int id)
            return;
        lock (_gate)
        {
            _userOf[connectionID] = id;
            if (!_byUser.TryGetValue(id, out var theirs))
                _byUser[id] = theirs = new(StringComparer.Ordinal);
            theirs.Add(connectionID);
        }
    }

    public void Disconnected(string connectionID)
    {
        lock (_gate)
        {
            if (_userOf.Remove(connectionID, out var id) && _byUser.TryGetValue(id, out var theirs) && theirs.Remove(connectionID) && theirs.Count == 0)
                _byUser.Remove(id);
        }
    }

    /// <summary>A signed-in user's connections here.</summary>
    public IReadOnlyList<string> OfUser(int userID)
    {
        lock (_gate)
            return _byUser.TryGetValue(userID, out var theirs) ? [.. theirs] : [];
    }

    public void Watch(string topic, string connectionID, SeasonViewer viewer)
    {
        lock (_gate)
        {
            if (!_byTopic.TryGetValue(topic, out var watching))
                _byTopic[topic] = watching = new(StringComparer.Ordinal);
            watching[connectionID] = viewer;
        }
    }

    public void Unwatch(string topic, string connectionID)
    {
        lock (_gate)
        {
            if (_byTopic.TryGetValue(topic, out var watching) && watching.Remove(connectionID) && watching.Count == 0)
                _byTopic.Remove(topic);
        }
    }

    /// <summary>The connections watching <paramref name="topic"/> here, with their viewers.</summary>
    public IReadOnlyList<(string ConnectionID, SeasonViewer Viewer)> Of(string topic)
    {
        lock (_gate)
            return _byTopic.TryGetValue(topic, out var watching) ? [.. watching.Select(w => (w.Key, w.Value))] : [];
    }

    /// <summary>The users whose profiles (<see cref="LiveTopics.Player"/>) are watched here.</summary>
    public IReadOnlyList<int> Players()
    {
        lock (_gate)
            return [.. _byTopic.Keys.Select(LiveTopics.Split)
                .Where(t => t.Kind == LiveTopics.PlayerKind && int.TryParse(t.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                .Select(t => int.Parse(t.Key!, NumberStyles.None, CultureInfo.InvariantCulture))];
    }
}

/// <summary>
/// <see cref="LiveDelivery"/>'s tuning (the <c>Live</c> configuration section), checked at startup
/// (<see cref="LiveServiceCollectionExtensions.AddValidatedLiveOptions"/>): every limit and duration positive, and the
/// build lock outliving the wait for it (<see cref="LockTtl"/> greater than <see cref="BuildWait"/>) - otherwise a late
/// pod could find an already-expired lock and build the very change a slower winner is still building. Defaults match
/// what shipped before these were configurable.
/// </summary>
public sealed class LiveOptions
{
    public const string Section = "Live";

    /// <summary>The defaults, for code (mainly tests) that wants them without going through configuration.</summary>
    public static readonly LiveOptions Default = new();

    /// <summary>How many of a change's topics (and viewers) build at once (see <see cref="LiveDelivery"/>'s remarks).</summary>
    public int MaxConcurrentBuilds { get; set; } = 4;

    /// <summary>How long a topic's built data is kept in Redis for the other pods to read.</summary>
    public TimeSpan DataTtl { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a pod that lost the lock race waits for the winner's data before building its own.</summary>
    public TimeSpan BuildWait { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>How long one pod holds the build lock: a dead-builder safety net (see <see cref="LiveDelivery"/>'s
    /// remarks), not the happy path's hold time.</summary>
    public TimeSpan LockTtl { get; set; } = TimeSpan.FromSeconds(13);

    /// <summary>
    /// Whether a change's data is built once and shared across pods through Redis. False (every pod builds its own -
    /// the behavior before this setting existed) still relays changes between pods through Redis
    /// (<see cref="LiveUpdates.RelayChannel"/>); only the build lock and the shared data keys go untouched.
    /// </summary>
    public bool SharedBuilds { get; set; } = true;
}

public static class LiveServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="LiveOptions"/> from the <see cref="LiveOptions.Section"/> section, checked at startup: every limit and
    /// duration positive, and the build lock outliving the wait for it.
    /// </summary>
    public static IServiceCollection AddValidatedLiveOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LiveOptions>()
            .Bind(configuration.GetSection(LiveOptions.Section))
            .Validate(options =>
            {
                var errors = new List<string>();
                if (options.MaxConcurrentBuilds < 1)
                    errors.Add("MaxConcurrentBuilds must be at least 1");
                if (options.DataTtl <= TimeSpan.Zero)
                    errors.Add("DataTtl must be positive");
                if (options.BuildWait <= TimeSpan.Zero)
                    errors.Add("BuildWait must be positive");
                if (options.LockTtl <= TimeSpan.Zero)
                    errors.Add("LockTtl must be positive");
                if (options.LockTtl <= options.BuildWait)
                    errors.Add("LockTtl must be greater than BuildWait");

                if (errors.Count > 0)
                    throw new ArgumentException(string.Join(", ", errors));

                return true;
            })
            .ValidateOnStart();
        return services;
    }
}

/// <summary>
/// On an API process: tells this process's pages watching each changed topic. A topic many watch at once comes with its
/// data (<see cref="LiveSnapshots"/>), built once here for all of them (once a viewer where it differs by viewer), as
/// the API gives it (<see cref="ApiJson"/>); the rest are notices, and their pages read again. A topic nobody here watches
/// costs nothing. Data that can't be built is a notice instead.
/// </summary>
/// <remarks>
/// Scaled out, every pod that watches a topic gets the same relayed change and would build the same data: the first here
/// for a given <c>changeID</c> (the same on every pod for that change, never another's: <see cref="LiveUpdates.ChangeIDOf"/>)
/// takes a lock under an owner token of its own (<see cref="LiveOptions.LockTtl"/>, a safety net for one that dies
/// mid-build, well past <see cref="LiveOptions.BuildWait"/> so a late pod never finds it freshly expired while that pod
/// is simply slow), builds, keeps the result in Redis a few seconds for the rest to read
/// (<see cref="LiveOptions.DataTtl"/>), and releases its lock right away - by a compare-and-delete on its own token, so a
/// build that outlives its lock (and so outlives being anyone's responsibility) never deletes a different pod's. A build
/// that fails outright is never cached as "nothing to show" (the lock is released without storing, so the next pod gets
/// a real attempt); a pod that finds neither a build nor a lock while waiting takes its own shot immediately rather than
/// wait out the rest of <see cref="LiveOptions.BuildWait"/> for nothing. A pod that can't take the lock in time, or can't
/// reach Redis at all, builds its own rather than keep its watchers waiting. A call with no <c>changeID</c> (this pod's
/// own local delivery, never shared with another) always builds its own, as does every pod when Redis isn't configured
/// or <see cref="LiveOptions.SharedBuilds"/> is false. Every topic (and viewer) a change touches builds and sends
/// alongside the others (up to <see cref="LiveOptions.MaxConcurrentBuilds"/> at once), so one pod's wait on one topic
/// never delays another's; two changes to the very same topic still reach its watchers in the order they were relayed
/// (<see cref="EnqueueForTopic"/>).
/// </remarks>
public sealed class LiveDelivery(LiveWatchers watchers, IHubContext<LiveHub, ILiveHubClient> hub, IServiceScopeFactory scopes,
    ILogger<LiveDelivery> logger, IConfiguration configuration, AppRedis? redis = null, TimeProvider? time = null, IOptions<LiveOptions>? liveOptions = null)
{
    private static readonly JsonSerializerOptions Json = ApiJson.Options();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly LiveOptions _options = liveOptions?.Value ?? LiveOptions.Default;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Deletes the lock at KEYS[1] only if it still holds the value this pod set it to (ARGV[1]): a compare-and-
    /// delete, so a release that outlives its own lock's TTL never takes a different pod's newer one.</summary>
    private const string ReleaseLockScript = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    private readonly Lock _topicQueueGate = new();
    private readonly Dictionary<string, Task> _topicQueues = new(StringComparer.Ordinal);

    public async Task Deliver(IReadOnlyCollection<LiveChange> changes, Guid? changeID = null, CancellationToken ct = default)
    {
        var sends = new List<Task>();
        // A user's own: to their connections here (each API process tells its own, so each connection hears it once)
        foreach (var change in changes.Where(c => c.UserID is not null))
        {
            var theirs = watchers.OfUser(change.UserID!.Value);
            if (theirs.Count > 0)
                sends.Add(Send(theirs, change.Topic, null));
        }
        var topics = changes.Where(c => c.UserID is null).Select(c => c.Topic).ToList();
        topics.AddRange(await ProfilesOfAccounts(topics, ct));
        var toBuild = new List<(string Topic, SeasonViewer Viewer, IReadOnlyList<string> Connections)>();
        foreach (var topic in topics.Distinct(StringComparer.Ordinal))
        {
            var watching = watchers.Of(topic);
            if (watching.Count == 0)
                continue;
            if (!LiveSnapshots.Pushes(topic))
            {
                sends.Add(EnqueueForTopic(topic, SeasonViewer.Public, () => Send(watching.Select(w => w.ConnectionID).ToList(), topic, null)));
                continue;
            }
            var byViewer = LiveSnapshots.DependsOnViewer(topic)
                ? watching.GroupBy(w => w.Viewer).Select(g => (Viewer: g.Key, Connections: g.Select(w => w.ConnectionID).ToList())).ToList()
                : [(watching[0].Viewer, watching.Select(w => w.ConnectionID).ToList())];
            toBuild.AddRange(byViewer.Select(bv => (topic, bv.Viewer, (IReadOnlyList<string>)bv.Connections)));
        }
        if (toBuild.Count > 0)
            sends.Add(Parallel.ForEachAsync(toBuild, new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrentBuilds, CancellationToken = ct },
                (item, token) => new ValueTask(EnqueueForTopic(item.Topic, item.Viewer,
                    () => BuildAndSend(item.Topic, item.Viewer, item.Connections, changeID, token)))));
        await Task.WhenAll(sends);
    }

    /// <summary>
    /// Chains <paramref name="work"/> after whatever is already queued for <paramref name="topic"/> (and
    /// <paramref name="viewer"/>, for one whose data depends on it), so two changes to the same topic are always built
    /// and sent in the order they were relayed - even though different topics, and different relayed messages, never
    /// wait on one another. A failed item never stops the next one queued behind it (<see cref="Chain"/>); internal so
    /// a test can exercise that directly, rather than contrive a failure through the whole public <see cref="Deliver"/>.
    /// </summary>
    internal Task EnqueueForTopic(string topic, SeasonViewer viewer, Func<Task> work)
    {
        var key = LiveSnapshots.DependsOnViewer(topic) ? $"{topic}:{viewer}" : topic;
        lock (_topicQueueGate)
        {
            var previous = _topicQueues.TryGetValue(key, out var pending) ? pending : Task.CompletedTask;
            var next = Chain(previous, work);
            _topicQueues[key] = next;
            return next;
        }
    }

    private static async Task Chain(Task previous, Func<Task> work)
    {
        try
        {
            await previous;
        }
        catch
        {
            // A previous item's own failure is already logged where it happened; it must never stop this one
        }
        await work();
    }

    private async Task BuildAndSend(string topic, SeasonViewer viewer, IReadOnlyList<string> connections, Guid? changeID, CancellationToken ct)
        => await Send(connections, topic, await Build(topic, viewer, changeID, ct));

    /// <summary>
    /// The profiles watched here (<see cref="LiveTopics.Player"/>) of the users whose Xbox accounts changed (a Halo game of
    /// theirs, an account's gamertag): who plays as an account is the users' to say. One lookup for the batch, none when
    /// no profile is watched here; one that fails is logged, and those profiles hear of it on their next change.
    /// </summary>
    private async Task<IEnumerable<string>> ProfilesOfAccounts(IReadOnlyCollection<string> topics, CancellationToken ct)
    {
        var accounts = topics.Select(LiveTopics.Split)
            .Where(t => t.Kind == LiveTopics.XboxKind && long.TryParse(t.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(t => long.Parse(t.Key!, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToList();
        if (accounts.Count == 0)
            return [];
        var watched = watchers.Players();
        if (watched.Count == 0)
            return [];
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            var users = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Users.AsNoTracking()
                .Where(u => watched.Contains(u.Id) && u.XboxUserID != null && accounts.Contains(u.XboxUserID.Value))
                .Select(u => u.Id)
                .ToListAsync(ct);
            return users.Select(LiveTopics.Player);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not look up whose Xbox accounts changed: their profiles hear of it on their next change");
            return [];
        }
    }

    /// <summary>
    /// <paramref name="topic"/>'s data as <paramref name="viewer"/> sees it: built here alone when there's no
    /// <paramref name="changeID"/>, no Redis, or <see cref="LiveOptions.SharedBuilds"/> is off; otherwise shared with
    /// every other pod building the same change (see the class remarks).
    /// </summary>
    private async Task<string?> Build(string topic, SeasonViewer viewer, Guid? changeID, CancellationToken ct)
    {
        if (redis is null || changeID is null || !_options.SharedBuilds)
            return (await BuildNow(topic, viewer, ct)).Data;
        var key = CacheKey(topic, viewer, changeID.Value);
        try
        {
            var db = (await redis.ConnectAsync()).GetDatabase();
            var cached = await db.StringGetAsync(key);
            if (!cached.IsNull)
                return Decode(cached);
            var owner = NewLockOwner();
            if (await db.StringSetAsync($"{key}:lock", owner, _options.LockTtl, When.NotExists))
                return await BuildAndRelease(db, key, owner, topic, viewer, ct);
            return await WaitForBuild(db, key, topic, viewer, ct);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not build {Topic}'s data once across the pods: building it here", topic);
            return (await BuildNow(topic, viewer, ct)).Data;
        }
    }

    /// <summary>
    /// Builds <paramref name="topic"/>'s data, having already won <paramref name="key"/>'s lock under
    /// <paramref name="owner"/>. Stores the result for the other pods unless the build failed outright (never cached as
    /// "nothing to show"); always releases the lock, by a compare-and-delete on <paramref name="owner"/>, so a release
    /// this late (the build outlived <see cref="LiveOptions.LockTtl"/>) can never take a different pod's newer lock.
    /// </summary>
    private async Task<string?> BuildAndRelease(IDatabase db, string key, string owner, string topic, SeasonViewer viewer, CancellationToken ct)
    {
        var built = await BuildNow(topic, viewer, ct);
        try
        {
            if (!built.Failed)
                await db.StringSetAsync(key, Encode(built.Data), _options.DataTtl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Built {Topic}'s data but could not cache it for the other pods", topic);
        }
        try
        {
            await ReleaseLock(db, key, owner);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not release {Topic}'s build lock: it will expire on its own", topic);
        }
        return built.Data;
    }

    private static Task<RedisResult> ReleaseLock(IDatabase db, string key, string owner)
        => db.ScriptEvaluateAsync(ReleaseLockScript, [$"{key}:lock"], [owner]);

    /// <summary>A token only this attempt at the lock uses, so its release (<see cref="ReleaseLock"/>) can never be
    /// mistaken for a different pod's attempt at the very same key.</summary>
    private static string NewLockOwner() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Waits for the pod building <paramref name="topic"/>'s data under <paramref name="key"/>; builds it here once
    /// <see cref="LiveOptions.BuildWait"/> passes without it (that pod may be gone, or its build failed). Checks before
    /// every sleep, so a build that already finished is never delayed a whole poll for it; a poll that finds neither the
    /// data nor the lock (its builder released without storing, or died before ever taking it) takes a shot at the lock
    /// right away, rather than wait out the rest of <see cref="LiveOptions.BuildWait"/> for nothing.
    /// </summary>
    private async Task<string?> WaitForBuild(IDatabase db, string key, string topic, SeasonViewer viewer, CancellationToken ct)
    {
        using var giveUp = new CancellationTokenSource(_options.BuildWait);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, giveUp.Token);
        try
        {
            while (true)
            {
                var cached = await db.StringGetAsync(key);
                if (!cached.IsNull)
                    return Decode(cached);
                var owner = NewLockOwner();
                if (await db.StringSetAsync($"{key}:lock", owner, _options.LockTtl, When.NotExists))
                    return await BuildAndRelease(db, key, owner, topic, viewer, ct);
                await Task.Delay(PollInterval, _time, linked.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (await BuildNow(topic, viewer, ct)).Data;
        }
    }

    /// <summary>The key a build of <paramref name="topic"/> as <paramref name="viewer"/> sees it, for <paramref name="changeID"/>
    /// alone: a topic that doesn't depend on the viewer (<see cref="LiveSnapshots.DependsOnViewer"/>) shares one key across
    /// viewers, so pods whose watchers happen to differ still share the one build.</summary>
    internal string CacheKey(string topic, SeasonViewer viewer, Guid changeID) => AppRedisExtensions.Key(configuration, "livebuild",
        LiveSnapshots.DependsOnViewer(topic) ? $"{changeID:N}:{topic}:{viewer}" : $"{changeID:N}:{topic}");

    /// <summary>A cached value: empty for a build with nothing to show (never a missing key, which means no one has built yet).</summary>
    private static RedisValue Encode(string? json) => json is null ? RedisValue.EmptyString : json;

    private static string? Decode(RedisValue cached) => cached.IsNullOrEmpty ? null : cached.ToString();

    /// <summary>
    /// A build's own result: its data (null for a topic with nothing to show), and whether it failed outright (an
    /// exception, logged already here, never cached as "nothing to show" - see the class remarks).
    /// </summary>
    private readonly record struct BuildResult(string? Data, bool Failed);

    private async Task<BuildResult> BuildNow(string topic, SeasonViewer viewer, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            var data = await scope.ServiceProvider.GetRequiredService<LiveSnapshots>().For(topic, viewer, ct);
            return new(data is null ? null : JsonSerializer.Serialize(data, data.GetType(), Json), Failed: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not build {Topic}'s data: its pages read it again", topic);
            return new(null, Failed: true);
        }
    }

    private async Task Send(IReadOnlyList<string> connections, string topic, string? data)
    {
        try
        {
            var clients = hub.Clients.Clients(connections);
            await (data is null ? clients.Changed(topic) : clients.Snapshot(topic, data));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not tell the pages watching {Topic} that it changed", topic);
        }
    }
}
