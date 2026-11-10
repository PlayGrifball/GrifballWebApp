namespace GrifballWebApp.Application.Matchmaking;

/// <summary>Tells the queue's watchers what changed. Never throws: the queue changed whether or not they heard.</summary>
public interface IQueueNotifier
{
    /// <summary>
    /// Sends the queue's size, as just read from the database. Every change sends what it read, and the matchmaking
    /// tick sends the true size every 10 seconds, whatever was sent before: sends come from every API pod and the
    /// worker, so one that failed or arrived out of order is put right within a tick.
    /// </summary>
    Task Send(int count);

    /// <summary>Tells these players their own status changed (the queue hub's <c>QueueStatusChanged</c>).</summary>
    Task SendStatusChanged(IReadOnlyCollection<int> userIds);
}
