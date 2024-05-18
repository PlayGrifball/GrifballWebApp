using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GrifballWebApp.Server.MatchPlanner;

public class MatchPlannerService
{
    private readonly GrifballContext _grifballContext;
    public MatchPlannerService(GrifballContext grifballContext)
    {
        _grifballContext = grifballContext;
    }

    public async Task CreateSeasonMatches(int seasonID, CancellationToken ct = default)
    {
        using var t = await _grifballContext.Database.BeginTransactionAsync(ct);
        var deletedCount = await _grifballContext.SeasonMatches
            .Where(x => x.SeasonID == seasonID && x.BracketMatch == null)
            .ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            // TODO add guard against user deleting unless they approve
        }

        var teams = await _grifballContext.Teams.Where(t => t.SeasonID == seasonID).ToListAsync(ct);

        var newMatches = new List<SeasonMatch>();

        // Each team should have 1 home game against the other teams
        foreach (var homeTeam in teams)
        {
            foreach (var awayTeam in teams.Where(team => team != homeTeam))
            {
                var newMatch = new SeasonMatch()
                {
                    SeasonID = seasonID,
                    HomeTeam = homeTeam,
                    AwayTeam = awayTeam,
                };
                newMatches.Add(newMatch);
            }
        }


        await _grifballContext.SeasonMatches.AddRangeAsync(newMatches);
        await _grifballContext.SaveChangesAsync(ct);

        await t.CommitAsync(ct);
    }

    public async Task<List<UnscheduledMatchDto>> GetUnscheduledMatches(int seasonID, CancellationToken ct = default)
    {
        var seasonMatches = await _grifballContext.SeasonMatches.AsSplitQuery().AsNoTracking()
            .Include(x => x.HomeTeam.Captain.User.XboxUser)
            .Include(x => x.AwayTeam.Captain.User.XboxUser)
            .Where(sm => sm.SeasonID == seasonID && sm.BracketMatch == null && sm.ScheduledTime == null)
            .ToListAsync(ct);

        var ordered = seasonMatches
            .OrderBy(x => x.HomeTeam.Captain.User.XboxUser.Gamertag)
            .ThenBy(x => x.AwayTeam.Captain.User.XboxUser.Gamertag)
            .Select(x => new UnscheduledMatchDto()
            {
                SeasonMatchID = x.SeasonMatchID,
                HomeCaptain = x.HomeTeam.Captain.User.XboxUser.Gamertag,
                AwayCaptain = x.AwayTeam.Captain.User.XboxUser.Gamertag,
            });
        return ordered.ToList();
    }

    public async Task<List<ScheduledMatchDto>> GetScheduledMatches(int seasonID, CancellationToken ct = default)
    {
        var seasonMatches = await _grifballContext.SeasonMatches.AsSplitQuery().AsNoTracking()
            .Include(x => x.HomeTeam.Captain.User.XboxUser)
            .Include(x => x.AwayTeam.Captain.User.XboxUser)
            .Where(sm => sm.SeasonID == seasonID && sm.BracketMatch == null && sm.ScheduledTime != null)
            .ToListAsync(ct);

        var ordered = seasonMatches
            .OrderBy(x => x.ScheduledTime)
            .Select(x => new ScheduledMatchDto()
            {
                SeasonMatchID = x.SeasonMatchID,
                HomeCaptain = x.HomeTeam.Captain.User.XboxUser.Gamertag,
                AwayCaptain = x.AwayTeam.Captain.User.XboxUser.Gamertag,
                Complete = x.HomeTeamResult is not null || x.AwayTeamResult is not null,
                Time = x.ScheduledTime ?? throw new Exception("No time on season match")
            });
        return ordered.ToList();
    }

    public async Task UpdateMatchTime(UpdateMatchTimeDto dto, CancellationToken ct = default)
    {
        var seasonMatch = await _grifballContext.SeasonMatches.Where(x => x.SeasonMatchID == dto.SeasonMatchID).FirstOrDefaultAsync(ct);

        if (seasonMatch is null)
            return;

        seasonMatch.ScheduledTime = dto.Time;

        await _grifballContext.SaveChangesAsync(ct);
    }
}
