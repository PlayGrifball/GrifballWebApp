using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Import;

public partial class HistoricalImportService
{
    /// <summary>The season's matches with no games: its forfeits and its scheduled matches with no result, each when the
    /// archive has them for it.</summary>
    private async Task ImportGamelessMatches(SeasonGroup group, Season season, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        if (group.Forfeits is not null)
            await ImportForfeits(group, season, teams, counts, ct);
        if (group.ScheduledMatches is not null)
            await ImportScheduledMatches(group, season, teams, counts, ct);
    }

    /// <summary>
    /// The season's scheduled matches with no result (<see cref="ArchiveScheduledMatchInput"/>): each a season match between its
    /// two teams on its day, with no games and no result. Reconciled with the season's matches that have no games, no result and
    /// no bracket slot: one of the same teams and day is kept, the rest are added, and those the archive no longer lists go, so
    /// a re-run of the same archive changes nothing. A match naming a team the season hasn't is left out, reported once for the season.
    /// </summary>
    private async Task ImportScheduledMatches(SeasonGroup group, Season season, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var existing = await _context.SeasonMatches
            .Where(m => m.SeasonID == season.SeasonID && !m.SeriesGames.Any() && m.BracketMatch == null
                && m.HomeTeamResult == null && m.AwayTeamResult == null)
            .ToListAsync(ct);
        var withoutTeam = new List<string>();
        foreach (var scheduled in group.ScheduledMatches!)
        {
            var (home, away) = (teams.Find(scheduled.Home)?.TeamID, teams.Find(scheduled.Away)?.TeamID);
            if (home is null || away is null || home == away)
            {
                withoutTeam.Add(scheduled.SourceKey);
                continue;
            }
            var why = $"{scheduled.Home} vs. {scheduled.Away} on {scheduled.PlayedAt:yyyy-MM-dd HH:mm} UTC, no result";
            if (existing.FirstOrDefault(m => m.HomeTeamID == home && m.AwayTeamID == away && m.ScheduledTime == scheduled.PlayedAt) is { } same)
            {
                existing.Remove(same);
                _provenance.Read(same, scheduled.Origin, why);
                continue;
            }
            var match = SeasonMatch.Between(season.SeasonID, home.Value, away.Value, bestOf: 1, scheduledTime: scheduled.PlayedAt);
            _context.SeasonMatches.Add(match);
            _provenance.Read(match, scheduled.Origin, why);
            counts.SeasonMatchesCreated++;
        }
        foreach (var gone in existing)
            gone.Cancel();
        _context.SeasonMatches.RemoveRange(existing);
        counts.SeasonMatchesRemoved += existing.Count;
        await _context.SaveChangesAsync(ct);
        if (withoutTeam.Count > 0)
            Warn(counts, $"{group.SourceId}: {withoutTeam.Count} scheduled match(es) naming a team that is none of the season's, left out: {string.Join(", ", withoutTeam)}");
    }
}
