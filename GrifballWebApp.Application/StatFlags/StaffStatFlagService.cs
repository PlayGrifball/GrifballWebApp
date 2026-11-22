using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.StatFlags;

/// <summary>
/// What the staff flags page and the match page's marker read, and dismissing a flag: commissioners and sysadmins
/// only (the controller's role check). A flag is never fixed here - staff fix the game itself, on the match page - and
/// dismissing one never removes or changes it beyond its own dismissed columns.
/// </summary>
public class StaffStatFlagService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    private readonly ICurrentUserService? _currentUser;

    public StaffStatFlagService(IApplicationDbContext context, TimeProvider? time = null, ICurrentUserService? currentUser = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
        _currentUser = currentUser;
    }

    /// <summary>Every open flag, newest first: the staff flags page.</summary>
    public async Task<List<StatFlagDto>> OpenFlagsAsync(CancellationToken ct = default)
    {
        var flags = await _context.StatFlags.Where(StatFlag.Open).OrderByDescending(f => f.FoundAt).ToListAsync(ct);
        return await ToDtosAsync(flags, ct);
    }

    /// <summary>How many flags are open: the staff dashboard's count.</summary>
    public Task<int> OpenFlagCountAsync(CancellationToken ct = default) => _context.StatFlags.CountAsync(StatFlag.Open, ct);

    /// <summary>The open flags of one game (a recorded game or a Halo match, never both): the match page's marker.</summary>
    public async Task<List<StatFlagDto>> OpenFlagsForGameAsync(Guid? matchID, int? recordedGameID, CancellationToken ct = default)
    {
        var flags = await _context.StatFlags.Where(StatFlag.Open)
            .Where(f => matchID != null ? f.MatchID == matchID : f.RecordedGameID == recordedGameID)
            .ToListAsync(ct);
        return await ToDtosAsync(flags, ct);
    }

    /// <summary>The open flags of every game of a season match's series: the match page's markers, one request for all
    /// of them rather than one a game.</summary>
    public async Task<List<StatFlagDto>> OpenFlagsForSeasonMatchAsync(int seasonMatchID, CancellationToken ct = default)
    {
        var games = await _context.SeasonSeriesGames.Where(g => g.SeasonMatchID == seasonMatchID)
            .Select(g => new { g.MatchID, g.RecordedGameID }).ToListAsync(ct);
        var matchIDs = games.Select(g => g.MatchID).OfType<Guid>().ToList();
        var recordedGameIDs = games.Select(g => g.RecordedGameID).OfType<int>().ToList();
        var flags = await _context.StatFlags.Where(StatFlag.Open)
            .Where(f => (f.MatchID != null && matchIDs.Contains(f.MatchID.Value)) || (f.RecordedGameID != null && recordedGameIDs.Contains(f.RecordedGameID.Value)))
            .ToListAsync(ct);
        return await ToDtosAsync(flags, ct);
    }

    /// <summary>Staff looked at it: not wrong, or not worth fixing.</summary>
    public async Task Dismiss(int statFlagID, CancellationToken ct = default)
    {
        var flag = await _context.StatFlags.FirstOrDefaultAsync(f => f.StatFlagID == statFlagID, ct)
            ?? throw new StatFlagNotFoundException(statFlagID);
        flag.Dismiss(_currentUser?.GetCurrentUserId(), _time.GetUtcNow().UtcDateTime);
        await _context.SaveChangesAsync(ct);
    }

    private async Task<List<StatFlagDto>> ToDtosAsync(List<StatFlag> flags, CancellationToken ct)
    {
        if (flags.Count == 0)
            return [];

        var playerIDs = flags.Select(f => f.PlayerUserID).OfType<int>().Distinct().ToList();
        var users = await _context.Users.Include(u => u.XboxUser).Include(u => u.DiscordUser)
            .Where(u => playerIDs.Contains(u.Id)).ToListAsync(ct);
        var nameByID = users.ToDictionary(u => u.Id, u => UserNames.Pick(u.XboxUser?.Gamertag, u.DiscordUser?.DiscordUsername, u.DisplayName, u.UserName));

        var matchIDs = flags.Select(f => f.MatchID).OfType<Guid>().Distinct().ToList();
        var seriesGamesByMatch = await _context.SeasonSeriesGames.Where(g => g.MatchID != null && matchIDs.Contains(g.MatchID!.Value))
            .ToDictionaryAsync(g => g.MatchID!.Value, g => ((int?)g.SeasonMatchID, (int?)g.GameNumber), ct);
        var recordedGameIDs = flags.Select(f => f.RecordedGameID).OfType<int>().Distinct().ToList();
        var seriesGamesByRecordedGame = await _context.SeasonSeriesGames.Where(g => g.RecordedGameID != null && recordedGameIDs.Contains(g.RecordedGameID!.Value))
            .ToDictionaryAsync(g => g.RecordedGameID!.Value, g => ((int?)g.SeasonMatchID, (int?)g.GameNumber), ct);

        return flags.Select(f =>
        {
            var (seasonMatchID, gameNumber) = f.MatchID is Guid matchID ? seriesGamesByMatch.GetValueOrDefault(matchID)
                : f.RecordedGameID is int recordedGameID ? seriesGamesByRecordedGame.GetValueOrDefault(recordedGameID)
                : (null, null);
            return new StatFlagDto
            {
                StatFlagID = f.StatFlagID,
                Rule = f.Rule,
                MatchID = f.MatchID,
                RecordedGameID = f.RecordedGameID,
                SeasonMatchID = seasonMatchID,
                GameNumber = gameNumber,
                PlayerUserID = f.PlayerUserID,
                PlayerName = f.PlayerUserID is int playerID ? nameByID.GetValueOrDefault(playerID) : null,
                Reason = f.Reason,
                FoundAt = f.FoundAt,
            };
        }).ToList();
    }
}
