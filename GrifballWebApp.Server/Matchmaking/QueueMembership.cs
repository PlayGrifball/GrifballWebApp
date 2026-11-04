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
/// Every change is sent to the queue's watchers (<see cref="IQueueCountNotifier"/>).
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

    /// <summary>How many players are in the queue.</summary>
    public Task<int> Count(CancellationToken ct = default) => _context.QueuedPlayer.CountAsync(ct);

    public async Task<QueueJoinResult> Join(int userId, CancellationToken ct = default)
    {
        if (await HasGamertag(userId, ct) is false)
            return QueueJoinResult.NoGamertag;
        if (await _queue.GetQueuePlayer(userId, ct) is not null)
            return QueueJoinResult.AlreadyInQueue;
        if (await _queue.IsInMatch(userId, ct))
            return QueueJoinResult.InMatch;

        // false if another request added it meanwhile: it is in the queue either way
        await _queue.AddPlayerToQueue(userId, ct);
        await _notifier.Send(await Count(ct));
        return QueueJoinResult.Joined;
    }

    public async Task<QueueLeaveResult> Leave(int userId, CancellationToken ct = default)
    {
        if (await HasGamertag(userId, ct) is false)
            return QueueLeaveResult.NoGamertag;
        if (await _queue.GetQueuePlayer(userId, ct) is null)
            return QueueLeaveResult.NotInQueue;

        await _queue.RemovePlayerToQueue(userId, ct);
        await _notifier.Send(await Count(ct));
        return QueueLeaveResult.Left;
    }
}

/// <summary>What a player is told about joining or leaving, on Discord and on the site alike.</summary>
public static class QueueMessages
{
    public const string NoGamertag = "You must set your gamertag first";

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
