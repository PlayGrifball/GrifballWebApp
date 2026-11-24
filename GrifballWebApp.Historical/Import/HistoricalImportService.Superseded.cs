using GrifballWebApp.Application.Queries;
using Microsoft.Extensions.Logging;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Import;

public partial class HistoricalImportService
{
    /// <summary>
    /// A season several of whose sheets once made seasons of their own, now made one (its entries share a name): the
    /// imported seasons keyed by its other entries' sheets are leftovers, whatever they have. Each goes with its teams,
    /// rosters, titles, awards and season matches, which the merged season has again, under the guardrail like any
    /// season's removals, unless anything else points at it (stats, signups, VODs, recorded games, claims): then it stays
    /// and the report says why.
    /// </summary>
    private async Task RetireSupersededSeasons(Dictionary<string, SeasonGroup> groups, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var archiveKeys = groups.Values.Select(g => g.SourceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.Values.Where(g => !g.IsMatchmaking))
        {
            var keys = group.Entries.Select(EntrySourceId).OfType<string>()
                .Where(k => !archiveKeys.Contains(k))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (keys.Count == 0)
                continue;
            var leftovers = await _context.Seasons.AsNoTracking()
                .Where(Season.ImportedFromArchive).Where(s => keys.Contains(s.HistoricalSourceID))
                .Select(s => new { s.SeasonID, s.HistoricalSourceID, s.SeasonName })
                .ToListAsync(ct);
            foreach (var season in leftovers)
                await RetireSupersededSeason(season.SeasonID, season.HistoricalSourceID, season.SeasonName, group.SeasonName, report, options, ct);
        }
    }

    private async Task RetireSupersededSeason(int seasonID, string key, string name, string mergedInto, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var used = (await UsesOf(seasonID, ct)).Where(u => u != "season matches").ToList();
        if (used.Count > 0)
        {
            Warn(report, $"{key}: it is part of {mergedInto} now, but it has {string.Join(", ", used)}; left for a sysadmin");
            return;
        }

        var removal = new HistoricalSeasonRemoval { SeasonKey = key, SeasonName = name };
        var counts = new HistoricalImportReport();
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var season = await _context.Seasons.WithChampionships().SingleAsync(s => s.SeasonID == seasonID, ct);
            removal.TeamsBefore = await _context.Teams.CountAsync(t => t.SeasonID == seasonID, ct);
            removal.TitlesBefore = season.PlayerChampionships.Count;
            removal.AwardsBefore = await _context.Seasons.Awards().CountAsync(a => a.SeasonID == seasonID, ct);
            await RemoveTeamsNoLongerThere(season, new SeasonTeams(), counts, ct);
            (removal.TeamsRemoved, removal.TitlesRemoved, removal.AwardsRemoved) = (counts.TeamsRemoved, removal.TitlesBefore, counts.AwardsRemoved);
            _context.Seasons.Remove(season);
            await _context.SaveChangesAsync(ct);

            removal.Stopped = !options.DryRun && options.Guardrail?.Exceeded(removal) == true;
            report.SeasonRemovals.Add(removal);
            if (options.DryRun || removal.Stopped)
            {
                await transaction.RollbackAsync(ct);
                _context.ChangeTracker.Clear();
                if (removal.Stopped)
                    Warn(report, $"{key}: stopped for review, retiring it would remove {removal.Summary}");
                return;
            }
            await transaction.CommitAsync(ct);
            _context.ChangeTracker.Clear();
            Add(report, counts);
            report.ChampionshipsRemoved += removal.TitlesRemoved;
            report.SeasonsRetired++;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Retiring the superseded season {Season} failed and was rolled back", key);
            report.Errors.Add($"{key}: {ex.GetBaseException().Message}");
        }
    }
}
