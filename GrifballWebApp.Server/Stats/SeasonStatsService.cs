using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Stats;

/// <summary>A player's stats for one phase of a season (regular season or playoffs) on one team, with the season's facts.</summary>
public class SeasonStatLine
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public DateTime SeasonStart { get; set; }
    public int LeagueID { get; set; }
    public required string LeagueName { get; set; }
    public required string LeagueSlug { get; set; }
    public bool IsHistorical { get; set; }
    public string? HistoricalSourceUrl { get; set; }
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }

    /// <summary>Totalled from the season's matches (true), or the season totals stored for a player with no match data.</summary>
    public bool FromMatches { get; set; }
    /// <summary>The stored row, for stored totals.</summary>
    public int? PlayerSeasonStatID { get; set; }
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int? TeamID { get; set; }
    public string? TeamName { get; set; }
    public string? Division { get; set; }
    public string? TradeNote { get; set; }
    public string? RoleNote { get; set; }
    public int StintIndex { get; set; } = 1;
    public bool IsPlayoffs { get; set; }
    /// <summary>A stored row of a sheet that has the regular season and the playoffs together (the season's only row for
    /// the player): it covers both phases.</summary>
    public bool IsCombined { get; set; }

    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public int Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int? KdSpread { get; set; }
    public int CarrierKills { get; set; }
    /// <summary>The line counts carrier stops: stored totals do, games only where their source records them (Waypoint's don't).</summary>
    public bool RecordsCarrierKills { get; set; } = true;
    public int Sprees { get; set; }
    public int MultiKills { get; set; }
    public int Exterminations { get; set; }
    public int Betrayals { get; set; }
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }
    public int? Score { get; set; }
    public int? DamageDealt { get; set; }
    public int? DamageTaken { get; set; }
}

/// <summary>
/// Season player stats, the one place the season page, team page, profiles and leaderboards read them from. Where a
/// player's phase of a season has games in the app (<see cref="PlayerGameStatsQuery"/>: Waypoint matches of reported or
/// imported series, recorded MCC games, Late League scrims), the stats are those games' totals; otherwise they are the season totals
/// stored for it (<see cref="PlayerSeasonStat"/>, a historical sheet's). Never both for one player's phase of a season.
/// </summary>
public class SeasonStatsService
{
    private readonly GrifballContext _context;

    public SeasonStatsService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The stat lines of a season, of a player, or of every season (both null).</summary>
    public async Task<List<SeasonStatLine>> GetAsync(int? seasonID = null, int? userID = null, CancellationToken ct = default)
    {
        var fromMatches = await FromMatchesAsync(seasonID, userID, ct);
        var covered = fromMatches.Select(l => (l.SeasonID, l.UserID, l.IsPlayoffs)).ToHashSet();

        var stored = _context.PlayerSeasonStats.AsNoTracking().Where(s => s.Source != StatSource.Waypoint);
        if (seasonID is not null)
            stored = stored.Where(s => s.SeasonID == seasonID);
        if (userID is not null)
            stored = stored.Where(s => s.UserID == userID);
        var storedLines = (await stored.Select(s => new SeasonStatLine
        {
            SeasonID = s.SeasonID,
            SeasonName = s.Season.SeasonName,
            SeasonStart = s.Season.SeasonStart,
            LeagueID = s.Season.LeagueID,
            LeagueName = s.Season.League.LeagueName,
            LeagueSlug = s.Season.League.Slug,
            IsHistorical = s.Season.IsHistorical,
            HistoricalSourceUrl = s.Season.HistoricalSourceUrl,
            GameVersionID = s.GameVersionID,
            GameVersionName = s.GameVersion.GameVersionName,
            GameVersionSlug = s.GameVersion.Slug,
            PlayerSeasonStatID = s.PlayerSeasonStatID,
            UserID = s.UserID,
            Gamertag = s.Gamertag,
            TeamID = s.TeamID,
            TeamName = s.Team != null ? s.Team.TeamName : null,
            Division = s.Season.Division,
            TradeNote = s.TradeNote,
            RoleNote = s.RoleNote,
            StintIndex = s.StintIndex,
            IsPlayoffs = s.IsPlayoffs,
            IsCombined = s.IsCombined,
            GamesPlayed = s.GamesPlayed,
            Wins = s.Wins,
            Losses = s.Losses,
            WinPercent = s.WinPercent,
            Goals = s.Goals,
            Kills = s.Kills,
            Deaths = s.Deaths,
            Assists = s.Assists,
            KdRatio = s.KdRatio,
            KdSpread = s.KdSpread,
            CarrierKills = s.CarrierKills,
            Sprees = s.Sprees,
            MultiKills = s.MultiKills,
            Exterminations = s.Exterminations,
            Betrayals = s.Betrayals,
            HammerKills = s.HammerKills,
            SwordKills = s.SwordKills,
            BallKills = s.BallKills,
            Punches = s.Punches,
            Score = s.Score,
            DamageDealt = s.DamageDealt,
            DamageTaken = s.DamageTaken,
        }).ToListAsync(ct))
            // The games, where there are any, are the player's stats for that phase of the season; a combined row covers both
            .Where(l => !covered.Contains((l.SeasonID, l.UserID, l.IsPlayoffs)) && !(l.IsCombined && covered.Contains((l.SeasonID, l.UserID, !l.IsPlayoffs))));

        return fromMatches.Concat(storedLines)
            .OrderByDescending(l => l.SeasonStart)
            .ThenBy(l => l.SeasonID)
            .ThenBy(l => l.IsPlayoffs)
            .ThenByDescending(l => l.Goals)
            .ThenByDescending(l => l.Kills)
            .ThenBy(l => l.Gamertag)
            .ThenBy(l => l.StintIndex)
            .ToList();
    }

