using GrifballWebApp.Database;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>A player's deployment history: every team they were rostered on, with its season, record and finish.</summary>
public class DeploymentHistoryService
{
    private readonly GrifballContext _context;

    public DeploymentHistoryService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The user's teams, newest season first; null when there is no such user.</summary>
    public async Task<List<DeploymentHistoryDto>?> GetAsync(int userID, CancellationToken ct = default)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == userID, ct))
            return null;

        var rosters = await _context.TeamPlayers
            .AsNoTracking()
            .Where(tp => tp.UserID == userID)
            .Select(tp => new
            {
                tp.TeamID,
                tp.Team.SeasonID,
                IsCaptain = tp.Team.CaptainID == tp.TeamPlayerID,
                ChampionshipTier = tp.Team.PlayerChampionships.OrderBy(c => c.Tier).Select(c => c.Tier).FirstOrDefault(),
                PlayedPlayoffs = tp.Team.HomeMatches.Any(m => m.BracketMatch != null)
                    || tp.Team.AwayMatches.Any(m => m.BracketMatch != null)
                    || tp.Team.PlayerSeasonStats.Any(s => s.IsPlayoffs),
            })
            .ToListAsync(ct);
        if (rosters.Count == 0)
            return [];

        var teamIDs = rosters.Select(r => r.TeamID).Distinct().ToList();
        var records = await _context.Teams
            .AsNoTracking()
            .AsSplitQuery()
            .Where(t => teamIDs.Contains(t.TeamID))
            .Select(TeamRecord.Of)
            .ToDictionaryAsync(r => r.TeamID, ct);
        var seasons = await SeasonInfoDto.LoadAsync(_context, rosters.Select(r => r.SeasonID), ct);

        return rosters
            // A player on a team twice (re-added) is one deployment
            .GroupBy(r => r.TeamID)
            .Select(g =>
            {
                var roster = g.First();
                var record = records[roster.TeamID];
                var season = seasons[roster.SeasonID];
                return new DeploymentHistoryDto
                {
                    Season = season,
                    TeamID = roster.TeamID,
                    TeamName = record.TeamName,
                    IsCaptain = g.Any(r => r.IsCaptain),
                    Wins = record.Wins,
                    Losses = record.Losses,
                    RecordFromSource = record.FromSource,
                    Finish = FinishOf(roster.ChampionshipTier is not null, roster.PlayedPlayoffs, season.HasEnded),
                    ChampionshipTier = roster.ChampionshipTier,
                };
            })
            .OrderByDescending(d => d.Season.Start)
            .ThenByDescending(d => d.Season.SeasonID)
            .ThenBy(d => d.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The best a team did: a title, then playoffs; otherwise the regular season once the season is over.</summary>
    public static DeploymentFinish FinishOf(bool champion, bool playedPlayoffs, bool seasonEnded) => (champion, playedPlayoffs, seasonEnded) switch
    {
        (true, _, _) => DeploymentFinish.Champion,
        (_, true, _) => DeploymentFinish.Playoffs,
        (_, _, true) => DeploymentFinish.RegularSeason,
        _ => DeploymentFinish.InProgress,
    };
}
