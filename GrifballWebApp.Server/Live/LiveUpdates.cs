using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Infrastructure.Redis;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace GrifballWebApp.Server.Live;

/// <summary>
/// The pages are told what changed (<see cref="ILiveUpdates"/>), from any process. Changes wait <see cref="Window"/> and go
/// out once a topic: a save that changes a season twice, or an import that changes it a thousand times, is one. A user's
/// own (their queue place) goes straight to their connections, through the backplane. The rest go to the API processes,
/// where the pages' connections are (<see cref="LiveDelivery"/>): through Redis when scaled out (<see cref="RelayChannel"/>,
/// <see cref="LiveRelayListener"/>), else to this process's own. Publishing never waits; a failure to send is logged: the
/// change is made, and a page loads it again whenever its connection comes back.
/// </summary>
public sealed class LiveUpdates(IHubContext<LiveHub, ILiveHubClient> hub, ILogger<LiveUpdates> logger, AppRedis? redis = null,
    LiveDelivery? delivery = null) : BackgroundService, ILiveUpdates
{
    /// <summary>How long changes gather before they go out.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(250);

    /// <summary>The channel (under the app's prefix) the changed topics are published on, for every API process.</summary>
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
        while (await _signal.Reader.WaitToReadAsync(stoppingToken))
        {
            _signal.Reader.TryRead(out _);
            await Task.Delay(Window, stoppingToken);
            await SendPendingAsync(stoppingToken);
        }
    }

    /// <summary>Sends what gathered so far: each user's to their connections, the topics to the API processes.</summary>
    public async Task SendPendingAsync(CancellationToken ct = default)
    {
        HashSet<LiveChange> changes;
        lock (_gate)
        {
            changes = _pending;
            _pending = [];
        }
        foreach (var change in changes.Where(c => c.UserID is not null))
        {
            try
            {
                await hub.Clients.User(change.UserID!.Value.ToString(CultureInfo.InvariantCulture)).Changed(change.Topic);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not tell user {UserID} that {Topic} changed", change.UserID, change.Topic);
            }
        }
        var topics = changes.Where(c => c.UserID is null).Select(c => c.Topic).Distinct(StringComparer.Ordinal).ToList();
        if (topics.Count == 0)
            return;
        if (redis is not null)
        {
            try
            {
                await (await redis.ConnectAsync()).GetSubscriber().PublishAsync(RelayChannel, JsonSerializer.Serialize(topics));
                return;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                // This process's own pages, at least
                logger.LogWarning(ex, "Could not tell the other processes that {Topics} changed", topics);
            }
        }
        if (delivery is not null)
            await delivery.Deliver(topics, ct);
    }

    /// <summary>The topics in a message of <see cref="RelayChannel"/>; none for one that isn't a list of them.</summary>
    public static List<string> Parse(string? message)
    {
        try
        {
            return message is null ? [] : JsonSerializer.Deserialize<List<string>>(message) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>Scaled out, on an API process: the topics any process published (<see cref="LiveUpdates.RelayChannel"/>), to this one's pages.</summary>
public sealed class LiveRelayListener(LiveDelivery delivery, ILogger<LiveRelayListener> logger, AppRedis? redis = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        redis?.WhenConnected(connection => connection.GetSubscriber().Subscribe(LiveUpdates.RelayChannel).OnMessage(async message =>
        {
            try
            {
                await delivery.Deliver(LiveUpdates.Parse(message.Message), CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not tell this process's pages what changed");
            }
        }));
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
}

/// <summary>
/// On an API process: tells this process's pages watching each changed topic. A topic many watch at once comes with its
/// data (<see cref="LiveSnapshots"/>), built once here for all of them (once a viewer where it differs by viewer), as
/// the API gives it (<see cref="ApiJson"/>); the rest are notices, and their pages read again. A topic nobody here watches
/// costs nothing. Data that can't be built is a notice instead.
/// </summary>
public sealed class LiveDelivery(LiveWatchers watchers, IHubContext<LiveHub, ILiveHubClient> hub, IServiceScopeFactory scopes, ILogger<LiveDelivery> logger)
{
    private static readonly JsonSerializerOptions Json = ApiJson.Options();

    public async Task Deliver(IReadOnlyCollection<string> topics, CancellationToken ct = default)
    {
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

    private async Task<string?> Build(string topic, SeasonViewer viewer, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
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
