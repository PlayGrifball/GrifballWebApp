using GrifballWebApp.Database.Queries;
using GrifballWebApp.Database;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using QueuedPlayer = GrifballWebApp.Domain.LateLeague.QueuedPlayer;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>A player's place in matchmaking, and the queue's size, read together (<see cref="IQueueRepository.PlayerState"/>).</summary>
public sealed record QueuePlayerState(int UserId, bool HasGamertag, bool InQueue, bool InMatch, int QueueCount);

public interface IQueueRepository
{
    /// <summary>
    /// Adds the spot (<see cref="QueuedPlayer.Join"/>, which checked the rules) to the queue; false when they are in it
    /// already, a concurrent add (the Discord button and the site at once) included: the second insert's key violation means just that.
    /// </summary>
    Task<bool> AddPlayerToQueue(QueuedPlayer spot, CancellationToken ct = default);
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
        return await _context.QueuedPlayer.FirstOrDefaultAsync(QueuedPlayer.Of(id), ct);
    }

    /// <summary>Whether the user is still in a match being played (<see cref="MatchedPlayer.InActiveMatch"/>): from the
    /// user's own players, by its key.</summary>
    public async Task<bool> IsInMatch(int id, CancellationToken ct)
    {
        return await _context.Users
            .Where(x => x.Id == id)
            .SelectMany(x => x.MatchedPlayers)
            .AnyAsync(MatchedPlayer.InActiveMatch, ct);
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
                x.MatchedPlayers.AsQueryable().Any(MatchedPlayer.InActiveMatch),
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
            .WithPlayerNames()
            .Where(x => x.Status == MatchedMatchStatus.Active)
            .ToArrayAsync(ct);
    }

    public async Task<bool> AddPlayerToQueue(QueuedPlayer spot, CancellationToken ct = default)
    {
        var id = spot.UserID;
        var isInQueue = await _context.QueuedPlayer.AnyAsync(QueuedPlayer.Of(id), ct);
        if (isInQueue)
        {
            return false;
        }
        var entry = _context.QueuedPlayer.Add(spot);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Added by another request between the check and the insert (the Discord button and the site at once):
            // the key violation means they are in the queue. Anything else is a real failure.
            entry.State = EntityState.Detached;
            if (await _context.QueuedPlayer.AnyAsync(QueuedPlayer.Of(id), ct))
                return false;
            throw;
        }
        return true;
    }

    public async Task<bool> RemovePlayerToQueue(int id, CancellationToken ct = default)
    {
        var isInQueue = await _context.QueuedPlayer.FirstOrDefaultAsync(QueuedPlayer.Of(id), ct);
        if (isInQueue is null)
        {
            return false;
        }
        _context.QueuedPlayer.Remove(isInQueue);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
