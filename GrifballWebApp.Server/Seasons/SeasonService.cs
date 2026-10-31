using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Seasons;

public class SeasonService
{
    private readonly GrifballContext _context;
    public SeasonService(GrifballContext context)
    {
        _context = context;
    }

    public Task<int> GetCurrentSeasonID(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _context.Seasons
            .Where(season => season.SeasonStart <= now && now <= season.SeasonEnd)
            .Select(season => season.SeasonID)
            .FirstOrDefaultAsync(ct);
    }

    public Task<string?> GetSeasonName(int seasonID, CancellationToken ct = default)
    {
        return _context.Seasons
            .Where(season => season.SeasonID == seasonID)
            .Select(season => season.SeasonName)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>The season's page header: what it is, its champions and how much data of each kind it has. Null when
    /// there is no such season.</summary>
    public async Task<SeasonSummaryDto?> GetSeasonSummary(int seasonID, CancellationToken ct = default)
    {
        var summary = await _context.Seasons.AsNoTracking()
            .Where(s => s.SeasonID == seasonID)
            .Select(s => new SeasonSummaryDto
            {
                SeasonID = s.SeasonID,
                SeasonName = s.SeasonName,
                Year = s.SeasonStart.Year,
                LeagueID = s.LeagueID,
                LeagueName = s.League.LeagueName,
                GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : null,
                GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : null,
                Division = s.Division,
                IsHistorical = s.IsHistorical,
                HistoricalSourceUrl = s.HistoricalSourceUrl,
                TeamCount = s.Teams.Count,
                SignupCount = s.SeasonSignups.Count,
                MatchCount = s.SeasonMatches.Count(m => m.BracketMatch == null),
                BracketMatchCount = s.SeasonMatches.Count(m => m.BracketMatch != null),
                PlayerStatCount = _context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
        if (summary is null)
            return null;
        summary.Champions = (await GetChampions([seasonID], ct)).GetValueOrDefault(seasonID) ?? [];
        return summary;
    }

    /// <summary>Every player's stat rows of the season (regular season and playoffs, each stint), most goals first.</summary>
    public Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats(int seasonID, CancellationToken ct = default)
    {
        return _context.PlayerSeasonStats.AsNoTracking()
            .Where(s => s.SeasonID == seasonID)
            .OrderByDescending(s => s.Goals)
            .ThenByDescending(s => s.Kills)
            .ThenBy(s => s.Gamertag)
            .Select(s => new SeasonPlayerStatDto
            {
                PlayerSeasonStatID = s.PlayerSeasonStatID,
                UserID = s.UserID,
                Gamertag = s.Gamertag,
                TeamID = s.TeamID,
                TeamName = s.Team != null ? s.Team.TeamName : null,
                Division = s.Division,
                TradeNote = s.TradeNote,
                RoleNote = s.RoleNote,
                StintIndex = s.StintIndex,
                IsPlayoffs = s.IsPlayoffs,
                IsCombined = s.IsCombined,
                Source = s.Source == StatSource.Waypoint ? nameof(StatSource.Waypoint) : nameof(StatSource.Manual),
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
            })
            .ToListAsync(ct);
    }

    /// <summary>Every season that has started, newest first, imported or played in the app alike: the archive's catalog.</summary>
    public async Task<List<SeasonCatalogItemDto>> GetSeasonCatalog(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var seasons = await _context.Seasons.AsNoTracking()
            .Where(s => s.SeasonStart <= now)
            .OrderByDescending(s => s.SeasonStart)
            .ThenByDescending(s => s.SeasonID)
            .Select(s => new SeasonCatalogItemDto
            {
                SeasonID = s.SeasonID,
                SeasonName = s.SeasonName,
                Year = s.SeasonStart.Year,
                LeagueID = s.LeagueID,
                LeagueName = s.League.LeagueName,
                LeagueSlug = s.League.Slug,
                GameVersionID = s.GameVersionID,
                GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : null,
                GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : null,
                Division = s.Division,
                IsHistorical = s.IsHistorical,
                HistoricalSourceUrl = s.HistoricalSourceUrl,
                TeamCount = s.Teams.Count,
            })
            .ToListAsync(ct);
        var ids = seasons.Select(s => s.SeasonID).ToList();

        // A season's players: its rosters' and its stat rows' (a player can have stats without a team)
        var rostered = await _context.TeamPlayers.AsNoTracking().Where(tp => ids.Contains(tp.Team.SeasonID))
            .Select(tp => new { tp.Team.SeasonID, tp.UserID }).ToListAsync(ct);
        var withStats = await _context.PlayerSeasonStats.AsNoTracking().Where(s => ids.Contains(s.SeasonID))
            .Select(s => new { s.SeasonID, s.UserID }).ToListAsync(ct);
        var players = rostered.Concat(withStats).Distinct().GroupBy(p => p.SeasonID).ToDictionary(g => g.Key, g => g.Count());
        var champions = await GetChampions(ids, ct);
        foreach (var season in seasons)
        {
            season.PlayerCount = players.GetValueOrDefault(season.SeasonID);
            season.Champions = champions.GetValueOrDefault(season.SeasonID) ?? [];
        }
        return seasons;
    }

    /// <summary>The seasons' champions, by season: each tier's team and the players who hold its title, captain first.</summary>
    private async Task<Dictionary<int, List<SeasonChampionDto>>> GetChampions(List<int> seasonIDs, CancellationToken ct)
    {
        var titles = await _context.PlayerChampionships.AsNoTracking()
            .Where(c => seasonIDs.Contains(c.SeasonID))
            .Select(c => new { c.SeasonID, c.Tier, c.TeamID, c.Team.TeamName, c.UserID, c.GamertagUsed, c.IsCaptain, c.PlayerChampionshipID })
            .ToListAsync(ct);
        return titles.GroupBy(c => c.SeasonID).ToDictionary(
            season => season.Key,
            season => season.GroupBy(c => new { c.Tier, c.TeamID, c.TeamName })
                .OrderBy(t => t.Min(c => c.PlayerChampionshipID))
                .Select(t => new SeasonChampionDto
                {
                    Tier = t.Key.Tier,
                    TeamID = t.Key.TeamID,
                    TeamName = t.Key.TeamName,
                    Roster = t.OrderByDescending(c => c.IsCaptain).ThenBy(c => c.PlayerChampionshipID)
                        .Select(c => new ChampionPlayerDto { UserID = c.UserID, Gamertag = c.GamertagUsed, IsCaptain = c.IsCaptain })
                        .ToList(),
                })
                .ToList());
    }
}
