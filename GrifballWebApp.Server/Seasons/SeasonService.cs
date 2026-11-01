using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Sorting;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Seasons;

public class SeasonService
{
    private readonly GrifballContext _context;
    private readonly SeasonStatsService _stats;
    public SeasonService(GrifballContext context, SeasonStatsService stats)
    {
        _context = context;
        _stats = stats;
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
            .Select(SeasonInfoDto.Select(s => new SeasonSummaryDto
            {
                TeamCount = s.Teams.Count,
                SignupCount = s.SeasonSignups.Count,
                MatchCount = s.SeasonMatches.Count(m => m.BracketMatch == null),
                BracketMatchCount = s.SeasonMatches.Count(m => m.BracketMatch != null),
            }))
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
        if (summary is null)
            return null;
        summary.Champions = (await GetChampions([seasonID], ct)).GetValueOrDefault(seasonID) ?? [];
        summary.PlayerStatCount = (await _stats.GetAsync(seasonID: seasonID, ct: ct)).Count;
        return summary;
    }

    /// <summary>
    /// The season's teams and their rosters: records (calculated from matches or from source sheets), championship titles,
    /// and players (captains first, draft order). Null when there is no such season.
    /// </summary>
    public async Task<List<SeasonTeamDto>?> GetSeasonTeams(int seasonID, CancellationToken ct = default)
    {
        var seasonExists = await _context.Seasons.AnyAsync(s => s.SeasonID == seasonID, ct);
        if (!seasonExists)
            return null;

        var rawTeams = await _context.Teams
            .AsNoTracking()
            .AsSplitQuery()
            .Where(t => t.SeasonID == seasonID)
            .Select(t => new
            {
                t.TeamID,
                t.TeamName,
                Wins = t.RecordedWins != null || t.RecordedLosses != null
                    ? (t.RecordedWins ?? 0)
                    : t.HomeMatches.Count(m => m.HomeTeamResult == SeasonMatchResult.Won) + t.AwayMatches.Count(m => m.AwayTeamResult == SeasonMatchResult.Won),
                Losses = t.RecordedWins != null || t.RecordedLosses != null
                    ? (t.RecordedLosses ?? 0)
                    : t.HomeMatches.Count(m => m.HomeTeamResult == SeasonMatchResult.Loss || m.HomeTeamResult == SeasonMatchResult.Forfeit) + t.AwayMatches.Count(m => m.AwayTeamResult == SeasonMatchResult.Loss || m.AwayTeamResult == SeasonMatchResult.Forfeit),
                TBD = t.RecordedWins != null || t.RecordedLosses != null
                    ? 0
                    : t.HomeMatches.Count(m => m.HomeTeamResult == null) + t.AwayMatches.Count(m => m.AwayTeamResult == null),
                RecordFromSource = t.RecordedWins != null || t.RecordedLosses != null,
                Titles = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToList(),
                CaptainOrder = t.Captain != null ? t.Captain.DraftCaptainOrder : t.TeamPlayers.Where(tp => tp.DraftCaptainOrder != null).Select(tp => tp.DraftCaptainOrder).FirstOrDefault(),
                Players = t.TeamPlayers.Select(tp => new
                {
                    tp.TeamPlayerID,
                    tp.UserID,
                    Gamertag = tp.User.XboxUser != null ? tp.User.XboxUser.Gamertag : null,
                    Discord = tp.User.DiscordUser != null ? tp.User.DiscordUser.DiscordUsername : null,
                    tp.User.DisplayName,
                    tp.User.UserName,
                    tp.DraftCaptainOrder,
                    tp.DraftRound,
                    tp.DraftPick,
                    IsCaptain = tp.CaptainTeam != null || tp.DraftCaptainOrder != null || (tp.Team.CaptainID != null && tp.Team.CaptainID == tp.TeamPlayerID),
                    Region = tp.User.Region != null ? tp.User.Region.RegionName : null,
                }).ToList(),
            })
            .ToListAsync(ct);

        var result = rawTeams.Select(t => new SeasonTeamDto
        {
            TeamID = t.TeamID,
            TeamName = t.TeamName,
            Wins = t.Wins,
            Losses = t.Losses,
            TBD = t.TBD,
            RecordFromSource = t.RecordFromSource,
            Titles = t.Titles,
            DraftCaptainOrder = t.CaptainOrder,
            Players = t.Players.Select(p => new SeasonTeamMemberDto
            {
                TeamPlayerID = p.TeamPlayerID,
                UserID = p.UserID,
                Gamertag = UserNames.Pick(p.Gamertag, p.Discord, p.DisplayName, p.UserName),
                IsCaptain = p.IsCaptain,
                DraftCaptainOrder = p.DraftCaptainOrder,
                DraftRound = p.DraftRound,
                DraftPick = p.DraftPick,
                Region = p.Region,
            })
            .OrderBy(p => p.IsCaptain ? 0 : 1)
            .ThenBy(p => p.DraftRound ?? int.MaxValue)
            .ThenBy(p => p.DraftPick ?? int.MaxValue)
            .ThenBy(p => p.Gamertag)
            .ToList(),
        })
        .OrderBy(t => t.Titles.Count > 0 ? 0 : 1)
        .ThenByDescending(t => t.Wins)
        .ThenBy(t => t.Losses)
        .ThenBy(t => t.DraftCaptainOrder ?? int.MaxValue)
        .ThenBy(t => t.TeamName)
        .ToList();

        return result;
    }

