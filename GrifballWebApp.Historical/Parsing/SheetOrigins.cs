using GrifballWebApp.Historical.Import;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// The cells of one tab as <see cref="ArchiveOrigin"/>s, for a parser that runs with or without its workbook's catalog ID
/// (a test's sheet has none): every origin is null without one.
/// </summary>
internal sealed record SheetOrigins(string? SourceId, SheetTable Sheet)
{
    public ArchiveOrigin? Cell(int row, int column) => SourceId is null ? null : ArchiveOrigin.Cell(SourceId, Sheet, row, column);

    public ArchiveOrigin? Row(int row) => SourceId is null ? null : ArchiveOrigin.Row(SourceId, Sheet, row);

    public ArchiveOrigin? Rows(int first, int last) => SourceId is null ? null : ArchiveOrigin.Rows(SourceId, Sheet, first, last);

    /// <summary>Cells of one column (a team's column of a board): the range from the first to the last, their texts one per line.</summary>
    public ArchiveOrigin? Column(int column, IEnumerable<int> rows)
    {
        var list = rows.Distinct().Order().ToList();
        if (SourceId is null || list.Count == 0)
            return null;
        return new ArchiveOrigin(SourceId, Sheet.Name, Sheet.Range(list[0], column, list[^1], column),
            string.Join("\n", list.Select(r => Sheet.Cell(r, column)).Where(t => t.Length > 0)));
    }
}
