using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A player's spot in the Late League queue: the row is the spot. A player joins through <see cref="Join"/> (the rules are
/// <see cref="CheckJoin"/>), or is put back ahead of everyone through <see cref="PutBackAtFront"/> when their match is
/// canceled. Leaving, or being matched out of the queue (<see cref="MatchUp"/>), deletes the spot; nothing about it changes
/// while it lasts.
/// </summary>
/// <remarks>
/// The key is the user: a player is queued once. Two joins at once (the Discord button and the site) meet at the key.
/// </remarks>
[AggregateRoot]
public class QueuedPlayer : AuditableEntity
{
    /// <summary>For EF. A spot begins through <see cref="Join"/> or <see cref="PutBackAtFront"/>.</summary>
    private QueuedPlayer() { }

    /// <summary>
    /// Whether a player may join the queue, or why not: matches are played on Halo, so a player needs a gamertag; one
    /// already in a match can't queue for another; and a player is queued once. Leaving needs none of that: a player whose
    /// gamertag was unlinked while queued must not be stuck.
    /// </summary>
    public static QueueJoinResult CheckJoin(bool hasGamertag, bool inMatch, bool inQueue)
    {
        if (hasGamertag is false)
            return QueueJoinResult.NoGamertag;
        if (inMatch)
            return QueueJoinResult.InMatch;
        if (inQueue)
            return QueueJoinResult.AlreadyInQueue;
        return QueueJoinResult.Joined;
    }

    /// <summary>The user <paramref name="userId"/>'s spot, joined at <paramref name="joinedAt"/>. <see cref="CheckJoin"/> first.</summary>
    public static QueuedPlayer Join(int userId, DateTime joinedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        return new() { UserID = userId, JoinedAt = joinedAt };
    }

    /// <summary>For tests: the spot of a user not saved yet, which EF keys once it is.</summary>
    internal static QueuedPlayer Join(User user, DateTime joinedAt) => new() { UserID = user.Id, User = user, JoinedAt = joinedAt };

    /// <summary>
    /// The players of a canceled match that began at <paramref name="matchStartedAt"/> put back in the queue, in the order
    /// <paramref name="userIdsInOrder"/> gives, ahead of everyone in <paramref name="queue"/> now. Their old join times went
    /// with their spots, but they were the longest queued (<see cref="MatchUp"/> takes those), so they go back just in
    /// front: a second apart, ending a second before the earlier of the match's start and the queue's first join. Those
    /// in the queue again already are left out (a player is queued once).
    /// </summary>
    public static IReadOnlyList<QueuedPlayer> PutBackAtFront(IEnumerable<int> userIdsInOrder, DateTime matchStartedAt, IReadOnlyCollection<QueuedPlayer> queue)
    {
        var front = queue.Count == 0 ? matchStartedAt : new[] { matchStartedAt, queue.Min(x => x.JoinedAt) }.Min();
        var requeue = userIdsInOrder.Distinct().Where(id => queue.Any(x => x.IsOf(id)) is false).ToList();
        return requeue.Select((userId, index) => Join(userId, front.AddSeconds(index - requeue.Count))).ToList();
    }

    /// <summary>
    /// The players matched out of <paramref name="queue"/>: as many whole matches of <paramref name="playersPerMatch"/> as
    /// it holds, the longest queued first, a match's players in queue order. The rest stay queued. Their spots go once
    /// they are in their matches.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<QueuedPlayer>> MatchUp(IEnumerable<QueuedPlayer> queue, int playersPerMatch)
    {
        if (playersPerMatch <= 0)
            return [];
        return queue
            .OrderBy(x => x.JoinedAt) // Longest queued get to play first
            .Chunk(playersPerMatch)
            .Where(x => x.Length == playersPerMatch)
            .ToList();
    }

    // Nothing outside the spot sets its fields: only its factories, and EF (which uses the private setters)

    /// <summary>The queued user, and the key: a user is queued once.</summary>
    public int UserID { get; internal set; }
    public User User { get; private set; } = null!;
    public DateTime JoinedAt { get; private set; }

    /// <summary>The user <paramref name="userId"/>'s spot (<see cref="IsOf"/> for a loaded one): whether they are queued already.</summary>
    public static Expression<Func<QueuedPlayer, bool>> Of(int userId) => x => x.UserID == userId;

    /// <summary>Whether this is the user <paramref name="userId"/>'s spot (<see cref="Of"/> for queries).</summary>
    public bool IsOf(int userId) => UserID == userId;

    /// <summary>
    /// Whether the player is still playing in one of <paramref name="activeMatches"/> (kicked players are not): then their
    /// spot goes, and they may queue again once the match is over. Needs the matches' players loaded.
    /// </summary>
    public bool IsPlayingIn(IEnumerable<MatchedMatch> activeMatches) => activeMatches.Any(m => m.PlayersStillIn().Any(p => p.UserID == UserID));
}

/// <summary>Whether a player joined the queue, or why not (<see cref="QueuedPlayer.CheckJoin"/>).</summary>
public enum QueueJoinResult
{
    /// <summary>No such user, or no gamertag linked: matches are played on Halo, so a player needs one.</summary>
    NoGamertag,
    AlreadyInQueue,
    InMatch,
    Joined,
}

/// <summary>Whether a player left the queue.</summary>
public enum QueueLeaveResult
{
    /// <summary>Not in the queue, or no such user.</summary>
    NotInQueue,
    Left,
}
