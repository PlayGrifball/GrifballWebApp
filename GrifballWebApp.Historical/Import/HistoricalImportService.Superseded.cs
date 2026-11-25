using GrifballWebApp.Application.Queries;
using GrifballWebApp.Domain.Archive;
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
    private async Task<List<string>> RetireSupersededSeasons(Dictionary<string, SeasonGroup> groups, HashSet<string> archiveKeys, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var blocked = new List<string>();
        var retired = new HashSet<int>();
        var candidates = groups.Where(g => !g.Value.IsMatchmaking)
            .Select(g => (g.Key, Group: g.Value, Keys: AllKeys(g.Value)
                .Where(k => !archiveKeys.Contains(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()))
            .Where(c => c.Keys.Count > 0).ToList();
        if (candidates.Count == 0)
            return blocked;
        var allKeys = candidates.SelectMany(c => c.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var imported = await _context.Seasons.AsNoTracking()
            .Where(Season.ImportedFromArchive).Where(s => allKeys.Contains(s.HistoricalSourceID))
            .Select(s => new { s.SeasonID, s.HistoricalSourceID, s.SeasonName })
            .ToListAsync(ct);
        foreach (var (groupKey, group, keys) in candidates)
        {
            var leftovers = imported.Where(s => keys.Contains(s.HistoricalSourceID, StringComparer.OrdinalIgnoreCase)).ToList();
            var allGone = true;
            foreach (var season in leftovers.Where(s => !retired.Contains(s.SeasonID)))
                if (await RetireSupersededSeason(season.SeasonID, season.HistoricalSourceID, season.SeasonName, group, report, options, ct))
                    retired.Add(season.SeasonID);
                else
                    allGone = false;
            if (!allGone)
                blocked.Add(groupKey);
        }
        return blocked;
    }

    private async Task<bool> RetireSupersededSeason(int seasonID, string key, string name, SeasonGroup merged, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var gamesComeBack = await RecordedGamesComeBack(seasonID, merged, ct);
        return await RetireSeason(new SeasonRetirement(seasonID, key, name, $"it is part of {merged.SeasonName} now", gamesComeBack ? ["season matches", "recorded games"] : ["season matches"]), async removal =>
        {
            if (gamesComeBack)
                await RemoveRecordedGamesOf(seasonID, ct);
            return await RemoveSeasonWithItsSchedule(seasonID, removal, ct);
        }, report, options, ct);
    }

    /// <summary>The season with its teams, rosters, titles, awards, season matches and any stat rows left.</summary>
    private async Task<HistoricalImportReport> RemoveSeasonWithItsSchedule(int seasonID, HistoricalSeasonRemoval removal, CancellationToken ct)
    {
        var counts = new HistoricalImportReport();
        var season = await _context.Seasons.WithChampionships().WithAwards().SingleAsync(s => s.SeasonID == seasonID, ct);
        removal.TeamsBefore = await _context.Teams.CountAsync(t => t.SeasonID == seasonID, ct);
        removal.TitlesBefore = season.PlayerChampionships.Count;
        removal.AwardsBefore = await _context.Seasons.Awards().CountAsync(a => a.SeasonID == seasonID, ct);
        removal.MatchesBefore = await _context.SeasonMatches.CountAsync(m => m.SeasonID == seasonID, ct);
        await RemoveTeamsNoLongerThere(season, new SeasonTeams(), counts, ct);
        counts.SeasonMatchesRemoved = removal.MatchesBefore;
        (removal.TeamsRemoved, removal.TitlesRemoved, removal.AwardsRemoved, removal.MatchesRemoved) = (counts.TeamsRemoved, removal.TitlesBefore, counts.AwardsRemoved, removal.MatchesBefore);
        var stats = await _context.PlayerSeasonStats.Where(s => s.SeasonID == seasonID).ToListAsync(ct);
        removal.StatsBefore = removal.StatsRemoved = counts.StatsRemoved = stats.Count;
        _context.PlayerSeasonStats.RemoveRange(stats);
        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync(ct);
        return counts;
    }

    /// <summary>
    /// An imported season whose sheet key the archive no longer has, while the sheet it was read from still gives other seasons
    /// (renamed or split: "Summer League 2011" is now "Summer League 2011 (Pro)" and "(Amateur)"), and a season of the DB the archive has, named as its name and a variant in brackets, is the same games: it goes
    /// with its stats and schedule, under the guardrail, unless anything else points at it. A sheet the archive no longer
    /// reads at all keeps its seasons: its source may only have failed to load.
    /// </summary>
    private async Task RetireRenamedSeasons(HashSet<string> archiveKeys, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var sheets = archiveKeys.Select(SheetOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imported = await _context.Seasons.AsNoTracking().Where(Season.ImportedFromArchive)
            .Select(s => new { s.SeasonID, s.HistoricalSourceID, s.SeasonName }).ToListAsync(ct);
        var current = imported.Where(s => archiveKeys.Contains(s.HistoricalSourceID)).ToList();
        foreach (var season in imported.Where(s => !archiveKeys.Contains(s.HistoricalSourceID) && sheets.Contains(SheetOf(s.HistoricalSourceID))))
        {
            var successor = current.Find(c => c.SeasonName.StartsWith(season.SeasonName + " (", StringComparison.OrdinalIgnoreCase));
            if (successor is null)
                continue;
            await RetireSeason(new SeasonRetirement(season.SeasonID, season.HistoricalSourceID, season.SeasonName, $"it is {successor.SeasonName} now", ["stats", "season matches"]),
                removal => RemoveSeasonWithItsSchedule(season.SeasonID, removal, ct), report, options, ct);
        }
    }

    /// <summary>A season key without its season part: the sheet it was read from.</summary>
    private static string SheetOf(string key) => key.Contains('#', StringComparison.Ordinal) ? key[..key.IndexOf('#', StringComparison.Ordinal)] : key;

    /// <summary>The leftover has recorded games, every one imported and one the merged season brings back by its key: the
    /// import makes them again in the merged season, so they need not keep the leftover.</summary>
    private async Task<bool> RecordedGamesComeBack(int seasonID, SeasonGroup merged, CancellationToken ct)
    {
        var games = await _context.RecordedGames.AsNoTracking().Where(g => g.SeasonID == seasonID).Select(g => new { g.SourceKey, g.Origin }).ToListAsync(ct);
        var coming = (merged.RecordedGames ?? []).Select(g => g.SourceKey.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return games.Count > 0 && games.TrueForAll(g => g.Origin != RecordedGameOrigin.Manual && coming.Contains(g.SourceKey));
    }

    /// <summary>Its games go with the season, out of their series and matched matches as a game the archive dropped does.</summary>
    private async Task RemoveRecordedGamesOf(int seasonID, CancellationToken ct)
    {
        var games = await _context.RecordedGames.Where(g => g.SeasonID == seasonID)
            .Include(g => g.Teams).Include(g => g.Participants).Include(g => g.SeriesGame)
            .Include(g => g.MatchedMatch!).ThenInclude(m => m.HomeTeam).Include(g => g.MatchedMatch!).ThenInclude(m => m.AwayTeam).Include(g => g.MatchedMatch!).ThenInclude(m => m.Players)
            .AsSplitQuery().ToListAsync(ct);
        foreach (var game in games)
        {
            if (game.MatchedMatch is { } matched)
                _context.RemoveArchivedMatch(matched);
            if (game.SeriesGame is { } seriesGame)
                _context.Remove(seriesGame);
            _context.RemoveRecordedGame(game);
        }
    }

    /// <summary>
    /// Retires an imported season that no archive season is now, in a transaction of its own and under the guardrail. It
    /// stays, and the report says why, when anything but <paramref name="retiring"/>.IgnoredUses points at it, when the guardrail
    /// stops it, or when it fails. <paramref name="remove"/> removes it (saving) and returns what to add to the report.
    /// </summary>
    /// <returns>True when it is gone (never on a dry run: it is rolled back).</returns>
    private async Task<bool> RetireSeason(SeasonRetirement retiring, Func<HistoricalSeasonRemoval, Task<HistoricalImportReport>> remove, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var (seasonID, key, name, why, ignoredUses) = retiring;
        var used = (await UsesOf(seasonID, ct)).Except(ignoredUses).ToList();
        if (used.Count > 0)
        {
            Warn(report, $"{key}: {why}, but it has {string.Join(", ", used)}; left for a sysadmin");
            return false;
        }

        var removal = new HistoricalSeasonRemoval { SeasonKey = key, SeasonName = name };
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var counts = await remove(removal);
            removal.Stopped = !options.DryRun && options.Guardrail?.Exceeded(removal) == true;
            report.SeasonRemovals.Add(removal);
            if (options.DryRun || removal.Stopped)
            {
                await transaction.RollbackAsync(ct);
                _context.ChangeTracker.Clear();
                if (removal.Stopped)
                    Warn(report, $"{key}: stopped for review, retiring it would remove {removal.Summary}");
                return false;
            }
            await transaction.CommitAsync(ct);
            _context.ChangeTracker.Clear();
            Add(report, counts);
            report.ChampionshipsRemoved += removal.TitlesRemoved;
            report.SeasonsRetired++;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Retiring the season {Season} failed and was rolled back", key);
            report.Errors.Add($"{key}: {ex.GetBaseException().Message}");
            return false;
        }
    }

    /// <summary>The season to retire, and why: <paramref name="IgnoredUses"/> are the things pointing at it that do not keep it.</summary>
    private sealed record SeasonRetirement(int SeasonID, string Key, string? Name, string Why, string[] IgnoredUses);
}
