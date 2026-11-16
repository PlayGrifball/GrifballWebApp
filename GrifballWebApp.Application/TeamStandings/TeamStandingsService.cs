using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;

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
    /// (each rank once, at least half its teams ranked) in the sheet's order, its tiebreakers included, the teams it doesn't
    /// rank last. Several divisions' tables rank from 1st each, and a sheet that seeds only a few teams (or ties two)
    /// doesn't order the season: those keep the records' order. A tournament whose sheet gives where its teams finished is
    /// in that order (its ranks are the seeds going into its bracket, not a result).
    /// </summary>
    public async Task<TeamStandingDto[]> GetTeamStandings(int seasonID, CancellationToken ct = default)
    {
        var records = await _context.Teams
            .AsNoTracking().AsSplitQuery()
            .Where(t => t.SeasonID == seasonID)
            .Select(TeamRecord.Select(t => new StandingRecord { Rank = t.ArchivedRank, Finish = t.ArchivedFinish }))
            .ToArrayAsync(ct);
        var tournament = await _context.Seasons.AnyAsync(s => s.SeasonID == seasonID && s.Format == SeasonFormat.Tournament, ct);
        var ranks = records.Select(r => r.Rank).OfType<int>().ToList();
        var bySheet = ranks.Count > 0 && ranks.Count * 2 >= records.Length && ranks.Distinct().Count() == ranks.Count;
        var ordered = (tournament && records.Any(r => r.Finish is not null)) switch
        {
            true => records.OrderBy(r => r.Finish ?? int.MaxValue).ThenByDescending(r => r.Wins).ThenBy(r => r.Losses),
            false when bySheet => records.OrderBy(r => r.Rank ?? int.MaxValue).ThenByDescending(r => r.Wins).ThenBy(r => r.Losses),
            _ => records.OrderByDescending(r => r.Wins).ThenBy(r => r.Losses),
        };
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
