using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>A team's page: its season, record, roster and titles.</summary>
public class TeamPageService
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;

    public TeamPageService(GrifballContext context, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = context;
    }

    /// <summary>
    /// The team: its record (from its matches, or the one its source spreadsheet gives for a season whose matches are
    /// not in the app), roster and titles; its players' stats are Season/GetSeasonPlayerStats?teamID=. Null when there
    /// is no such team.
    /// </summary>
    public async Task<TeamDto?> GetAsync(int teamID, CancellationToken ct = default)
    {
        var team = await _context.Teams.AsNoTracking().Where(x => x.TeamID == teamID)
            .Select(x => new
            {
                x.TeamName,
                x.SeasonID,
                TBD = x.HomeMatches.AsQueryable().Count(SeasonMatch.Undecided) + x.AwayMatches.AsQueryable().Count(SeasonMatch.Undecided),
                Titles = x.PlayerChampionships.Select(c => c.Tier).Distinct().ToArray(),
                Players = x.TeamPlayers.AsQueryable().Select(RosterPlayer.Of).ToList(),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;
        // Counted as the standings count it
        var record = await _context.Teams.Where(x => x.TeamID == teamID).Select(TeamRecord.Of).FirstAsync(ct);
        var season = (await SeasonInfoDto.LoadAsync(_context, [team.SeasonID], _time.GetUtcNow().UtcDateTime, ct))[team.SeasonID];
        return new TeamDto
        {
            TeamName = team.TeamName,
            Season = season,
            Wins = record.Wins,
            Losses = record.Losses,
            RecordFromSource = record.FromSource,
            TBD = team.TBD,
            Titles = team.Titles,
            Players = RosterPlayer.ToPlayers(team.Players),
        };
    }
}
