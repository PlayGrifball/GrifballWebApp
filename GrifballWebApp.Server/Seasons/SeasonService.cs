using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Sorting;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Seasons;

public class SeasonService
{
    private readonly GrifballContext _context;
    private readonly SeasonStatsService _stats;
    public SeasonService(GrifballContext context, SeasonStatsService? stats = null)
    {
        _context = context;
        _stats = stats ?? new SeasonStatsService(context);
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
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
        if (summary is null)
            return null;
        summary.Champions = (await GetChampions([seasonID], ct)).GetValueOrDefault(seasonID) ?? [];
        summary.PlayerStatCount = (await _stats.GetAsync(seasonID: seasonID, ct: ct)).Count;
        return summary;
    }

    /// <summary>Every player's stats of the season (regular season and playoffs, each team), most goals first:
    /// from its games where it has them, otherwise the totals stored for it (<see cref="SeasonStatsService"/>).</summary>
    public async Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats(int seasonID, CancellationToken ct = default)
    {
        var lines = await _stats.GetAsync(seasonID: seasonID, ct: ct);
        return lines.Select(s => new SeasonPlayerStatDto
        {
            PlayerSeasonStatID = s.PlayerSeasonStatID,
            UserID = s.UserID,
            Gamertag = s.Gamertag,
            TeamID = s.TeamID,
            TeamName = s.TeamName,
            Division = s.Division,
            TradeNote = s.TradeNote,
            RoleNote = s.RoleNote,
            StintIndex = s.StintIndex,
            IsPlayoffs = s.IsPlayoffs,
            IsCombined = s.IsCombined,
            Source = s.FromMatches ? "Matches" : "Recorded",
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
            .OrderByDescending(s => s.Goals)
            .ThenByDescending(s => s.Kills)
            .ThenBy(s => s.Gamertag)
            .ThenBy(s => s.IsPlayoffs)
            .ToList();
    }

    /// <summary>
    /// The season list: every season that has started (the current ones included), imported or played in the app
    /// alike, filtered by league, game and whether it has ended. Latest end first unless sorted otherwise.
    /// </summary>
    public async Task<PaginationResult<SeasonListItemDto>> GetSeasons(SeasonListFilter filter, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var seasons = _context.Seasons.AsNoTracking().Where(s => s.SeasonStart <= now);
        if (filter.LeagueID is not null)
            seasons = seasons.Where(s => s.LeagueID == filter.LeagueID);
        if (filter.GameVersionID is not null)
            seasons = seasons.Where(s => s.GameVersionID == filter.GameVersionID);
        if (filter.Ended is bool ended)
            seasons = ended ? seasons.Where(s => s.SeasonEnd < now) : seasons.Where(s => now <= s.SeasonEnd);

        var query = seasons.Select(s => new SeasonListItemDto
        {
            SeasonID = s.SeasonID,
            Name = s.SeasonName,
            Start = s.SeasonStart,
            End = s.SeasonEnd,
            Year = s.SeasonStart.Year,
            IsCurrent = now <= s.SeasonEnd,
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
        });
        query = filter.SortColumn is null || filter.SortDirection is null or SortDirection.None
            ? query.OrderByDescending(s => s.End).ThenByDescending(s => s.SeasonID)
            : query.OrderBy(filter);
        var page = await query.PaginationResult(filter, ct);

        var ids = page.Results.Select(s => s.SeasonID).ToList();
        // A season's players: its rosters' and its stats' (a player can have stats without a team)
        var rostered = await _context.TeamPlayers.AsNoTracking().Where(tp => ids.Contains(tp.Team.SeasonID))
            .Select(tp => new { tp.Team.SeasonID, tp.UserID }).ToListAsync(ct);
        var withStats = await _context.PlayerSeasonStats.AsNoTracking().Where(s => ids.Contains(s.SeasonID))
            .Select(s => new { s.SeasonID, s.UserID }).ToListAsync(ct);
        var players = rostered.Concat(withStats).Distinct().GroupBy(p => p.SeasonID).ToDictionary(g => g.Key, g => g.Count());
        var champions = await GetChampions(ids, ct);
        foreach (var season in page.Results)
        {
            season.PlayerCount = players.GetValueOrDefault(season.SeasonID);
            season.Champions = champions.GetValueOrDefault(season.SeasonID) ?? [];
        }
        return page;
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
