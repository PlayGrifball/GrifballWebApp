using System.Globalization;
using System.Text.Json;
using GrifballWebApp.Server.Historical.Sources;

namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>
/// A source's database tables of stats (<see cref="StatsTable"/>, e.g. the grifball.info backup) as archive entries: the
/// rows split by season (the season column, named by the config), division (a column of the team's row) and phase (the
/// playoffs column), each player's line read as a season stats tab's (<see cref="SeasonStatsParser"/>). Rows of a
/// season the config doesn't name, and rows without a gamertag, are skipped and reported.
/// </summary>
public static class StatsTableParser
{
    /// <summary>Stats that are rates, not counts: a player's per-game rows aren't added up into them (the importer works
    /// them out again from the totals).</summary>
    private static readonly HashSet<string> Rates = ["kdRatio", "winPercent", "averageLifeSeconds"];

    public static List<ArchiveSeason> Parse(string sourceId, HistoricalSourceConfig config, SheetWorkbook workbook, List<string> warnings)
    {
        var seasons = new List<ArchiveSeason>();
        foreach (var table in config.Tables)
            seasons.AddRange(ParseTable(sourceId, config, table, workbook, warnings));
        return seasons;
    }

    private static List<ArchiveSeason> ParseTable(string sourceId, HistoricalSourceConfig config, StatsTable table, SheetWorkbook workbook, List<string> warnings)
    {
        var sheet = workbook.Sheet(table.Sheet);
        if (sheet is null || sheet.Rows.Count < 2)
        {
            warnings.Add($"{sourceId}: no sheet '{table.Sheet}', or it is empty");
            return [];
        }
        var header = sheet.Rows[0];
        var seasonColumn = Index(header, table.SeasonColumn);
        if (seasonColumn < 0)
        {
            warnings.Add($"{sourceId}: {table.Sheet} has no '{table.SeasonColumn}' column");
            return [];
        }
        var playoffsColumn = table.PlayoffsColumn is null ? -1 : Index(header, table.PlayoffsColumn);
        var columns = SeasonStatsParser.ColumnsOf(header);
        var gamertagColumn = columns.Where(c => c.Value.Key == "gamertag").Select(c => c.Key).DefaultIfEmpty(-1).Max();
        var teamNameColumn = columns.Where(c => c.Value.Key == "teamName").Select(c => c.Key).DefaultIfEmpty(-1).Max();
        var players = Lookup(sourceId, workbook, table.Players, header, warnings);
        var teams = Lookup(sourceId, workbook, table.Teams, header, warnings);
        var divisionColumn = teams is not null && table.DivisionColumn is not null ? Index(teams.Value.Sheet.Rows[0], table.DivisionColumn) : -1;
        var extended = table.Extended
            .Select(e => (Column: Index(header, e.Key), Metric: e.Value))
            .Where(e => e.Column >= 0)
            .ToList();

        // Each season, phase and division's players, in the order they first appear: a line is a gamertag and team
        var groups = new Dictionary<(string Season, bool Playoffs, string? Division), Dictionary<(string Gamertag, string? Team), List<string[]>>>();
        var unknownSeasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var noGamertag = 0;
        var noDivision = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (var r = 1; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var seasonId = Value(row, seasonColumn);
            if (seasonId is null || !table.Seasons.TryGetValue(seasonId, out var season))
            {
                var id = seasonId ?? "(none)";
                unknownSeasons[id] = unknownSeasons.GetValueOrDefault(id) + 1;
                continue;
            }
            var teamRow = teams?.Rows.GetValueOrDefault(Value(row, teams.Value.RowKey) ?? "");
            string? division = null;
            if (season.Divisions is not null)
            {
                var value = teamRow is null ? null : Value(teamRow, divisionColumn);
                if (value is null || !season.Divisions.TryGetValue(value, out division))
                {
                    noDivision[seasonId] = noDivision.GetValueOrDefault(seasonId) + 1;
                    continue;
                }
            }
            var gamertag = Value(row, gamertagColumn)
                ?? (players is { } p && p.Rows.GetValueOrDefault(Value(row, p.RowKey) ?? "") is { } playerRow ? Value(playerRow, p.Column) : null);
            if (gamertag is null)
            {
                noGamertag++;
                continue;
            }
            var team = Value(row, teamNameColumn) ?? (teamRow is null ? null : Value(teamRow, teams!.Value.Column));
            var key = (seasonId, Number(row, playoffsColumn) == 1, division);
            if (!groups.TryGetValue(key, out var lines))
                groups[key] = lines = [];
            var line = (gamertag.Trim(), team?.Trim());
            if (!lines.TryGetValue(line, out var rows))
                lines[line] = rows = [];
            rows.Add(row);
        }
        foreach (var (id, count) in unknownSeasons)
            warnings.Add($"{sourceId}: {table.Sheet}: {count} row(s) of season {id}, which its config doesn't name, skipped");
        foreach (var (id, count) in noDivision)
            warnings.Add($"{sourceId}: {table.Sheet}: {count} row(s) of season {id} without one of its divisions, skipped");
        if (noGamertag > 0)
            warnings.Add($"{sourceId}: {table.Sheet}: {noGamertag} row(s) without a gamertag, skipped");

        var games = header.Length;
        var lineColumns = columns.Values.Any(c => c.Key == "gamesPlayed")
            ? columns
            : new Dictionary<int, (string Key, string Header)>(columns) { [games] = ("gamesPlayed", "games") };
        var seasonIds = table.Seasons.Keys.ToList();
        return groups
            .OrderBy(g => seasonIds.IndexOf(g.Key.Season))
            .ThenBy(g => g.Key.Playoffs)
            .ThenBy(g => g.Key.Division is null ? -1 : table.Seasons[g.Key.Season].Divisions!.Values.ToList().IndexOf(g.Key.Division))
            .Select(g =>
            {
                var season = table.Seasons[g.Key.Season];
                var stints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var lines = g.Value.Select(l =>
                {
                    var (gamertag, tradeNote, roleNote) = ArchiveCells.Gamertag(l.Key.Gamertag);
                    var stint = stints.GetValueOrDefault(gamertag) + 1;
                    stints[gamertag] = stint;
                    var row = Line(l.Value, columns, games, table.PerGame);
                    return SeasonStatsParser.Player(row, lineColumns, gamertag, l.Key.Team, tradeNote, roleNote, stint,
                        l.Value.Count == 1 ? Extended(l.Value[0], extended) : null);
                }).ToList();
                return new ArchiveSeason
                {
                    SeasonName = g.Key.Division is null ? season.Name : $"{season.Name} ({g.Key.Division})",
                    GameVersion = table.Game,
                    League = config.Season?.League ?? "AGLA",
                    Division = g.Key.Division ?? config.Season?.Division ?? "Open",
                    Format = config.Season?.Format,
                    SourceWorkbook = sourceId,
                    SourceSheet = $"{table.Sheet}#s{g.Key.Season}" + (g.Key.Division is null ? "" : $"_{g.Key.Division}"),
                    IsPlayoffs = g.Key.Playoffs,
                    Players = lines,
                };
            })
            .ToList();
    }

