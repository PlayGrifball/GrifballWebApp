using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using QueuedPlayer = GrifballWebApp.Database.Models.QueuedPlayer;

namespace GrifballWebApp.Server.Matchmaking;

public interface IQueueRepository
{
    Task<bool> AddPlayerToQueue(int id, CancellationToken ct = default);
    Task<bool> RemovePlayerToQueue(int id, CancellationToken ct = default);
    Task<QueuedPlayer?> GetQueuePlayer(int id, CancellationToken ct = default);
    Task<bool> IsInMatch(int id, CancellationToken ct = default);
    Task<QueuedPlayer[]> GetQueuePlayersWithInfo(CancellationToken ct);
    Task<MatchedMatch[]> GetActiveMatches(CancellationToken ct);
}

public class QueueRepository : IQueueRepository
{
    private readonly GrifballContext _context;
    public QueueRepository(GrifballContext context)
    {
        _context = context;
    }

    public async Task<QueuedPlayer?> GetQueuePlayer(int id, CancellationToken ct)
    {
        return await _context.QueuedPlayer.FirstOrDefaultAsync(x => x.UserID == id, ct);
    }

    public async Task<bool> IsInMatch(int id, CancellationToken ct)
    {
        return await _context.MatchedPlayers
            .Where(x => x.UserID == id)
            .Where(x => x.MatchedTeam.HomeMatchedMatch!.Active == true || x.MatchedTeam.AwayMatchedMatch!.Active == true)
            .Where(x => x.Kicked == false)
            .AnyAsync(ct);
    }

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
                .ThenInclude(x => x.User.DiscordUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            .Where(x => x.Active)
            .ToArrayAsync(ct);
    }

    public async Task<bool> AddPlayerToQueue(int id, CancellationToken ct = default)
    {
        var isInQueue = await _context.QueuedPlayer.AnyAsync(x => x.UserID == id, ct);
        if (isInQueue)
        {
            return false;
        }
        _context.QueuedPlayer.Add(new QueuedPlayer
        {
            UserID = id,
            JoinedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync(ct);
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
