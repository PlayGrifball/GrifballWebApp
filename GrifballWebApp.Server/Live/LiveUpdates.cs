using System.Globalization;
using System.Threading.Channels;
using GrifballWebApp.Application.Live;
using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Live;

/// <summary>
/// The pages are told what changed (<see cref="ILiveUpdates"/>) through <see cref="LiveHub"/>: from any process, as its
/// messages reach every API pod's browsers through the backplane. Changes wait <see cref="Window"/> and go out once a topic:
/// a save that changes a season twice, or an import that changes it a thousand times, is one message to its pages.
/// Publishing never waits, and a failure to send is logged: the change is made, and a page that missed it shows it on its
/// next load (it loads again whenever its connection comes back).
/// </summary>
public sealed class LiveUpdates(IHubContext<LiveHub, ILiveHubClient> hub, ILogger<LiveUpdates> logger) : BackgroundService, ILiveUpdates
{
    /// <summary>How long changes gather before they go out.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(250);

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
            await SendPendingAsync();
        }
    }

    /// <summary>Sends what gathered so far: each topic to its watchers, each user's to their connections.</summary>
    public async Task SendPendingAsync()
    {
        HashSet<LiveChange> changes;
        lock (_gate)
        {
            changes = _pending;
            _pending = [];
        }
        foreach (var change in changes)
        {
            try
            {
                var clients = change.UserID is int userID
                    ? hub.Clients.User(userID.ToString(CultureInfo.InvariantCulture))
                    : hub.Clients.Group(change.Topic);
                await clients.Changed(change.Topic);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not tell the pages watching {Topic} that it changed", change.Topic);
            }
        }
    }
}
