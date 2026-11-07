using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using QueuedPlayer = GrifballWebApp.Database.Models.QueuedPlayer;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>A player's place in matchmaking, and the queue's size, read together (<see cref="IQueueRepository.PlayerState"/>).</summary>
public sealed record QueuePlayerState(int UserId, bool HasGamertag, bool InQueue, bool InMatch, int QueueCount);

public interface IQueueRepository
{
    /// <summary>
    /// Adds the player to the queue; false when they are in it already, a concurrent add (the Discord button and the
    /// site at once) included: the second insert's key violation means just that.
    /// </summary>
    Task<bool> AddPlayerToQueue(int id, CancellationToken ct = default);
    Task<bool> RemovePlayerToQueue(int id, CancellationToken ct = default);
    Task<QueuedPlayer?> GetQueuePlayer(int id, CancellationToken ct = default);
    Task<bool> IsInMatch(int id, CancellationToken ct = default);
    /// <summary>How many players are in the queue.</summary>
    Task<int> Count(CancellationToken ct = default);
    Task<QueuedPlayer[]> GetQueuePlayersWithInfo(CancellationToken ct);
    /// <summary>The user's place in matchmaking and the queue's size, in one query; null for no such user.</summary>
    Task<QueuePlayerState?> PlayerState(int userId, CancellationToken ct = default);
    /// <summary>The same as <see cref="PlayerState(int, CancellationToken)"/>, for the user with this Discord account.</summary>
    Task<QueuePlayerState?> PlayerStateByDiscord(long discordUserId, CancellationToken ct = default);
    Task<MatchedMatch[]> GetActiveMatches(CancellationToken ct);
}

public class QueueRepository : IQueueRepository
{
    private readonly GrifballContext _context;
    public QueueRepository(GrifballContext context)
    {
        _context = context;
    }

    public Task<int> Count(CancellationToken ct = default) => _context.QueuedPlayer.CountAsync(ct);

    public async Task<QueuedPlayer?> GetQueuePlayer(int id, CancellationToken ct)
    {
        return await _context.QueuedPlayer.FirstOrDefaultAsync(x => x.UserID == id, ct);
    }

    /// <summary>A player still in a match being played: not kicked from it. What keeps them off the queue.</summary>
    public static readonly System.Linq.Expressions.Expression<Func<MatchedPlayer, bool>> InActiveMatch = p => p.Kicked == false
        && (p.MatchedTeam.HomeMatchedMatch!.Status == MatchedMatchStatus.Active || p.MatchedTeam.AwayMatchedMatch!.Status == MatchedMatchStatus.Active);

    public async Task<bool> IsInMatch(int id, CancellationToken ct)
    {
        return await _context.MatchedPlayers
            .Where(x => x.UserID == id)
            .Where(InActiveMatch)
            .AnyAsync(ct);
    }

    public Task<QueuePlayerState?> PlayerState(int userId, CancellationToken ct = default)
        => PlayerStateOf(_context.Users.Where(x => x.Id == userId), ct);

    public Task<QueuePlayerState?> PlayerStateByDiscord(long discordUserId, CancellationToken ct = default)
        => PlayerStateOf(_context.Users.Where(x => x.DiscordUserID == discordUserId), ct);

    private Task<QueuePlayerState?> PlayerStateOf(IQueryable<User> user, CancellationToken ct)
        => user
            .Select(x => new QueuePlayerState(
                x.Id,
                x.XboxUserID != null,
                x.QueuedPlayer != null,
                x.MatchedPlayers.AsQueryable().Any(InActiveMatch),
                _context.QueuedPlayer.Count()))
            .FirstOrDefaultAsync(ct);

    public async Task<QueuedPlayer[]> GetQueuePlayersWithInfo(CancellationToken ct)
    {
        return await _context.QueuedPlayer
            .Include(x => x.User.XboxUser)
            .Include(x => x.User.DiscordUser)
            .ToArrayAsync(ct);
    }

    public async Task<MatchedMatch[]> GetActiveMatches(CancellationToken ct)
    {
        return await _context.MatchedMatches
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User.XboxUser)
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.XboxUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            // For the match to count its players' votes (MatchedMatch.VotedWinner)
            .Include(x => x.MatchedWinnerVotes)
            .AsSplitQuery()
            .Where(x => x.Status == MatchedMatchStatus.Active)
            .ToArrayAsync(ct);
    }

    public async Task<bool> AddPlayerToQueue(int id, CancellationToken ct = default)
    {
        var isInQueue = await _context.QueuedPlayer.AnyAsync(x => x.UserID == id, ct);
        if (isInQueue)
        {
            return false;
        }
        var entry = _context.QueuedPlayer.Add(new QueuedPlayer
        {
            UserID = id,
            JoinedAt = DateTime.UtcNow,
        });
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Added by another request between the check and the insert (the Discord button and the site at once):
            // the key violation means they are in the queue. Anything else is a real failure.
            entry.State = EntityState.Detached;
            if (await _context.QueuedPlayer.AnyAsync(x => x.UserID == id, ct))
                return false;
            throw;
        }
        return true;
    }

    public async Task<bool> RemovePlayerToQueue(int id, CancellationToken ct = default)
    {
        var isInQueue = await _context.QueuedPlayer.FirstOrDefaultAsync(x => x.UserID == id, ct);
        if (isInQueue is null)
        {
            return false;
        }
        _context.QueuedPlayer.Remove(isInQueue);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
