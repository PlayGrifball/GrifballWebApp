using GrifballWebApp.Application.Database;

using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.TeamStandings;

public class TeamStandingsService
{
    private readonly IApplicationDbContext _context;
    public TeamStandingsService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TeamStandingDto[]> GetTeamStandings(int seasonID, CancellationToken ct = default)
    {
        var records = await _context.Teams
            .AsNoTracking().AsSplitQuery()
            .Where(t => t.SeasonID == seasonID)
            .Select(TeamRecord.Of)
            .OrderByDescending(x => x.Wins)
            .ThenBy(x => x.Losses)
            .ToArrayAsync(ct);
        return records.Select(r => new TeamStandingDto
        {
            TeamID = r.TeamID,
            TeamName = r.TeamName,
            Wins = r.Wins,
            Losses = r.Losses,
            RecordFromSource = r.FromSource,
        }).ToArray();
    }
}
