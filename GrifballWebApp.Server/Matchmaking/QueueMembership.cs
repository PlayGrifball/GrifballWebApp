using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Matchmaking;

public enum QueueJoinResult
{
    /// <summary>No such user, or no gamertag linked: matches are played on Halo, so a player needs one.</summary>
    NoGamertag,
    AlreadyInQueue,
    InMatch,
    Joined,
}

public enum QueueLeaveResult
{
    /// <summary>No such user, or no gamertag linked.</summary>
    NoGamertag,
    NotInQueue,
    Left,
}

/// <summary>
/// Joining and leaving the matchmaking queue, with its rules: a player needs a gamertag, can't queue twice and can't
/// queue while in a match. The Discord buttons (<see cref="MatchmakingButtonHandler"/>) and the site
/// (<see cref="QueueController"/>) both go through here; each says the outcome its own way.
/// Every change is sent to the queue's watchers (<see cref="IQueueCountNotifier"/>), without waiting for it: a Discord
/// button must be answered within 3 seconds, and the send goes through the backplane.
/// </summary>
public class QueueMembership
{
    private readonly IQueueRepository _queue;
    private readonly GrifballContext _context;
    private readonly IQueueCountNotifier _notifier;

    public QueueMembership(IQueueRepository queue, GrifballContext context, IQueueCountNotifier notifier)
    {
        _queue = queue;
        _context = context;
        _notifier = notifier;
    }

    /// <summary>Whether the user exists and has a gamertag linked.</summary>
    public Task<bool> HasGamertag(int userId, CancellationToken ct = default)
        => _context.Users.AnyAsync(x => x.Id == userId && x.XboxUserID != null, ct);

    /// <summary>Joins the queue. <c>Count</c> is the queue's size once joined, 0 when refused.</summary>
    public async Task<(QueueJoinResult Result, int Count)> Join(int userId, CancellationToken ct = default)
    {
        if (await HasGamertag(userId, ct) is false)
            return (QueueJoinResult.NoGamertag, 0);
        if (await _queue.IsInMatch(userId, ct))
            return (QueueJoinResult.InMatch, 0);
        if (await _queue.AddPlayerToQueue(userId, ct) is false)
            return (QueueJoinResult.AlreadyInQueue, 0);

        return (QueueJoinResult.Joined, await Changed(ct));
    }

    /// <summary>Leaves the queue. <c>Count</c> is the queue's size once left, 0 when refused.</summary>
    public async Task<(QueueLeaveResult Result, int Count)> Leave(int userId, CancellationToken ct = default)
    {
        if (await HasGamertag(userId, ct) is false)
            return (QueueLeaveResult.NoGamertag, 0);
        if (await _queue.RemovePlayerToQueue(userId, ct) is false)
            return (QueueLeaveResult.NotInQueue, 0);

        return (QueueLeaveResult.Left, await Changed(ct));
    }

    /// <summary>The queue's new size, sent on without waiting (Send never throws).</summary>
    private async Task<int> Changed(CancellationToken ct)
    {
        var count = await _queue.Count(ct);
        _ = _notifier.Send(count);
        return count;
    }
}

/// <summary>What a player is told about joining or leaving, on Discord and on the site alike.</summary>
public static class QueueMessages
{
    public const string NoGamertag = "You must set your gamertag first";

    /// <summary>The site's Join where matchmaking does not run (a preview without Discord): nobody would be matched.</summary>
    public const string MatchmakingOff = "Matchmaking is off here";

    public static string For(QueueJoinResult result) => result switch
    {
        QueueJoinResult.NoGamertag => NoGamertag,
        QueueJoinResult.AlreadyInQueue => "You are already in the queue!",
        QueueJoinResult.InMatch => "You are already in a match!",
        _ => "You have joined the queue! 🎉",
    };

    public static string For(QueueLeaveResult result) => result switch
    {
        QueueLeaveResult.NoGamertag => NoGamertag,
        QueueLeaveResult.NotInQueue => "You are not in the matchmaking queue.",
        _ => "You have left the queue",
    };
}
