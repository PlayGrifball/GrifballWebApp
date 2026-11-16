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

    /// <summary>A team's record and where its source spreadsheet placed it.</summary>
    private sealed class StandingRecord : TeamRecord
    {
        public int? Rank { get; init; }
        public int? Finish { get; init; }
    }

    /// <summary>
    /// The season's teams by their records, most wins first; a season whose source spreadsheet ranks its teams in one table
    /// (each rank once) in the sheet's order, its tiebreakers included, the teams it doesn't rank last. Several divisions'
    /// tables rank from 1st each: their season keeps the records' order.
    /// </summary>
    public async Task<TeamStandingDto[]> GetTeamStandings(int seasonID, CancellationToken ct = default)
    {
        var records = await _context.Teams
            .AsNoTracking().AsSplitQuery()
            .Where(t => t.SeasonID == seasonID)
            .Select(TeamRecord.Select(t => new StandingRecord { Rank = t.ArchivedRank, Finish = t.ArchivedFinish }))
            .ToArrayAsync(ct);
        var ranks = records.Select(r => r.Rank).OfType<int>().ToList();
        var bySheet = ranks.Count > 0 && ranks.Distinct().Count() == ranks.Count;
        var ordered = bySheet
            ? records.OrderBy(r => r.Rank ?? int.MaxValue).ThenByDescending(r => r.Wins).ThenBy(r => r.Losses)
            : records.OrderByDescending(r => r.Wins).ThenBy(r => r.Losses);
        return ordered.Select(r => new TeamStandingDto
        {
            TeamID = r.TeamID,
            TeamName = r.TeamName,
            Wins = r.Wins,
            Losses = r.Losses,
            RecordFromSource = r.FromSource,
            Finish = r.Finish,
        }).ToArray();
    }
}
