using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>A player's deployment history: every team they were rostered on, with its season, record and finish.</summary>
public class DeploymentHistoryService
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;

    public DeploymentHistoryService(GrifballContext context, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = context;
    }

    /// <summary>The user's teams, newest season first; null when there is no such user.</summary>
    public async Task<List<DeploymentHistoryDto>?> GetAsync(int userID, CancellationToken ct = default)
    {
        // A team holds a player once (TeamPlayers is unique on UserID and TeamID), so each team is one deployment
        var teams = await _context.Teams
            .AsNoTracking()
            .Where(t => t.TeamPlayers.Any(tp => tp.UserID == userID))
            .Select(TeamRecord.Select(t => new DeploymentRow
            {
                SeasonID = t.SeasonID,
                IsCaptain = t.Apply(Team.CaptainedBy(userID)),
                ChampionshipTiers = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToList(),
                PlayedPlayoffs = t.HomeMatches.Any(m => m.BracketMatch != null)
                    || t.AwayMatches.Any(m => m.BracketMatch != null)
                    || t.PlayerSeasonStats.Any(s => s.IsPlayoffs),
            }))
            .ToListAsync(ct);
        // A player on no team still has a profile; only a user who does not exist has none
        if (teams.Count == 0)
            return await _context.Users.AnyAsync(u => u.Id == userID, ct) ? [] : null;

        var seasons = await SeasonInfoDto.LoadAsync(_context, teams.Select(t => t.SeasonID), _time.GetUtcNow().UtcDateTime, ct);

        return teams
            .Select(team =>
            {
                var season = seasons[team.SeasonID];
                var tiers = team.ChampionshipTiers.Order(StringComparer.OrdinalIgnoreCase).ToList();
                return new DeploymentHistoryDto
                {
                    Season = season,
                    TeamID = team.TeamID,
                    TeamName = team.TeamName,
                    IsCaptain = team.IsCaptain,
                    Wins = team.Wins,
                    Losses = team.Losses,
                    RecordFromSource = team.FromSource,
                    Finish = FinishOf(season.Format, tiers.Count > 0, team.PlayedPlayoffs, season.HasEnded),
                    ChampionshipTiers = tiers,
                };
            })
            .OrderByDescending(d => d.Season.Start)
            .ThenByDescending(d => d.Season.SeasonID)
            .ThenBy(d => d.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The best a team did: a title, then playoffs; otherwise the regular season once the season is over. A tournament is
    /// only a bracket, so no team in it played a regular season and its bracket is not "playoffs": a team without a title
    /// is eliminated once it is over.
    /// </summary>
    public static DeploymentFinish FinishOf(SeasonFormat format, bool champion, bool playedPlayoffs, bool seasonEnded) => (format, champion, playedPlayoffs, seasonEnded) switch
    {
        (_, true, _, _) => DeploymentFinish.Champion,
        (SeasonFormat.Tournament, _, _, true) => DeploymentFinish.Eliminated,
        (SeasonFormat.Tournament, _, _, false) => DeploymentFinish.InProgress,
        (_, _, true, _) => DeploymentFinish.Playoffs,
        (_, _, _, true) => DeploymentFinish.RegularSeason,
        _ => DeploymentFinish.InProgress,
    };

    /// <summary>A team of the player's: its record and what the deployment line needs besides.</summary>
    private sealed class DeploymentRow : TeamRecord
    {
        public int SeasonID { get; set; }
        public bool IsCaptain { get; set; }
        public List<string> ChampionshipTiers { get; set; } = [];
        public bool PlayedPlayoffs { get; set; }
    }
}
