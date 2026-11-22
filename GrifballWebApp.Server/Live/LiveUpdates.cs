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
    /// What goes out on <see cref="RelayChannel"/>: the changes, and this publish's own ID. The ID, not the changes'
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

    /// <summary>The changes in a message of <see cref="RelayChannel"/>; none for one that isn't one.</summary>
    public static List<LiveChange> Parse(string? message) => TryParse(message)?.Changes ?? [];

    /// <summary>
    /// The change a message of <see cref="RelayChannel"/> is: the same on every pod that gets it, and different from every
    /// other message's, whatever its changes (<see cref="LiveDelivery.Deliver"/>'s cache key); null for one that isn't one.
    /// </summary>
    public static Guid? ChangeIDOf(string? message) => TryParse(message)?.ChangeID;

    private static Relayed? TryParse(string? message)
    {
        try
        {
            return message is null ? null : JsonSerializer.Deserialize<Relayed>(message);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Scaled out, on an API process: the changes any process published (<see cref="LiveUpdates.RelayChannel"/>), to this one's pages.</summary>
public sealed class LiveRelayListener(LiveDelivery delivery, AppRedis? redis = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Deliver logs its own failures (a build, a send) and isn't canceled here, so it doesn't throw. Every pod gets the
        // same message, so the same change ID (LiveUpdates.ChangeIDOf): the first to build a topic's data for it keeps the
        // others from building their own (LiveDelivery.Deliver).
        redis?.WhenConnected(connection => connection.GetSubscriber().Subscribe(LiveUpdates.RelayChannel)
            .OnMessage(message => delivery.Deliver(LiveUpdates.Parse(message.Message), LiveUpdates.ChangeIDOf(message.Message), CancellationToken.None)));
        return Task.CompletedTask;
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
/// On an API process: tells this process's pages watching each changed topic. A topic many watch at once comes with its
/// data (<see cref="LiveSnapshots"/>), built once here for all of them (once a viewer where it differs by viewer), as
/// the API gives it (<see cref="ApiJson"/>); the rest are notices, and their pages read again. A topic nobody here watches
/// costs nothing. Data that can't be built is a notice instead.
/// </summary>
/// <remarks>
/// Scaled out, every pod that watches a topic gets the same relayed change and would build the same data: the first here
/// for a given <c>changeID</c> (the same on every pod for that change, never another's: <see cref="LiveUpdates.ChangeIDOf"/>)
/// keeps it in Redis a few seconds (<see cref="DataTtl"/>) under a lock (<see cref="LockTtl"/>, held well past
/// <see cref="BuildWait"/> so a slow build is never joined by a second pod taking a freshly-expired lock); the rest read it
/// instead of building their own. A pod that can't take the lock in time, or can't reach Redis at all, builds its own
/// rather than keep its watchers waiting; a build that fails outright is never cached as "nothing to show", so the next
/// pod gets a real attempt. A call with no <c>changeID</c> (this pod's own local delivery, never shared with another)
/// always builds its own, as does every pod when Redis isn't configured. Every topic (and viewer) builds and sends
/// alongside the others, so one pod's wait on one topic never delays another's.
/// </remarks>
public sealed class LiveDelivery(LiveWatchers watchers, IHubContext<LiveHub, ILiveHubClient> hub, IServiceScopeFactory scopes,
    ILogger<LiveDelivery> logger, IConfiguration configuration, AppRedis? redis = null)
{
    private static readonly JsonSerializerOptions Json = ApiJson.Options();

    /// <summary>How long a topic's built data is kept for the other pods to read.</summary>
    public static readonly TimeSpan DataTtl = TimeSpan.FromSeconds(5);

    /// <summary>How long a pod that lost the race for the lock waits for the winner's data before building its own.</summary>
    public static readonly TimeSpan BuildWait = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long one pod holds the lock to build: well past <see cref="BuildWait"/>, so every pod that lost the race gives
    /// up and builds its own before a late-arriving pod could ever find the lock expired and take a fresh one, building
    /// the same change again alongside a winner that is simply slow.
    /// </summary>
    public static readonly TimeSpan LockTtl = BuildWait + TimeSpan.FromSeconds(10);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

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
        foreach (var topic in topics.Distinct(StringComparer.Ordinal))
        {
            var watching = watchers.Of(topic);
            if (watching.Count == 0)
                continue;
            if (!LiveSnapshots.Pushes(topic))
            {
                sends.Add(Send(watching.Select(w => w.ConnectionID).ToList(), topic, null));
                continue;
            }
            var byViewer = LiveSnapshots.DependsOnViewer(topic)
                ? watching.GroupBy(w => w.Viewer).Select(g => (Viewer: g.Key, Connections: g.Select(w => w.ConnectionID).ToList())).ToList()
                : [(watching[0].Viewer, watching.Select(w => w.ConnectionID).ToList())];
            // Every topic (and viewer) alongside the rest: a lock lost on one never delays the others' builds and sends
            foreach (var (viewer, connections) in byViewer)
                sends.Add(BuildAndSend(topic, viewer, connections, changeID, ct));
        }
        await Task.WhenAll(sends);
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
    /// <paramref name="changeID"/> or no Redis; otherwise shared with every other pod building the same change (see the
    /// class remarks).
    /// </summary>
    private async Task<string?> Build(string topic, SeasonViewer viewer, Guid? changeID, CancellationToken ct)
    {
        if (redis is null || changeID is null)
            return (await BuildNow(topic, viewer, ct)).Data;
        var key = CacheKey(topic, viewer, changeID.Value);
        IDatabase db;
        try
        {
            db = (await redis.ConnectAsync()).GetDatabase();
            var cached = await db.StringGetAsync(key);
            if (!cached.IsNull)
                return Decode(cached);
            if (!await db.StringSetAsync($"{key}:lock", RedisValue.EmptyString, LockTtl, When.NotExists))
                return await WaitForBuild(db, key, topic, viewer, ct);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not build {Topic}'s data once across the pods: building it here", topic);
            return (await BuildNow(topic, viewer, ct)).Data;
        }

        // Won the lock: build regardless of what Redis does from here. A failed build is never cached as "nothing to
        // show" (the lock is dropped instead, so the next pod gets a real attempt); a store failure after a successful
        // build must never cost a second one, so it's caught on its own and the data already built is still returned.
        var built = await BuildNow(topic, viewer, ct);
        try
        {
            if (built.Failed)
                await db.KeyDeleteAsync($"{key}:lock");
            else
                await db.StringSetAsync(key, Encode(built.Data), DataTtl);
            return built.Data;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Built {Topic}'s data but could not cache it for the other pods", topic);
            return built.Data;
        }
    }

    /// <summary>
    /// Waits for the pod that is building <paramref name="topic"/>'s data under <paramref name="key"/>; builds it here once
    /// <see cref="BuildWait"/> passes without it (that pod may be gone, or its build failed), rather than keep this pod's
    /// watchers waiting. Checks before every sleep, so a build that already finished is never delayed a whole poll for it.
    /// </summary>
    private async Task<string?> WaitForBuild(IDatabase db, string key, string topic, SeasonViewer viewer, CancellationToken ct)
    {
        using var giveUp = new CancellationTokenSource(BuildWait);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, giveUp.Token);
        try
        {
            while (true)
            {
                var cached = await db.StringGetAsync(key);
                if (!cached.IsNull)
                    return Decode(cached);
                await Task.Delay(PollInterval, linked.Token);
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
    private string CacheKey(string topic, SeasonViewer viewer, Guid changeID) => AppRedisExtensions.Key(configuration, "livebuild",
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
