using System.Globalization;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Historical;

/// <summary>A row of where the historical import got something from (<see cref="HistoricalProvenance"/>), with its source's
/// title and links: the workbook, and the cells themselves when the source is one Google Sheets tab.</summary>
public sealed record HistoricalProvenanceDto
{
    public required long HistoricalProvenanceID { get; init; }
    public required string Entity { get; init; }
    public required string EntityKey { get; init; }
    public required HistoricalProvenanceKind Kind { get; init; }
    public required DateTime RecordedAt { get; init; }
    public int? HistoricalImportJobID { get; init; }
    public int? HistoricalSourceSnapshotID { get; init; }
    public required string SourceID { get; init; }
    public string? SourceTitle { get; init; }
    /// <summary>The link people open (the workbook, the page).</summary>
    public string? SourceUrl { get; init; }
    public string? Sheet { get; init; }
    public string? Cells { get; init; }
    /// <summary>The cells in Google Sheets, when the source is one tab of it (its gid known) and the row names them.</summary>
    public string? CellsUrl { get; init; }
    public string? Value { get; init; }
    public string? Detail { get; init; }
}

/// <summary>
/// Reads where the historical import got its rows from (<see cref="HistoricalProvenance"/>), for sysadmins tracking bad data
/// back to its sheet: an entity's rows (a user's with those of the placeholders merged into them), and what one source or
/// tab made.
/// </summary>
public class HistoricalProvenanceService(IApplicationDbContext context)
{
    /// <summary>The most rows one lookup returns.</summary>
    public const int MaxRows = 500;
    /// <summary>The longest merge chain followed back from a user (a placeholder merged into one merged into another).</summary>
    private const int MaxMergeDepth = 10;

    /// <summary>
    /// <paramref name="entity"/> <paramref name="key"/>'s rows, the cells that made it first, then the ones the latest run read
    /// it from, then the changes. A user's include the rows of every user merged into them (the placeholders the import
    /// made, which a merge folds into the real account).
    /// </summary>
    public async Task<List<HistoricalProvenanceDto>> ForEntity(string entity, string key, CancellationToken ct)
    {
        var keys = new List<string> { key.Trim() };
        if (entity == nameof(Domain.Players.User) && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var userID))
            keys.AddRange((await MergedInto(userID, ct)).Select(id => id.ToString(CultureInfo.InvariantCulture)));
        var rows = await context.HistoricalProvenance.AsNoTracking()
            .Where(p => p.Entity == entity && keys.Contains(p.EntityKey))
            .OrderBy(p => p.Kind).ThenBy(p => p.SourceID).ThenBy(p => p.Sheet).ThenBy(p => p.Cells).ThenBy(p => p.HistoricalProvenanceID)
            .Take(MaxRows)
            .ToListAsync(ct);
        return await Dtos(rows, ct);
    }

    /// <summary>What <paramref name="sourceID"/> (its <paramref name="sheet"/> only, when given) made and was read for, by
    /// entity and cells, a page at a time.</summary>
    public async Task<List<HistoricalProvenanceDto>> ForSource(string sourceID, string? sheet, int skip, int take, CancellationToken ct)
    {
        var query = context.HistoricalProvenance.AsNoTracking().Where(p => p.SourceID == sourceID);
        if (!string.IsNullOrWhiteSpace(sheet))
            query = query.Where(p => p.Sheet == sheet);
        var rows = await query
            .OrderBy(p => p.Sheet).ThenBy(p => p.Cells).ThenBy(p => p.Entity).ThenBy(p => p.EntityKey).ThenBy(p => p.HistoricalProvenanceID)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, MaxRows))
            .ToListAsync(ct);
        return await Dtos(rows, ct);
    }

    /// <summary>Every user merged into <paramref name="userID"/>, directly or through another merged user.</summary>
    private async Task<List<int>> MergedInto(int userID, CancellationToken ct)
    {
        var found = new HashSet<int> { userID };
        var frontier = new List<int> { userID };
        for (var depth = 0; depth < MaxMergeDepth && frontier.Count > 0; depth++)
        {
            var targets = frontier;
            frontier = (await context.UserMerges.AsNoTracking().Where(m => targets.Contains(m.TargetUserID)).Select(m => m.SourceUserID).ToListAsync(ct))
                .Where(found.Add).ToList();
        }
        found.Remove(userID);
        return [.. found];
    }

    private async Task<List<HistoricalProvenanceDto>> Dtos(List<HistoricalProvenance> rows, CancellationToken ct)
    {
        var sourceIDs = rows.Select(r => r.SourceID).Distinct().ToList();
        var sources = await context.HistoricalSources.AsNoTracking().Where(s => sourceIDs.Contains(s.HistoricalSourceID))
            .Select(s => new { s.HistoricalSourceID, s.Title, s.Url, s.SheetKey, s.Gid, s.CsvSheetName })
            .ToDictionaryAsync(s => s.HistoricalSourceID, ct);
        return [.. rows.Select(r =>
        {
            var source = sources.GetValueOrDefault(r.SourceID);
            return new HistoricalProvenanceDto
            {
                HistoricalProvenanceID = r.HistoricalProvenanceID,
                Entity = r.Entity,
                EntityKey = r.EntityKey,
                Kind = r.Kind,
                RecordedAt = r.RecordedAt,
                HistoricalImportJobID = r.HistoricalImportJobID,
                HistoricalSourceSnapshotID = r.HistoricalSourceSnapshotID,
                SourceID = r.SourceID,
                SourceTitle = source?.Title,
                SourceUrl = source?.Url,
                Sheet = r.Sheet,
                Cells = r.Cells,
                CellsUrl = source is null ? null : CellsUrl(source.SheetKey, source.Gid, source.CsvSheetName, r.Sheet, r.Cells),
                Value = r.Value,
                Detail = r.Detail,
            };
        })];
    }

    /// <summary>
    /// The cells in Google Sheets: only for a source that is one tab (its gid), the tab the row names. A whole workbook's tabs
    /// have gids its xlsx export doesn't give, so its rows link the workbook (<see cref="HistoricalProvenanceDto.SourceUrl"/>).
    /// </summary>
    public static string? CellsUrl(string? sheetKey, string? gid, string? csvSheetName, string? sheet, string? cells)
    {
        if (string.IsNullOrWhiteSpace(sheetKey) || string.IsNullOrWhiteSpace(gid) || string.IsNullOrWhiteSpace(cells))
            return null;
        if (csvSheetName is not null && sheet is not null && !string.Equals(csvSheetName.Trim(), sheet.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;
        return $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(sheetKey)}/edit#gid={Uri.EscapeDataString(gid)}&range={Uri.EscapeDataString(cells)}";
    }
}
