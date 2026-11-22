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

        // A recorded game outside any series (an imported scrim, a Late League matchmaking game) has no page to link
        // to: shown by its date and sides instead.
        var outsideASeries = recordedGameIDs.Except(seriesGamesByRecordedGame.Keys).ToList();
        var gameInfoByRecordedGame = outsideASeries.Count == 0 ? [] : await _context.RecordedGames
            .Where(g => outsideASeries.Contains(g.RecordedGameID))
            .Select(g => new GameInfo
            {
                RecordedGameID = g.RecordedGameID,
                PlayedAt = g.PlayedAt,
                Teams = g.Teams.OrderBy(t => t.Side).Select(t => t.Name).ToList(),
            })
            .ToDictionaryAsync(g => g.RecordedGameID, ct);

        return flags.Select(f => ToDto(f, nameByID, seriesGamesByMatch, seriesGamesByRecordedGame, gameInfoByRecordedGame)).ToList();
    }

    private static StatFlagDto ToDto(StatFlag f, Dictionary<int, string> nameByID,
        Dictionary<Guid, (int? SeasonMatchID, int? GameNumber)> seriesGamesByMatch,
        Dictionary<int, (int? SeasonMatchID, int? GameNumber)> seriesGamesByRecordedGame,
        Dictionary<int, GameInfo> gameInfoByRecordedGame)
    {
        var (seasonMatchID, gameNumber) = f.MatchID is Guid matchID ? seriesGamesByMatch.GetValueOrDefault(matchID)
            : f.RecordedGameID is int recordedGameID ? seriesGamesByRecordedGame.GetValueOrDefault(recordedGameID)
            : (null, null);
        var outside = seasonMatchID is null && f.RecordedGameID is int gameID ? gameInfoByRecordedGame.GetValueOrDefault(gameID) : null;
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
            GamePlayedAt = outside?.PlayedAt,
            GameTeams = outside is null ? null : string.Join(" vs ", outside.Teams.Select(n => n ?? "?")),
        };
    }

    /// <summary>A recorded game outside any series, for <see cref="ToDto"/>'s fallback (GamePlayedAt, GameTeams).</summary>
    private sealed class GameInfo
    {
        public int RecordedGameID { get; set; }
        public DateTime? PlayedAt { get; set; }
        public List<string?> Teams { get; set; } = [];
    }
}
