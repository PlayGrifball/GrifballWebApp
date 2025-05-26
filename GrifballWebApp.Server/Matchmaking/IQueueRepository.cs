using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using QueuedPlayer = GrifballWebApp.Database.Models.QueuedPlayer;

namespace GrifballWebApp.Server.Matchmaking;

public interface IQueueRepository
{
    Task<bool> AddPlayerToQueue(ulong id, CancellationToken ct = default);
    Task<bool> RemovePlayerToQueue(ulong id, CancellationToken ct = default);
    Task<QueuedPlayer?> GetQueuePlayer(ulong id, CancellationToken ct = default);
    Task<bool> IsInMatch(ulong id, CancellationToken ct = default);
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

    public async Task<QueuedPlayer?> GetQueuePlayer(ulong id, CancellationToken ct)
    {
        return await _context.QueuedPlayer.FirstOrDefaultAsync(x => x.DiscordUserID == (long)id, ct);
    }

    public async Task<bool> IsInMatch(ulong id, CancellationToken ct)
    {
        return await _context.MatchedPlayers
            .Where(x => x.DiscordUserID == (long)id)
            .Where(x => x.MatchedTeam.HomeMatchedMatch!.Active == true || x.MatchedTeam.AwayMatchedMatch!.Active == true)
            .Where(x => x.Kicked == false)
            .AnyAsync(ct);
    }

    public async Task<QueuedPlayer[]> GetQueuePlayersWithInfo(CancellationToken ct)
    {
        return await _context.QueuedPlayer
            .Include(x => x.DiscordUser.XboxUser)
            .ToArrayAsync(ct);
    }

    public async Task<MatchedMatch[]> GetActiveMatches(CancellationToken ct)
    {
        return await _context.MatchedMatches
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.DiscordUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.DiscordUser)
            .Where(x => x.Active)
            .ToArrayAsync(ct);
    }

    public async Task<bool> AddPlayerToQueue(ulong id, CancellationToken ct = default)
    {
        var isInQueue = await _context.QueuedPlayer.AnyAsync(x => x.DiscordUserID == (long)id, ct);
        if (isInQueue)
        {
            return false;
        }
        _context.QueuedPlayer.Add(new QueuedPlayer
        {
            DiscordUserID = (long)id,
            JoinedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemovePlayerToQueue(ulong id, CancellationToken ct = default)
    {
        var isInQueue = await _context.QueuedPlayer.FirstOrDefaultAsync(x => x.DiscordUserID == (long)id, ct);
        if (isInQueue is null)
        {
            return false;
        }
        _context.QueuedPlayer.Remove(isInQueue);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
