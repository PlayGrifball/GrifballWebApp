using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Import;

public partial class HistoricalImportService
{
    /// <summary>
    /// The season's forfeits (<see cref="ArchiveForfeitInput"/>): each a season match between its two teams decided by the
    /// forfeit (<see cref="SeasonMatch.Forfeit"/>), with no games. Reconciled with the season's forfeited matches that have
    /// no games and no bracket slot (an imported season's are all the import's: it can't be edited): one of the same teams and result is kept,
    /// the rest are added, and those the archive no longer lists go, so a re-run of the same archive changes nothing. A
    /// forfeit naming a team the season hasn't is left out, and so is a playoff one (the bracket is rebuilt from the games, #511's
    /// brackets), each reported once for the season.
    /// </summary>
    private async Task ImportForfeits(SeasonGroup group, Season season, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var existing = await _context.SeasonMatches
            .Where(m => m.SeasonID == season.SeasonID && !m.SeriesGames.Any() && m.BracketMatch == null
                && (m.HomeTeamResult == SeasonMatchResult.Forfeit || m.AwayTeamResult == SeasonMatchResult.Forfeit))
            .ToListAsync(ct);
        var withoutTeam = new List<string>();
        var playoffs = group.Forfeits!.Where(f => f.IsPlayoffs).Select(f => f.SourceKey).ToList();
        foreach (var forfeit in group.Forfeits!.Where(f => !f.IsPlayoffs))
        {
            if (!ImportForfeit(forfeit, season, teams, existing, counts))
                withoutTeam.Add(forfeit.SourceKey);
        }
        _context.SeasonMatches.RemoveRange(existing);
        counts.SeasonMatchesRemoved += existing.Count;
        await _context.SaveChangesAsync(ct);
        if (playoffs.Count > 0)
            Warn(counts, $"{group.SourceId}: {playoffs.Count} playoff forfeit(s) left out (the bracket is rebuilt from games): {string.Join(", ", playoffs)}");
        if (withoutTeam.Count > 0 && teams.ByName.Count > 0)
            Warn(counts, $"{group.SourceId}: {withoutTeam.Count} forfeit(s) naming a team that is none of the season's, left out: {string.Join(", ", withoutTeam)}");
    }

    /// <summary>One forfeit: the season's match of it kept (taken out of <paramref name="existing"/>), or made. False when
    /// a side is none of the season's teams (or both are the same).</summary>
    private bool ImportForfeit(ArchiveForfeitInput forfeit, Season season, SeasonTeams teams, List<SeasonMatch> existing, HistoricalImportReport counts)
    {
        var (winner, loser) = (teams.Find(forfeit.Winner)?.TeamID, teams.Find(forfeit.Loser)?.TeamID);
        if (winner is null || loser is null || winner == loser)
            return false;
        // The loser's side is always the forfeit: the winner's says whether it was a double one
        var home = forfeit.BothForfeited ? SeasonMatchResult.Forfeit : SeasonMatchResult.Won;
        if (existing.FirstOrDefault(m => m.HomeTeamID == winner && m.AwayTeamID == loser && m.HomeTeamResult == home) is { } same)
        {
            existing.Remove(same);
            return true;
        }
        var match = SeasonMatch.Imported(season.SeasonID, winner, loser, 0, 0, bestOf: 1, scheduledTime: forfeit.PlayedAt, bracketMatch: null);
        match.Forfeit(forfeit.BothForfeited ? Forfeiter.Both : Forfeiter.Away);
        _context.SeasonMatches.Add(match);
        counts.SeasonMatchesCreated++;
        return true;
    }
}
