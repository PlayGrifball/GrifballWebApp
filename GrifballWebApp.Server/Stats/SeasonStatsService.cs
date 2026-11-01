using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Seasons;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Stats;

/// <summary>A player's stats for one phase of a season (regular season or playoffs) on one team, with the season's facts.</summary>
public class SeasonStatLine
{
    public required SeasonInfoDto Season { get; set; }
    public int SeasonID => Season.SeasonID;
    /// <summary>The line's game: its games', which can differ from the season's (a season with Halo 3 and Reach tiers).</summary>
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
    public long Goals { get; set; }
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
    public long? Score { get; set; }
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

        var stored = _context.PlayerSeasonStats.AsNoTracking();
        if (seasonID is not null)
            stored = stored.Where(s => s.SeasonID == seasonID);
        if (userID is not null)
            stored = stored.Where(s => s.UserID == userID);
        var storedRows = (await stored.Select(s => new
        {
            Row = s,
            TeamName = s.Team != null ? s.Team.TeamName : null,
            GameVersionName = s.GameVersion.GameVersionName,
            GameVersionSlug = s.GameVersion.Slug,
        }).ToListAsync(ct))
            // The games, where there are any, are the player's stats for that phase of the season; a combined row covers both
            .Where(r => !covered.Contains((r.Row.SeasonID, r.Row.UserID, r.Row.IsPlayoffs)) && !(r.Row.IsCombined && covered.Contains((r.Row.SeasonID, r.Row.UserID, !r.Row.IsPlayoffs))))
            .ToList();

        var seasons = await SeasonInfoDto.LoadAsync(_context, fromMatches.Select(l => l.SeasonID).Concat(storedRows.Select(r => r.Row.SeasonID)), ct);
        foreach (var line in fromMatches)
            line.Season = seasons[line.Season.SeasonID];
        var storedLines = storedRows.Select(r =>
        {
            var s = r.Row;
            return new SeasonStatLine
            {
                Season = seasons[s.SeasonID],
                GameVersionID = s.GameVersionID,
                GameVersionName = r.GameVersionName,
                GameVersionSlug = r.GameVersionSlug,
                PlayerSeasonStatID = s.PlayerSeasonStatID,
                UserID = s.UserID,
                Gamertag = s.Gamertag,
                TeamID = s.TeamID,
                TeamName = r.TeamName,
                TradeNote = s.TradeNote,
                RoleNote = s.RoleNote,
                StintIndex = s.StintIndex,
                IsPlayoffs = s.IsPlayoffs,
                IsCombined = s.IsCombined,
                GamesPlayed = s.GamesPlayed,
                Wins = s.Wins,
                Losses = s.Losses,
                // A sheet's own figures where it has them, otherwise counted as the games' are
                WinPercent = s.WinPercent ?? StatMath.WinPercent(s.Wins, s.GamesPlayed),
                Goals = s.Goals,
                Kills = s.Kills,
                Deaths = s.Deaths,
                Assists = s.Assists,
                KdRatio = s.KdRatio ?? StatMath.KdRatio(s.Kills, s.Deaths),
                KdSpread = s.KdSpread ?? s.Kills - s.Deaths,
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
            };
        });

