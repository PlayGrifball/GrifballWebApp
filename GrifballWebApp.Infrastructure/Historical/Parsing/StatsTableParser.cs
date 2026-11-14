using System.Globalization;
using System.Text.Json;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;

namespace GrifballWebApp.Infrastructure.Historical.Parsing;

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
        if (sheet is null || sheet.Rows.Count == 0 || (sheet.Rows.Count < 2 && table.StandingsSheet is null))
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
        var canonicalIds = CanonicalSeasonIds(table);
        var groups = new Dictionary<(string Season, bool Playoffs, string? Division), Dictionary<(string Gamertag, string? Team), List<string[]>>>();
        var skipped = new SkippedRows();
        var at = new RowColumns(seasonColumn, playoffsColumn, gamertagColumn, teamNameColumn, divisionColumn);
        for (var r = 1; r < sheet.Rows.Count; r++)
            AddRow(sheet.Rows[r], table, canonicalIds, at, players, teams, groups, skipped);
        ReportSkipped(sourceId, table.Sheet, skipped, warnings);

        var standings = ParseStandings(sourceId, table, canonicalIds, workbook, warnings);
        foreach (var seasonId in standings.Keys.Where(id => table.Seasons[id].Divisions is null))
            groups.TryAdd((seasonId, false, null), []);

        var games = header.Length;
        var lineColumns = columns.Values.Any(c => c.Key == "gamesPlayed")
            ? columns
            : new Dictionary<int, (string Key, string Header)>(columns) { [games] = ("gamesPlayed", "games") };
        var seasonIds = table.Seasons.Keys.ToList();
        return groups
            .OrderBy(g => seasonIds.IndexOf(g.Key.Season))
            .ThenBy(g => g.Key.Playoffs)
            .ThenBy(g => g.Key.Division is null ? -1 : table.Seasons[g.Key.Season].Divisions!.Values.ToList().IndexOf(g.Key.Division))
            .Select(g => SeasonOf(sourceId, config, table, g.Key, g.Value, columns, lineColumns, games, extended, standings))
            .ToList();
    }

    private static void ReportSkipped(string sourceId, string sheetName, SkippedRows skipped, List<string> warnings)
    {
        foreach (var (id, count) in skipped.UnknownSeasons)
            warnings.Add($"{sourceId}: {sheetName}: {count} row(s) of season {id}, which its config doesn't name, skipped");
        foreach (var (id, count) in skipped.NoDivision)
            warnings.Add($"{sourceId}: {sheetName}: {count} row(s) of season {id} without one of its divisions, skipped");
        if (skipped.NoGamertag > 0)
            warnings.Add($"{sourceId}: {sheetName}: {skipped.NoGamertag} row(s) without a gamertag, skipped");
    }

    /// <summary>Maps each season ID in <see cref="StatsTable.Seasons"/> to the first ID that shares its name and division
    /// (e.g. two conferences of one season).</summary>
    private static Dictionary<string, string> CanonicalSeasonIds(StatsTable table)
    {
        var firstBySeason = new Dictionary<(string Name, string? Division), string>();
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, season) in table.Seasons)
        {
            var key = (season.Name, season.Division);
            if (!firstBySeason.TryGetValue(key, out var canonical))
                firstBySeason[key] = canonical = id;
            map[id] = canonical;
        }
        return map;
    }

    /// <summary>The rows a season, phase and division's lines skipped, by why, for the warnings.</summary>
    private sealed class SkippedRows
    {
        public SortedDictionary<string, int> UnknownSeasons { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, int> NoDivision { get; } = new(StringComparer.Ordinal);
        public int NoGamertag { get; set; }
    }

    /// <summary>The stats table's columns a row is read by; the division's is the teams tab's.</summary>
    private sealed record RowColumns(int Season, int Playoffs, int Gamertag, int TeamName, int Division);

    /// <summary>A stats row into its season, phase and division's player line; one that can't be placed is counted in <paramref name="skipped"/>.</summary>
    private static void AddRow(string[] row, StatsTable table, Dictionary<string, string> canonicalIds, RowColumns at,
        (SheetTable Sheet, Dictionary<string, string[]> Rows, int Column, int RowKey)? players,
        (SheetTable Sheet, Dictionary<string, string[]> Rows, int Column, int RowKey)? teams,
        Dictionary<(string Season, bool Playoffs, string? Division), Dictionary<(string Gamertag, string? Team), List<string[]>>> groups,
        SkippedRows skipped)
    {
        var seasonId = Value(row, at.Season);
        if (seasonId is null || !table.Seasons.TryGetValue(seasonId, out var season))
        {
            var id = seasonId ?? "(none)";
            skipped.UnknownSeasons[id] = skipped.UnknownSeasons.GetValueOrDefault(id) + 1;
            return;
        }
        var teamRow = teams?.Rows.GetValueOrDefault(Value(row, teams.Value.RowKey) ?? "");
        string? division = null;
        if (season.Divisions is not null)
        {
            var value = teamRow is null ? null : Value(teamRow, at.Division);
            if (value is null || !season.Divisions.TryGetValue(value, out division))
            {
                skipped.NoDivision[seasonId] = skipped.NoDivision.GetValueOrDefault(seasonId) + 1;
                return;
            }
        }
        var gamertag = Value(row, at.Gamertag)
            ?? (players is { } p && p.Rows.GetValueOrDefault(Value(row, p.RowKey) ?? "") is { } playerRow ? Value(playerRow, p.Column) : null);
        if (gamertag is null)
        {
            skipped.NoGamertag++;
            return;
        }
        var team = Value(row, at.TeamName) ?? (teamRow is null ? null : Value(teamRow, teams!.Value.Column));
        var key = (canonicalIds[seasonId], Number(row, at.Playoffs) == 1, division);
        if (!groups.TryGetValue(key, out var lines))
            groups[key] = lines = [];
        var line = (gamertag.Trim(), team?.Trim());
        if (!lines.TryGetValue(line, out var rows))
            lines[line] = rows = [];
        rows.Add(row);
    }

    private static ArchiveSeason SeasonOf(string sourceId, HistoricalSourceConfig config, StatsTable table, (string Season, bool Playoffs, string? Division) key,
        Dictionary<(string Gamertag, string? Team), List<string[]>> group, Dictionary<int, (string Key, string Header)> columns,
        Dictionary<int, (string Key, string Header)> lineColumns, int games, List<(int Column, string Metric)> extended,
        Dictionary<string, List<ArchiveTeam>> standings)
    {
        var season = table.Seasons[key.Season];
        var stints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lines = group.Select(l =>
        {
            var (gamertag, tradeNote, roleNote) = ArchiveCells.Gamertag(l.Key.Gamertag);
            var norm = HistoricalKeys.Normalize(gamertag);
            var stint = stints.GetValueOrDefault(norm) + 1;
            stints[norm] = stint;
            var row = Line(l.Value, columns, games, table.PerGame);
            return SeasonStatsParser.Player(row, lineColumns, gamertag, l.Key.Team, tradeNote, roleNote, stint,
                l.Value.Count == 1 ? Extended(l.Value[0], extended) : null);
        }).ToList();
        return new ArchiveSeason
        {
            SeasonName = key.Division is null ? season.Name : $"{season.Name} ({key.Division})",
            GameVersion = table.Game,
            League = config.Season?.League ?? "AGLA",
            Division = key.Division ?? season.Division ?? config.Season?.Division ?? "Open",
            Format = config.Season?.Format,
            Titles = season.Titles,
            SourceYear = season.Year,
            SourceWorkbook = sourceId,
            SourceSheet = $"{table.Sheet}#s{key.Season}" + (key.Division is null ? "" : $"_{key.Division}"),
            IsPlayoffs = key.Playoffs,
            Players = lines,
            // A season split into divisions has each division's teams
            Teams = !key.Playoffs && standings.TryGetValue(key.Season, out var seasonTeams)
                ? seasonTeams.Where(t => key.Division is null || string.Equals(t.Division, key.Division, StringComparison.OrdinalIgnoreCase)).ToList()
                : [],
        };
    }

    private static Dictionary<string, List<ArchiveTeam>> ParseStandings(string sourceId, StatsTable table, Dictionary<string, string> canonicalIds, SheetWorkbook workbook, List<string> warnings)
    {
        if (table.StandingsSheet is null)
            return [];
        var sheet = workbook.Sheet(table.StandingsSheet);
        if (sheet is null || sheet.Rows.Count < 2)
        {
            warnings.Add($"{sourceId}: no sheet '{table.StandingsSheet}', or it is empty");
            return [];
        }
        var header = sheet.Rows[0];
        var seasonCol = Index(header, table.SeasonColumn);
        var teamCol = FirstIndex(header, "team_name", "team");
        if (seasonCol < 0 || teamCol < 0)
        {
            warnings.Add($"{sourceId}: {table.StandingsSheet} has no '{table.SeasonColumn}' or 'team_name' column");
            return [];
        }
        var divCol = FirstIndex(header, "division_name", "division");
        var rankCol = Index(header, "rank");
        var winsCol = Index(header, "wins");
        var lossesCol = Index(header, "losses");
        var gfCol = Index(header, "goals_for");
        var gaCol = Index(header, "goals_against");
        var capCol = FirstIndex(header, "captain_gamertag", "captain");
        var rosterCol = FirstIndex(header, "roster_gamertags", "roster");
        var bySeason = new Dictionary<string, List<ArchiveTeam>>(StringComparer.Ordinal);
        foreach (var row in sheet.Rows.Skip(1))
        {
            if (Value(row, seasonCol) is not { } rawId || !canonicalIds.TryGetValue(rawId, out var seasonId) || Value(row, teamCol) is not { } teamName)
                continue;
            if (!bySeason.TryGetValue(seasonId, out var list))
                bySeason[seasonId] = list = [];
            var roster = Value(row, rosterCol)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [];
            list.Add(new ArchiveTeam
            {
                TeamName = teamName,
                Division = Value(row, divCol),
                Rank = IntValue(row, rankCol),
                Wins = IntValue(row, winsCol),
                Losses = IntValue(row, lossesCol),
                GoalsFor = IntValue(row, gfCol),
                GoalsAgainst = IntValue(row, gaCol),
                Captain = Value(row, capCol),
                Roster = roster,
            });
        }
        return bySeason;
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

    private static int FirstIndex(IReadOnlyList<string> header, params string[] names)
    {
        foreach (var name in names)
            if (Index(header, name) is >= 0 and var col)
                return col;
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

    /// <summary>A cell's whole number; null for none, or one beyond an int (a stray value, never a count).</summary>
    private static int? IntValue(string[] row, int column)
        => Number(row, column) is decimal d && Math.Round(d) is var whole && whole >= int.MinValue && whole <= int.MaxValue ? (int)whole : null;
}
