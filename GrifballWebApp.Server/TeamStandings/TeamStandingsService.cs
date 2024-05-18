using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamStandings;

public class TeamStandingsService
{
    private readonly GrifballContext _context;
    public TeamStandingsService(GrifballContext context)
    {
        _context = context;
    }

    public Task<TeamStandingDto[]> GetTeamStandings(int seasonID, CancellationToken ct = default)
    {
        return _context.Teams
            .AsNoTracking().AsSplitQuery()
            .Where(t => t.SeasonID == seasonID)
            .Select(t => new TeamStandingDto()
            {
                TeamID = t.TeamID,
                TeamName = t.TeamName,
                Wins = t.HomeMatches.Where(x => x.HomeTeamResult == Database.Models.SeasonMatchResult.Won).Count() +
                       t.AwayMatches.Where(x => x.AwayTeamResult == Database.Models.SeasonMatchResult.Won).Count(),
                Losses = t.HomeMatches.Where(x => x.HomeTeamResult == Database.Models.SeasonMatchResult.Loss).Count() +
                         t.AwayMatches.Where(x => x.AwayTeamResult == Database.Models.SeasonMatchResult.Loss).Count(),
            })
            .OrderByDescending(x => x.Wins)
            .ThenBy(x => x.Losses)
            .ToArrayAsync(ct);
    }
}