    /// <summary>Every player's stats of the season (regular season and playoffs, each team), most goals first:
    /// from its games where it has them, otherwise the totals stored for it (<see cref="SeasonStatsService"/>).</summary>
    public async Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats(int seasonID, int? teamID = null, CancellationToken ct = default)
    {
        var lines = await _stats.GetAsync(seasonID: seasonID, ct: ct);
        return lines.Where(l => teamID is null || l.TeamID == teamID).Select(SeasonPlayerStatDto.From<SeasonPlayerStatDto>)
            .OrderByDescending(s => s.Goals)
            .ThenByDescending(s => s.Kills)
            .ThenBy(s => s.Gamertag)
            .ThenBy(s => s.IsPlayoffs)
            .ToList();
    }

    /// <summary>
    /// The season list: every season that has started (the current ones included), imported or played in the app
    /// alike, filtered by league, game, format and whether it has ended. Latest end first unless sorted otherwise.
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
        if (filter.Format is not null)
            seasons = seasons.Where(s => s.Format == filter.Format);

        var query = seasons.Select(SeasonInfoDto.Select(s => new SeasonListItemDto { TeamCount = s.Teams.Count }));
        query = filter.SortColumn is null || filter.SortDirection is null or SortDirection.None
            ? query.OrderByDescending(s => s.End).ThenByDescending(s => s.SeasonID)
            : query.OrderBy(filter);
        var page = await query.PaginationResult(filter, ct);

        var ids = page.Results.Select(s => s.SeasonID).ToList();
        // A season's players: its rosters', its stats' and its games' (a player can have stats without a team)
        var rostered = await _context.TeamPlayers.AsNoTracking().Where(tp => ids.Contains(tp.Team.SeasonID))
            .Select(tp => new { tp.Team.SeasonID, tp.UserID }).ToListAsync(ct);
        var withStats = await _context.PlayerSeasonStats.AsNoTracking().Where(s => ids.Contains(s.SeasonID))
            .Select(s => new { s.SeasonID, s.UserID }).ToListAsync(ct);
        var inGames = await PlayerGameStatsQuery.Query(_context).Where(g => ids.Contains(g.SeasonID))
            .Select(g => new { g.SeasonID, g.UserID }).Distinct().ToListAsync(ct);
        var players = rostered.Concat(withStats).Concat(inGames).Distinct().GroupBy(p => p.SeasonID).ToDictionary(g => g.Key, g => g.Count());
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
