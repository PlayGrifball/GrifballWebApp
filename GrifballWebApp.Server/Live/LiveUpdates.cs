using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Infrastructure.Redis;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Sends what gathered so far to the API processes.</summary>
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
                await (await redis.ConnectAsync()).GetSubscriber().PublishAsync(RelayChannel, JsonSerializer.Serialize(list));
                return;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                // This process's own pages, at least
                logger.LogWarning(ex, "Could not tell the other processes that {Topics} changed", list.Select(c => c.Topic));
            }
        }
        if (delivery is not null)
            await delivery.Deliver(list, ct);
    }

    /// <summary>The changes in a message of <see cref="RelayChannel"/>; none for one that isn't a list of them.</summary>
    public static List<LiveChange> Parse(string? message)
    {
        try
        {
            return message is null ? [] : JsonSerializer.Deserialize<List<LiveChange>>(message) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>Scaled out, on an API process: the changes any process published (<see cref="LiveUpdates.RelayChannel"/>), to this one's pages.</summary>
public sealed class LiveRelayListener(LiveDelivery delivery, AppRedis? redis = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Deliver logs its own failures (a build, a send) and isn't canceled here, so it doesn't throw
        redis?.WhenConnected(connection => connection.GetSubscriber().Subscribe(LiveUpdates.RelayChannel)
            .OnMessage(message => delivery.Deliver(LiveUpdates.Parse(message.Message), CancellationToken.None)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>This process's connections watching each topic (<see cref="LiveHub.Watch"/>), and as which viewer.</summary>
public sealed class LiveWatchers
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Dictionary<string, SeasonViewer>> _byTopic = new(StringComparer.Ordinal);

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
public sealed class LiveDelivery(LiveWatchers watchers, HubConnections connections, IHubContext<LiveHub, ILiveHubClient> hub, IServiceScopeFactory scopes,
    ILogger<LiveDelivery> logger)
{
    private static readonly JsonSerializerOptions Json = ApiJson.Options();

    public async Task Deliver(IReadOnlyCollection<LiveChange> changes, CancellationToken ct = default)
    {
        // A user's own: to their connections here (each API process tells its own, so each connection hears it once)
        foreach (var change in changes.Where(c => c.UserID is not null))
        {
            var theirs = connections.SignedIn().Where(c => c.User.UserId == change.UserID).Select(c => c.Context.ConnectionId).ToList();
            if (theirs.Count > 0)
                await Send(theirs, change.Topic, null);
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
                await Send(watching.Select(w => w.ConnectionID).ToList(), topic, null);
                continue;
            }
            var byViewer = LiveSnapshots.DependsOnViewer(topic)
                ? watching.GroupBy(w => w.Viewer).Select(g => (Viewer: g.Key, Connections: g.Select(w => w.ConnectionID).ToList())).ToList()
                : [(watching[0].Viewer, watching.Select(w => w.ConnectionID).ToList())];
            foreach (var (viewer, connections) in byViewer)
                await Send(connections, topic, await Build(topic, viewer, ct));
        }
    }

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
        try
        {
            await using var scope = scopes.CreateAsyncScope();
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

    private async Task<string?> Build(string topic, SeasonViewer viewer, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            var data = await scope.ServiceProvider.GetRequiredService<LiveSnapshots>().For(topic, viewer, ct);
            return data is null ? null : JsonSerializer.Serialize(data, data.GetType(), Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not build {Topic}'s data: its pages read it again", topic);
            return null;
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
