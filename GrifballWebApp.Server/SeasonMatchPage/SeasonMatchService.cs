using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.SeasonMatchPage;

public class SeasonMatchService
{
    private readonly GrifballContext _context;
    public SeasonMatchService(GrifballContext context)
    {
        _context = context;
    }

    public async Task<SeasonMatchPageDto?> GetSeasonMatchPage(int seasonMatchID, CancellationToken ct = default)
    {
        var seasonMatch = await _context.SeasonMatches
            .Include(x => x.MatchLinks)
            .Include(x => x.Season)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Include(x => x.BracketMatch)
            .Where(sm => sm.SeasonMatchID == seasonMatchID)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (seasonMatch is null)
            return null;

        return new SeasonMatchPageDto()
        {
            SeasonID = seasonMatch.SeasonID,
            SeasonName = seasonMatch.Season.SeasonName,
            IsPlayoff = seasonMatch.BracketMatch is not null,
            HomeTeamName = seasonMatch.HomeTeam?.TeamName,
            HomeTeamID = seasonMatch.HomeTeam?.TeamID,
            HomeTeamScore = seasonMatch.HomeTeamScore,
            AwayTeamName = seasonMatch.AwayTeam?.TeamName,
            AwayTeamID = seasonMatch.AwayTeam?.TeamID,
            AwayTeamScore = seasonMatch.AwayTeamScore,
            ScheduledTime = seasonMatch.ScheduledTime,
            BestOf = seasonMatch.BestOf,
            ReportedGames = seasonMatch.MatchLinks.Select(x => new ReportedGameDto()
            {
                MatchID = x.MatchID,
                MatchNumber = x.MatchNumber
            }).ToArray(),
        };
    }
}
