using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// The matchmaking queue's size, live, for the home page's queue panel. Anyone may listen (the size is public); nothing
/// is called on it: the page gets the rest over HTTP (<see cref="QueueController"/>).
/// </summary>
public class QueueHub : Hub<IQueueHubClient>
{
}

/// <summary>Methods that can be invoked on front end client</summary>
public interface IQueueHubClient
{
    /// <summary>The queue now has this many players.</summary>
    Task QueueCount(int count);
}

/// <summary>Tells the queue's watchers its size. Never throws: the queue changed whether or not they heard.</summary>
public interface IQueueCountNotifier
{
    /// <summary>Sends the size after a change made here (a join or a leave).</summary>
    Task Send(int count);

    /// <summary>
    /// Sends the size unless it is the one this process sent last: the matchmaking tick checks every 10 seconds, and
    /// only a change (matches made, players dropped or put back) is news.
    /// </summary>
    Task SendIfChanged(int count);
}

/// <summary>
/// Through <see cref="QueueHub"/>: from the worker too, as its hub messages reach the API pods' browsers through the
/// backplane. A singleton, for the last size sent.
/// </summary>
public class QueueCountNotifier : IQueueCountNotifier
{
    private readonly IHubContext<QueueHub, IQueueHubClient> _hub;
    private readonly ILogger<QueueCountNotifier> _logger;
    private int _lastSent = -1;

    public QueueCountNotifier(IHubContext<QueueHub, IQueueHubClient> hub, ILogger<QueueCountNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task Send(int count)
    {
        Interlocked.Exchange(ref _lastSent, count);
        try
        {
            await _hub.Clients.All.QueueCount(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send the matchmaking queue's size");
        }
    }

    public Task SendIfChanged(int count)
        => Interlocked.Exchange(ref _lastSent, count) == count ? Task.CompletedTask : Send(count);
}
