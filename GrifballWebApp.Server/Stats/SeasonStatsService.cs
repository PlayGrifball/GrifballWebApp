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
/// player's phase of a season has games in the app (Matches linked to the season's matches, from reported games or
/// the historical import's Waypoint matches), the stats are those games' totals; otherwise they are the season totals
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
    /// The totals of the games linked to seasons' matches, by player, season, phase (a bracket match is a playoff
    /// one) and team: the side of the season match the player is on the roster of. A game's player is the account
    /// of their Xbox user; one with no account is left out (nobody to show the stats for).
    /// </summary>
    private async Task<List<SeasonStatLine>> FromMatchesAsync(int? seasonID, int? userID, CancellationToken ct)
    {
        var participants = _context.MatchParticipants.AsNoTracking().Where(p => p.MatchTeam.Match.MatchLink != null && p.XboxUser.User != null);
        if (seasonID is not null)
            participants = participants.Where(p => p.MatchTeam.Match.MatchLink.SeasonMatch.SeasonID == seasonID);
        if (userID is not null)
            participants = participants.Where(p => p.XboxUser.User!.Id == userID);
        var games = await participants.Select(p => new
        {
            p.MatchTeam.Match.MatchLink.SeasonMatch.SeasonID,
            IsPlayoffs = p.MatchTeam.Match.MatchLink.SeasonMatch.BracketMatch != null,
            p.MatchTeam.Match.MatchLink.SeasonMatch.HomeTeamID,
            p.MatchTeam.Match.MatchLink.SeasonMatch.AwayTeamID,
            UserID = p.XboxUser.User!.Id,
            p.XboxUser.Gamertag,
            p.MatchTeam.Outcome,
            p.Score,
            p.PersonalScore,
            p.Kills,
            p.Deaths,
            p.Assists,
            p.Betrayals,
            p.DamageDealt,
            p.DamageTaken,
        }).ToListAsync(ct);
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
                GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : null,
                GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : null,
                s.Division,
            })
            .ToDictionaryAsync(s => s.SeasonID, ct);
        var userIDs = games.Select(g => g.UserID).Distinct().ToList();
        var rosters = await _context.TeamPlayers.AsNoTracking()
            .Where(tp => seasonIDs.Contains(tp.Team.SeasonID) && userIDs.Contains(tp.UserID))
            .Select(tp => new { tp.TeamID, tp.UserID, tp.Team.TeamName })
            .ToListAsync(ct);
        var onRoster = rosters.ToDictionary(r => (r.TeamID, r.UserID), r => r.TeamName);

        int? TeamOf(int? home, int? away, int user) =>
            home is int h && onRoster.ContainsKey((h, user)) ? h : away is int a && onRoster.ContainsKey((a, user)) ? a : null;

        return games
            .GroupBy(g => (g.SeasonID, g.UserID, g.IsPlayoffs, TeamID: TeamOf(g.HomeTeamID, g.AwayTeamID, g.UserID)))
            .GroupBy(g => (g.Key.SeasonID, g.Key.UserID, g.Key.IsPlayoffs))
            .SelectMany(phase => phase.OrderBy(t => t.Key.TeamID).Select((team, index) =>
            {
                var season = seasons[team.Key.SeasonID];
                var played = team.Count();
                var wins = team.Count(g => g.Outcome == Outcomes.Won);
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
                    GameVersionID = season.GameVersionID,
                    GameVersionName = season.GameVersionName,
                    GameVersionSlug = season.GameVersionSlug,
                    Division = season.Division,
                    FromMatches = true,
                    UserID = team.Key.UserID,
                    Gamertag = team.First().Gamertag,
                    TeamID = team.Key.TeamID,
                    TeamName = team.Key.TeamID is int id ? onRoster[(id, team.Key.UserID)] : null,
                    StintIndex = index + 1,
                    IsPlayoffs = team.Key.IsPlayoffs,
                    GamesPlayed = played,
                    Wins = wins,
                    Losses = team.Count(g => g.Outcome == Outcomes.Lost),
                    WinPercent = Math.Round(100m * wins / played, 3),
                    // Grifball's goals are a player's score
                    Goals = team.Sum(g => g.Score),
                    Kills = kills,
                    Deaths = deaths,
                    Assists = team.Sum(g => g.Assists),
                    KdRatio = deaths == 0 ? null : Math.Round((decimal)kills / deaths, 3),
                    KdSpread = kills - deaths,
                    Betrayals = team.Sum(g => g.Betrayals),
                    Score = team.Sum(g => g.PersonalScore),
                    DamageDealt = team.Sum(g => g.DamageDealt),
                    DamageTaken = team.Sum(g => g.DamageTaken),
                };
            }))
            .ToList();
    }
}
