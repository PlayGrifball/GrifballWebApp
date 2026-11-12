using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.TeamPage;

/// <summary>A team's page: its season, record, roster and titles.</summary>
public class TeamPageService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;

    public TeamPageService(IApplicationDbContext context, TimeProvider? time = null)
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

    /// <summary>A team's matches, soonest unplayed first, then played ones by scheduled time.</summary>
    public Task<MatchDto[]> GetMatchesAsync(int teamID, CancellationToken ct = default)
    {
        return _context.SeasonMatches.Where(x => x.HomeTeamID == teamID)
            .Select(x => new MatchDto()
            {
                SeasonMatchID = x.SeasonMatchID,
                ScheduledTime = x.ScheduledTime,
                BestOf = x.BestOf,
                OtherTeamID = x.AwayTeamID,
                OtherTeamName = x.AwayTeam!.TeamName,
                Score = x.HomeTeamScore,
                OtherScore = x.AwayTeamScore,
                Result = x.HomeTeamResult,
                OtherResult = x.AwayTeamResult,
            })
            .Concat(_context.SeasonMatches.Where(x => x.AwayTeamID == teamID)
                .Select(x => new MatchDto()
                {
                    SeasonMatchID = x.SeasonMatchID,
                    ScheduledTime = x.ScheduledTime,
                    BestOf = x.BestOf,
                    OtherTeamID = x.HomeTeamID,
                    OtherTeamName = x.HomeTeam!.TeamName,
                    Score = x.AwayTeamScore,
                    OtherScore = x.HomeTeamScore,
                    Result = x.AwayTeamResult,
                    OtherResult = x.HomeTeamResult,
                }))
            // Order so that matches that still need to be played are first, in order of scheduled date.
            .OrderBy(x => x.Result == null ? 0 : 1) // Note: use Result and not Score which will be null in case of forfeits/byes
            .ThenBy(x => x.ScheduledTime)
            .ToArrayAsync(ct);
    }
}