    /// <summary>
    /// The totals of the games that count for seasons (<see cref="PlayerGameStatsQuery"/>: Waypoint and recorded games in
    /// season matches' series, and recorded games of a season outside any, e.g. Late League scrims), by player, season,
    /// phase (a bracket match is a playoff one) and team. A stat a game doesn't record is left out of the total; one no
    /// game records is null.
    /// </summary>
    private async Task<List<SeasonStatLine>> FromMatchesAsync(int? seasonID, int? userID, CancellationToken ct)
    {
        var query = PlayerGameStatsQuery.Query(_context);
        if (seasonID is not null)
            query = query.Where(g => g.SeasonID == seasonID);
        if (userID is not null)
            query = query.Where(g => g.UserID == userID);
        var games = await query.AsNoTracking().ToListAsync(ct);
        if (games.Count == 0)
            return [];

        var seasonIDs = games.Select(g => g.SeasonID).Distinct().ToList();
        var seasons = await _context.Seasons.AsNoTracking().Where(s => seasonIDs.Contains(s.SeasonID))
            .Select(s => new
            {
                s.SeasonID,
                s.SeasonName,
                s.SeasonStart,
                s.LeagueID,
                s.League.LeagueName,
                LeagueSlug = s.League.Slug,
                s.IsHistorical,
                s.HistoricalSourceUrl,
                s.GameVersionID,
                s.Division,
            })
            .ToDictionaryAsync(s => s.SeasonID, ct);
        var gameVersions = await _context.GameVersions.AsNoTracking().ToDictionaryAsync(g => g.GameVesionID, ct);
        var teamIDs = games.Where(g => g.TeamID is not null).Select(g => g.TeamID!.Value).Distinct().ToList();
        var teamNames = await _context.Teams.AsNoTracking().Where(t => teamIDs.Contains(t.TeamID))
            .ToDictionaryAsync(t => t.TeamID, t => t.TeamName, ct);

        static int? SumOrNull(IEnumerable<int?> values) => values.Any(v => v.HasValue) ? values.Sum(v => v ?? 0) : null;

        return games
            .GroupBy(g => (g.SeasonID, g.UserID, g.IsPlayoffs, g.TeamID))
            .GroupBy(g => (g.Key.SeasonID, g.Key.UserID, g.Key.IsPlayoffs))
            .SelectMany(phase => phase.OrderBy(t => t.Key.TeamID).Select((team, index) =>
            {
                var season = seasons[team.Key.SeasonID];
                // The games' game: a season's games are all one, the season's when they are not known
                var gameVersion = gameVersions.GetValueOrDefault(team.First().GameVersionID)
                    ?? (season.GameVersionID is int id ? gameVersions.GetValueOrDefault(id) : null);
                var played = team.Count();
                var wins = team.Count(g => g.Won);
                var kills = team.Sum(g => g.Kills);
                var deaths = team.Sum(g => g.Deaths);
                return new SeasonStatLine
                {
                    SeasonID = season.SeasonID,
                    SeasonName = season.SeasonName,
                    SeasonStart = season.SeasonStart,
                    LeagueID = season.LeagueID,
                    LeagueName = season.LeagueName,
                    LeagueSlug = season.LeagueSlug,
                    IsHistorical = season.IsHistorical,
                    HistoricalSourceUrl = season.HistoricalSourceUrl,
                    GameVersionID = gameVersion?.GameVesionID,
                    GameVersionName = gameVersion?.GameVersionName,
                    GameVersionSlug = gameVersion?.Slug,
                    Division = season.Division,
                    FromMatches = true,
                    UserID = team.Key.UserID,
                    Gamertag = team.First().Gamertag,
                    TeamID = team.Key.TeamID,
                    TeamName = team.Key.TeamID is int teamID ? teamNames.GetValueOrDefault(teamID) : null,
                    StintIndex = index + 1,
                    IsPlayoffs = team.Key.IsPlayoffs,
                    GamesPlayed = played,
                    Wins = wins,
                    Losses = team.Count(g => g.Lost),
                    WinPercent = Math.Round(100m * wins / played, 3),
                    Goals = team.Sum(g => g.Goals ?? 0),
                    Kills = kills,
                    Deaths = deaths,
                    Assists = team.Sum(g => g.Assists),
                    KdRatio = deaths == 0 ? null : Math.Round((decimal)kills / deaths, 3),
                    KdSpread = kills - deaths,
                    CarrierKills = team.Sum(g => g.CarrierKills ?? 0),
                    RecordsCarrierKills = team.Any(g => g.CarrierKills.HasValue),
                    Betrayals = team.Sum(g => g.Betrayals ?? 0),
                    HammerKills = SumOrNull(team.Select(g => g.HammerKills)),
                    SwordKills = SumOrNull(team.Select(g => g.SwordKills)),
                    BallKills = SumOrNull(team.Select(g => g.BallKills)),
                    Punches = SumOrNull(team.Select(g => g.Punches)),
                    Score = SumOrNull(team.Select(g => g.Score)),
                    DamageDealt = SumOrNull(team.Select(g => g.DamageDealt)),
                    DamageTaken = SumOrNull(team.Select(g => g.DamageTaken)),
                };
            }))
            .ToList();
    }
}