    /// <summary>
    /// A player's line as one row of the table's columns and a games column at <paramref name="games"/>: their only row,
    /// or their rows added up (counts summed, rates left out). Games are the table's own column, or the rows (one game
    /// each, <paramref name="perGame"/>), or wins and losses.
    /// </summary>
    private static string[] Line(List<string[]> rows, Dictionary<int, (string Key, string Header)> columns, int games, bool perGame)
    {
        var line = new string[games + 1];
        foreach (var (c, column) in columns)
        {
            if (rows.Count == 1)
                line[c] = Cell(rows[0], c);
            else if (!Rates.Contains(column.Key) && column.Key is not ("gamertag" or "teamName"))
            {
                var values = rows.Select(r => column.Key == "totalSecondsPlayed" ? ArchiveCells.DurationSeconds(Cell(r, c), column.Header) : ArchiveCells.Number(Cell(r, c)))
                    .Where(v => v is not null).ToList();
                line[c] = values.Count == 0 ? "" : values.Sum()!.Value.ToString(CultureInfo.InvariantCulture);
            }
            else
                line[c] = "";
        }
        decimal Sum(string key) => columns.Where(c => c.Value.Key == key).Select(c => ArchiveCells.Number(line[c.Key]) ?? 0).DefaultIfEmpty(0).Last();
        line[games] = (perGame ? rows.Count : Sum("wins") + Sum("losses")).ToString(CultureInfo.InvariantCulture);
        return line;
    }

    private static JsonElement? Extended(string[] row, List<(int Column, string Metric)> extended)
    {
        var metrics = new Dictionary<string, object>();
        foreach (var (column, metric) in extended)
        {
            if (Value(row, column) is not { } value)
                continue;
            if (metric.EndsWith("Id", StringComparison.Ordinal) || ArchiveCells.Number(value) is not decimal number)
                metrics[metric] = value;
            else
                metrics[metric] = number;
        }
        return metrics.Count == 0 ? null : JsonSerializer.SerializeToElement(metrics);
    }

    /// <summary>Another tab's rows by their key column, the column read from them, and the stats row's key column.</summary>
    private static (SheetTable Sheet, Dictionary<string, string[]> Rows, int Column, int RowKey)? Lookup(string sourceId, SheetWorkbook workbook, TableLookup? lookup, IReadOnlyList<string> header, List<string> warnings)
    {
        if (lookup is null)
            return null;
        var sheet = workbook.Sheet(lookup.Sheet);
        var key = sheet is null || sheet.Rows.Count == 0 ? -1 : Index(sheet.Rows[0], lookup.Key);
        var column = sheet is null || sheet.Rows.Count == 0 ? -1 : Index(sheet.Rows[0], lookup.Column);
        var rowKey = Index(header, lookup.RowKey ?? lookup.Key);
        if (sheet is null || key < 0 || column < 0 || rowKey < 0)
        {
            warnings.Add($"{sourceId}: no '{lookup.Column}' by '{lookup.Key}' in sheet '{lookup.Sheet}'");
            return null;
        }
        var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in sheet.Rows.Skip(1))
            if (Value(row, key) is { } id)
                rows.TryAdd(id, row);
        return (sheet, rows, column, rowKey);
    }

    private static int Index(IReadOnlyList<string> header, string name)
    {
        for (var c = 0; c < header.Count; c++)
            if (string.Equals(header[c].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return c;
        return -1;
    }

    private static string Cell(string[] row, int column) => column >= 0 && column < row.Length ? row[column] : "";

    /// <summary>A cell's text, null for an empty cell and the database's NULL.</summary>
    private static string? Value(string[] row, int column)
    {
        var text = Cell(row, column).Trim();
        return text.Length == 0 || text.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? null : text;
    }

    private static decimal? Number(string[] row, int column) => ArchiveCells.Number(Value(row, column));
}
