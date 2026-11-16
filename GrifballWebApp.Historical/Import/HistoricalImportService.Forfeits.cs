using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Import;

public partial class HistoricalImportService
{
    /// <summary>
    /// The season's forfeits (<see cref="ArchiveForfeitInput"/>): each a season match between its two teams decided by the
    /// forfeit (<see cref="SeasonMatch.Forfeit"/>), with no games. Reconciled with the season's forfeited matches that have
    /// no games (an imported season's are all the import's: it can't be edited): one of the same teams and result is kept,
    /// the rest are added, and those the archive no longer lists go, so a re-run of the same archive changes nothing. A
    /// forfeit naming a team the season hasn't is left out, reported once for the season.
    /// </summary>
    private async Task ImportForfeits(SeasonGroup group, Season season, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var existing = await _context.SeasonMatches
            .Where(m => m.SeasonID == season.SeasonID && !m.SeriesGames.Any()
                && (m.HomeTeamResult == SeasonMatchResult.Forfeit || m.AwayTeamResult == SeasonMatchResult.Forfeit))
            .ToListAsync(ct);
        var withoutTeam = new List<string>();
        foreach (var forfeit in group.Forfeits!)
        {
            var (winner, loser) = (teams.Find(forfeit.Winner)?.TeamID, teams.Find(forfeit.Loser)?.TeamID);
            if (winner is null || loser is null || winner == loser)
            {
                withoutTeam.Add(forfeit.SourceKey);
                continue;
            }
            var (home, away) = (forfeit.BothForfeited ? SeasonMatchResult.Forfeit : SeasonMatchResult.Won, SeasonMatchResult.Forfeit);
            var same = existing.FirstOrDefault(m => m.HomeTeamID == winner && m.AwayTeamID == loser && m.HomeTeamResult == home && m.AwayTeamResult == away);
            if (same is not null)
            {
                existing.Remove(same);
                continue;
            }
            var match = SeasonMatch.Imported(season.SeasonID, winner, loser, 0, 0, bestOf: 1, scheduledTime: forfeit.PlayedAt, bracketMatch: null);
            match.Forfeit(forfeit.BothForfeited ? Forfeiter.Both : Forfeiter.Away);
            _context.SeasonMatches.Add(match);
            counts.SeasonMatchesCreated++;
        }
        _context.SeasonMatches.RemoveRange(existing);
        counts.SeasonMatchesRemoved += existing.Count;
        await _context.SaveChangesAsync(ct);
        if (withoutTeam.Count > 0 && teams.ByName.Count > 0)
            Warn(counts, $"{group.SourceId}: {withoutTeam.Count} forfeit(s) naming a team that is none of the season's, left out: {string.Join(", ", withoutTeam)}");
    }
}
