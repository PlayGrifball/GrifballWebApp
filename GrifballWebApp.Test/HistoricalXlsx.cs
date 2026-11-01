using ClosedXML.Excel;

namespace GrifballWebApp.Test;

/// <summary>Small xlsx workbooks built in the tests (ClosedXML), shaped like the archive's sheets: never real data.</summary>
internal static class HistoricalXlsx
{
    /// <summary>A workbook of sheets, each its rows (null is an empty cell).</summary>
    public static byte[] Workbook(params (string Name, object?[][] Rows)[] sheets)
        => Build(workbook =>
        {
            foreach (var (name, rows) in sheets)
                Fill(workbook.Worksheets.Add(name), rows);
        });

    public static byte[] Build(Action<XLWorkbook> build)
    {
        using var workbook = new XLWorkbook();
        build(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static void Fill(IXLWorksheet sheet, object?[][] rows)
    {
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } value)
                    sheet.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static object?[] Row(params object?[] cells) => cells;

    /// <summary>A season stats tab: Player, Team, Games, Wins, Losses, Goals, Kills, Deaths, Assists.</summary>
    public static object?[][] StatsTab(params (string Gamertag, string Team, int Kills)[] players)
        => [Row("Player", "Team", "Games", "Wins", "Losses", "Goals", "Kills", "Deaths", "Assists"),
            .. players.Select(p => Row(p.Gamertag, p.Team, 4, 2, 2, 1, p.Kills, 10, 3))];
}
