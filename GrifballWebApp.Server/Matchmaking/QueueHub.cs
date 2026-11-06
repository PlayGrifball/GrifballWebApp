using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// The matchmaking queue, live, for the home page's queue panel: its size for everyone (it is public), and a nudge to a
/// signed-in player whose own place changed (joined or left, matched, their match over, kicked). Nothing is called on
/// it: the page gets the rest over HTTP (<see cref="QueueController"/>). A connection is its user's from the browser's
/// session cookie (SignalR's default user id is the NameIdentifier claim), so the page reconnects on a sign-in or out.
/// </summary>
public class QueueHub : Hub<IQueueHubClient>
{
}

/// <summary>Methods that can be invoked on front end client</summary>
public interface IQueueHubClient
{
    /// <summary>The queue now has this many players.</summary>
    Task QueueCount(int count);

    /// <summary>This player's own status changed: the page loads it again.</summary>
    Task QueueStatusChanged();
}

/// <summary>Tells the queue's watchers what changed. Never throws: the queue changed whether or not they heard.</summary>
public interface IQueueNotifier
{
    /// <summary>
    /// Sends the queue's size, as just read from the database. Every change sends what it read, and the matchmaking
    /// tick sends the true size every 10 seconds, whatever was sent before: sends come from every API pod and the
    /// worker, so one that failed or arrived out of order is put right within a tick.
    /// </summary>
    Task Send(int count);

    /// <summary>Tells these players their own status changed (see <see cref="IQueueHubClient.QueueStatusChanged"/>).</summary>
    Task SendStatusChanged(IReadOnlyCollection<int> userIds);
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
