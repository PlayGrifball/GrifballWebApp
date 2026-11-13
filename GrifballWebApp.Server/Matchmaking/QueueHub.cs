using Microsoft.AspNetCore.SignalR;
using TypedSignalR.Client;
using GrifballWebApp.Application.Matchmaking;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// The matchmaking queue, live, for the home page's queue panel: its size for everyone (it is public), and a nudge to a
/// signed-in player whose own place changed (joined or left, matched, their match over, kicked). Nothing is called on
/// it: the page gets the rest over HTTP (<see cref="QueueController"/>). A connection is its user's from the browser's
/// session cookie (SignalR's default user id is the NameIdentifier claim), so the page reconnects on a sign-in or out.
/// </summary>
public class QueueHub : Hub<IQueueHubClient>, IQueueHubServer
{
}

// S4023: empty on purpose, the hub has nothing to call; the typed client and the SignalR page need it to know the hub
#pragma warning disable S4023
/// <summary>Methods that can be invoked on back end server: none (the typed client's hub proxy for it is empty).</summary>
[Hub]
public interface IQueueHubServer;
#pragma warning restore S4023

/// <summary>Methods that can be invoked on front end client (the typed client's receiver)</summary>
[Receiver]
public interface IQueueHubClient
{
    /// <summary>The queue now has this many players.</summary>
    Task QueueCount(int count);

    /// <summary>This player's own status changed: the page loads it again.</summary>
    Task QueueStatusChanged();
}

/// <summary>
/// Through <see cref="QueueHub"/>: from the worker too, as its hub messages reach the API pods' browsers through the
/// backplane. Keeps no state of its own.
/// </summary>
public class QueueNotifier : IQueueNotifier
{
    private readonly IHubContext<QueueHub, IQueueHubClient> _hub;
    private readonly ILogger<QueueNotifier> _logger;

    public QueueNotifier(IHubContext<QueueHub, IQueueHubClient> hub, ILogger<QueueNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task Send(int count)
    {
        try
        {
            await _hub.Clients.All.QueueCount(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send the matchmaking queue's size");
        }
    }

    public async Task SendStatusChanged(IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0)
            return;
        try
        {
            await _hub.Clients.Users(userIds.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList()).QueueStatusChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tell {Count} players their matchmaking status changed", userIds.Count);
        }
    }
}
