using GrifballWebApp.Database;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// Joining and leaving the matchmaking queue, by its rules (<see cref="QueuedPlayer.Join"/>). The Discord buttons (<see cref="MatchmakingButtonHandler"/>) and the site (<see cref="QueueController"/>)
/// both go through here; each says the outcome its own way.
/// Every change is sent to the queue's watchers (<see cref="IQueueNotifier"/>): the new size to everyone, and a nudge to
/// the player, whose page may be open elsewhere. Without waiting for it: a Discord button must be answered within 3
/// seconds, and the send goes through the backplane.
/// </summary>
public class QueueMembership
{
    private readonly IQueueRepository _queue;
    private readonly GrifballContext _context;
    private readonly IQueueNotifier _notifier;

    public QueueMembership(IQueueRepository queue, GrifballContext context, IQueueNotifier notifier)
    {
        _queue = queue;
        _context = context;
        _notifier = notifier;
    }

    /// <summary>Joins the queue. <c>Count</c> is the queue's size once joined, 0 when refused.</summary>
    public async Task<(QueueJoinResult Result, int Count)> Join(int userId, CancellationToken ct = default)
        => await Join(await _queue.PlayerState(userId, ct), ct);

    /// <summary>Joins the queue as the user with this Discord account: the user, gamertag and match are one query.</summary>
    public async Task<(QueueJoinResult Result, int Count)> JoinByDiscord(long discordUserId, CancellationToken ct = default)
        => await Join(await _queue.PlayerStateByDiscord(discordUserId, ct), ct);

    private async Task<(QueueJoinResult Result, int Count)> Join(QueuePlayerState? player, CancellationToken ct)
    {
        if (player is null)
            return (QueueJoinResult.NoGamertag, 0);
        var join = QueuedPlayer.Join(player.UserId, player.HasGamertag, player.InMatch, player.InQueue, DateTime.UtcNow);
        if (join.Spot is null)
            return (join.Result, 0);
        // Joined by another request since the check (the Discord button and the site at once)
        if (await _queue.AddPlayerToQueue(join.Spot, ct) is false)
            return (QueueJoinResult.AlreadyInQueue, 0);

        return (QueueJoinResult.Joined, await Changed(player.UserId, ct));
    }

    /// <summary>Leaves the queue. <c>Count</c> is the queue's size once left, 0 when refused.</summary>
    public async Task<(QueueLeaveResult Result, int Count)> Leave(int userId, CancellationToken ct = default)
    {
        if (await _queue.RemovePlayerToQueue(userId, ct) is false)
            return (QueueLeaveResult.NotInQueue, 0);

        return (QueueLeaveResult.Left, await Changed(userId, ct));
    }

    /// <summary>Leaves the queue as the user with this Discord account.</summary>
    public async Task<(QueueLeaveResult Result, int Count)> LeaveByDiscord(long discordUserId, CancellationToken ct = default)
    {
        var userId = await _context.Users
            .Where(x => x.DiscordUserID == discordUserId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        return userId is int id ? await Leave(id, ct) : (QueueLeaveResult.NotInQueue, 0);
    }

    /// <summary>The queue's new size, sent on with the player's nudge, without waiting (neither send throws).</summary>
    private async Task<int> Changed(int userId, CancellationToken ct)
    {
        var count = await _queue.Count(ct);
        _ = _notifier.Send(count);
        _ = _notifier.SendStatusChanged([userId]);
        return count;
    }
}

/// <summary>What a player is told about joining or leaving, on Discord and on the site alike.</summary>
public static class QueueMessages
{
    public const string NoGamertag = "You must set your gamertag first";

    /// <summary>The site's Join where matchmaking does not run (a preview without Discord): nobody would be matched.</summary>
    public const string LateLeagueOff = "Late League is off here";

    public static string For(QueueJoinResult result) => result switch
    {
        QueueJoinResult.NoGamertag => NoGamertag,
        QueueJoinResult.AlreadyInQueue => "You are already in the Late League queue!",
        QueueJoinResult.InMatch => "You are already in a Late League match!",
        _ => "You have joined the Late League queue! 🎉",
    };

    public static string For(QueueLeaveResult result) => result switch
    {
        QueueLeaveResult.NotInQueue => "You are not in the Late League queue.",
        _ => "You have left the Late League queue",
    };
}