        return fromMatches.Concat(storedLines)
            .OrderByDescending(l => l.Season.Start)
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
    /// phase (a bracket match is a playoff one) and team, totalled in the database. A stat a game doesn't record is left
    /// out of the total; one no game records is null. The lines' Season is a stub with its ID until <see cref="GetAsync"/>
    /// loads the seasons.
    /// </summary>
    private async Task<List<SeasonStatLine>> FromMatchesAsync(int? seasonID, int? userID, CancellationToken ct)
    {
        var query = PlayerGameStatsQuery.Query(_context);
        if (seasonID is not null)
            query = query.Where(g => g.SeasonID == seasonID);
        if (userID is not null)
            query = query.Where(g => g.UserID == userID);
        var totals = await query
            .GroupBy(g => new { g.SeasonID, g.UserID, g.IsPlayoffs, g.TeamID })
            .Select(t => new
            {
                t.Key.SeasonID,
                t.Key.UserID,
                t.Key.IsPlayoffs,
                t.Key.TeamID,
                Gamertag = t.Max(g => g.Gamertag),
                GameVersionID = t.Max(g => g.GameVersionID),
                Played = t.Count(),
                Wins = t.Count(g => g.Won),
                Losses = t.Count(g => g.Lost),
                Kills = t.Sum(g => g.Kills),
                Deaths = t.Sum(g => g.Deaths),
                Assists = t.Sum(g => g.Assists),
                Goals = t.Sum(g => (long)(g.Goals ?? 0)),
                CarrierKills = t.Sum(g => g.CarrierKills ?? 0),
                RecordingCarrierKills = t.Count(g => g.CarrierKills != null),
                Betrayals = t.Sum(g => g.Betrayals ?? 0),
                HammerKills = t.Sum(g => g.HammerKills ?? 0),
                RecordingHammerKills = t.Count(g => g.HammerKills != null),
                SwordKills = t.Sum(g => g.SwordKills ?? 0),
                RecordingSwordKills = t.Count(g => g.SwordKills != null),
                BallKills = t.Sum(g => g.BallKills ?? 0),
                RecordingBallKills = t.Count(g => g.BallKills != null),
                Punches = t.Sum(g => g.Punches ?? 0),
                RecordingPunches = t.Count(g => g.Punches != null),
                Score = t.Sum(g => (long)(g.Score ?? 0)),
                RecordingScore = t.Count(g => g.Score != null),
                DamageDealt = t.Sum(g => g.DamageDealt ?? 0),
                RecordingDamageDealt = t.Count(g => g.DamageDealt != null),
                DamageTaken = t.Sum(g => g.DamageTaken ?? 0),
                RecordingDamageTaken = t.Count(g => g.DamageTaken != null),
            })
            .ToListAsync(ct);
        if (totals.Count == 0)
            return [];

        var gameVersions = await _context.GameVersions.AsNoTracking().ToDictionaryAsync(g => g.GameVesionID, ct);
        var teamIDs = totals.Where(t => t.TeamID is not null).Select(t => t.TeamID!.Value).Distinct().ToList();
        var teamNames = await _context.Teams.AsNoTracking().Where(t => teamIDs.Contains(t.TeamID))
            .ToDictionaryAsync(t => t.TeamID, t => t.TeamName, ct);

        static int? IfRecorded(int total, int recording) => recording > 0 ? total : null;
        static long? IfRecordedLong(long total, int recording) => recording > 0 ? total : null;

        return totals
            .GroupBy(t => (t.SeasonID, t.UserID, t.IsPlayoffs))
            .SelectMany(phase => phase.OrderBy(t => t.TeamID).Select((t, index) =>
            {
                var gameVersion = gameVersions.GetValueOrDefault(t.GameVersionID);
                return new SeasonStatLine
                {
                    Season = new SeasonInfoDto { SeasonID = t.SeasonID },
                    GameVersionID = gameVersion?.GameVesionID,
                    GameVersionName = gameVersion?.GameVersionName,
                    GameVersionSlug = gameVersion?.Slug,
                    FromMatches = true,
                    UserID = t.UserID,
                    Gamertag = t.Gamertag,
                    TeamID = t.TeamID,
                    TeamName = t.TeamID is int teamID ? teamNames.GetValueOrDefault(teamID) : null,
                    StintIndex = index + 1,
                    IsPlayoffs = t.IsPlayoffs,
                    GamesPlayed = t.Played,
                    Wins = t.Wins,
                    Losses = t.Losses,
                    WinPercent = StatMath.WinPercent(t.Wins, t.Played),
                    Goals = t.Goals,
                    Kills = t.Kills,
                    Deaths = t.Deaths,
                    Assists = t.Assists,
                    KdRatio = StatMath.KdRatio(t.Kills, t.Deaths),
                    KdSpread = t.Kills - t.Deaths,
                    CarrierKills = t.CarrierKills,
                    RecordsCarrierKills = t.RecordingCarrierKills > 0,
                    Betrayals = t.Betrayals,
                    HammerKills = IfRecorded(t.HammerKills, t.RecordingHammerKills),
                    SwordKills = IfRecorded(t.SwordKills, t.RecordingSwordKills),
                    BallKills = IfRecorded(t.BallKills, t.RecordingBallKills),
                    Punches = IfRecorded(t.Punches, t.RecordingPunches),
                    Score = IfRecordedLong(t.Score, t.RecordingScore),
                    DamageDealt = IfRecorded(t.DamageDealt, t.RecordingDamageDealt),
                    DamageTaken = IfRecorded(t.DamageTaken, t.RecordingDamageTaken),
                };
            }))
            .ToList();
    }
}
