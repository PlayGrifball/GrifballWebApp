using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.LateLeague;

/// <summary>A player's stats for a matchmaking season (Late League's archive), or for the Discord queue's games (Late League
/// now, in none).</summary>
public class MatchmakingStatLine : StatLine
{
    /// <summary>The matchmaking season; null for the queue's games, in none.</summary>
    public int? MatchmakingSeasonID { get; set; }
    public string? MatchmakingSeasonName { get; set; }
    /// <summary>The stored row, for stored totals.</summary>
    public int? MatchmakingPlayerStatID { get; set; }
    public override string? SeasonKey => MatchmakingSeasonID is int id ? $"matchmaking/{id}" : null;
}

/// <summary>
/// Matchmaking (Late League) player stats. Where a player has games of a matchmaking season in the app
/// (<see cref="PlayerGameStatsQuery.Matchmaking"/>: the archive's recorded games, the queue's reported matches and its matches decided by vote), the stats are
/// those games' totals; otherwise they are the totals stored for it (<see cref="MatchmakingPlayerStat"/>, its sheet's).
/// Never both for one player's season.
/// </summary>
public class MatchmakingStatsService
{
    private readonly GrifballContext _context;

    public MatchmakingStatsService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The stat lines of a matchmaking season (0: the Discord queue's games, in none), of a player, or of everything
    /// (both null).</summary>
    public async Task<List<MatchmakingStatLine>> GetAsync(int? matchmakingSeasonID = null, int? userID = null, CancellationToken ct = default)
    {
        var games = PlayerGameStatsQuery.Matchmaking(_context);
        if (matchmakingSeasonID is not null)
            games = games.Where(g => g.SeasonID == matchmakingSeasonID);
        if (userID is not null)
            games = games.Where(g => g.UserID == userID);
        var totals = await GameTotals.Of(games).ToListAsync(ct);
        var gameVersions = await _context.GameVersions.AsNoTracking().ToDictionaryAsync(g => g.GameVesionID, ct);
        // One line per player and season: matchmaking has no phases or teams
        var fromGames = totals
            .Select(t =>
            {
                var line = new MatchmakingStatLine { Gamertag = t.Gamertag, MatchmakingSeasonID = t.SeasonID == 0 ? null : t.SeasonID };
                line.SetGames(t, gameVersions.GetValueOrDefault(t.GameVersionID));
                return line;
            })
            .ToList();
        var covered = fromGames.Select(l => (l.MatchmakingSeasonID, l.UserID)).ToHashSet();

        var stored = _context.MatchmakingPlayerStats.AsNoTracking();
        if (matchmakingSeasonID is not null)
            stored = stored.Where(s => s.MatchmakingSeasonID == matchmakingSeasonID);
        if (userID is not null)
            stored = stored.Where(s => s.UserID == userID);
        var storedLines = (await stored.Select(s => new { Row = s, GameVersionName = s.GameVersion.GameVersionName, GameVersionSlug = s.GameVersion.Slug }).ToListAsync(ct))
            .Where(r => !covered.Contains((r.Row.MatchmakingSeasonID, r.Row.UserID)))
            .Select(r =>
            {
                var line = new MatchmakingStatLine { Gamertag = r.Row.Gamertag, MatchmakingSeasonID = r.Row.MatchmakingSeasonID, MatchmakingPlayerStatID = r.Row.MatchmakingPlayerStatID };
                line.SetStored(r.Row, r.GameVersionName, r.GameVersionSlug);
                return line;
            });

        var lines = fromGames.Concat(storedLines).ToList();
        var ids = lines.Select(l => l.MatchmakingSeasonID).OfType<int>().Distinct().ToList();
        var seasons = await _context.MatchmakingSeasons.AsNoTracking().Where(s => ids.Contains(s.MatchmakingSeasonID))
            .ToDictionaryAsync(s => s.MatchmakingSeasonID, ct);
        foreach (var line in lines)
            line.MatchmakingSeasonName = line.MatchmakingSeasonID is int id ? seasons[id].Name : null;
        return lines
            .OrderByDescending(l => l.MatchmakingSeasonID is int id ? seasons[id].StartsAt : DateTime.MaxValue)
            .ThenBy(l => l.MatchmakingSeasonID)
            .ThenByDescending(l => l.Goals)
            .ThenByDescending(l => l.Kills)
            .ThenBy(l => l.Gamertag)
            .ToList();
    }

    /// <summary>The matchmaking seasons, latest first, with how many players and games each has.</summary>
    public async Task<List<MatchmakingSeasonDto>> GetSeasonsAsync(CancellationToken ct = default)
    {
        var seasons = await _context.MatchmakingSeasons.AsNoTracking()
            .OrderByDescending(s => s.StartsAt).ThenBy(s => s.Name)
            .Select(MatchmakingSeasonDto.Of)
            .ToListAsync(ct);
        var players = (await GetAsync(ct: ct)).Where(l => l.MatchmakingSeasonID is not null)
            .GroupBy(l => l.MatchmakingSeasonID!.Value).ToDictionary(g => g.Key, g => g.Count());
        foreach (var season in seasons)
            season.PlayerCount = players.GetValueOrDefault(season.MatchmakingSeasonID);
        return seasons;
    }

    /// <summary>The Discord queue's games (Late League as it is played now, in no matchmaking season): how many there are and
    /// each player's totals.</summary>
    public async Task<MatchmakingQueueDto> GetQueueAsync(CancellationToken ct = default)
    {
        var lines = await GetAsync(0, ct: ct);
        // Played to a result: reported, or decided by its players' vote (#296)
        var queue = _context.MatchedMatches.Where(m => m.MatchmakingSeasonID == null);
        var games = await queue.CountAsync(MatchedMatch.PlayedToAResult, ct);
        var byVote = await queue.CountAsync(MatchedMatch.DecidedByVote, ct);
        return new MatchmakingQueueDto { GameCount = games, VoteDecidedGameCount = byVote, Players = lines.Select(MatchmakingPlayerStatDto.From).ToList() };
    }

    /// <summary>A matchmaking season and its players' lines; null when there is no such season.</summary>
    public async Task<MatchmakingSeasonDetailDto?> GetSeasonAsync(int matchmakingSeasonID, CancellationToken ct = default)
    {
        var season = await _context.MatchmakingSeasons.AsNoTracking()
            .Where(s => s.MatchmakingSeasonID == matchmakingSeasonID)
            .Select(MatchmakingSeasonDto.Of)
            .FirstOrDefaultAsync(ct);
        if (season is null)
            return null;
        var lines = await GetAsync(matchmakingSeasonID, ct: ct);
        season.PlayerCount = lines.Count;
        return new MatchmakingSeasonDetailDto { Season = season, Players = lines.Select(MatchmakingPlayerStatDto.From).ToList() };
    }
}
