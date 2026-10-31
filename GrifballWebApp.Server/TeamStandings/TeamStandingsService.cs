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
                RecordFromSource = t.RecordedWins != null || t.RecordedLosses != null,
                // A team of a season whose matches are not in the app has the record its source spreadsheet gives
                Wins = t.RecordedWins ?? t.HomeMatches.Where(x => x.HomeTeamResult == Database.Models.SeasonMatchResult.Won).Count() +
                       t.AwayMatches.Where(x => x.AwayTeamResult == Database.Models.SeasonMatchResult.Won).Count(),
                Losses = t.RecordedLosses ?? t.HomeMatches.Where(x => x.HomeTeamResult == Database.Models.SeasonMatchResult.Loss || x.HomeTeamResult == Database.Models.SeasonMatchResult.Forfeit).Count() +
                         t.AwayMatches.Where(x => x.AwayTeamResult == Database.Models.SeasonMatchResult.Loss || x.AwayTeamResult == Database.Models.SeasonMatchResult.Forfeit).Count(),
            })
            .OrderByDescending(x => x.Wins)
            .ThenBy(x => x.Losses)
            .ToArrayAsync(ct);
    }
}
