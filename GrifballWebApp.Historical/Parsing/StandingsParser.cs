using System.Text.RegularExpressions;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A season's team standings (<see cref="StandingsTab"/>): a table of teams, its columns (or, a team per column, its row
/// labels) found by their headers, or a list of teams down a column, read into <see cref="ArchiveTeam"/>s with any of a
/// record (Wins and Losses, or a Record like "12-8 (7-2)", the regular season's first), a rank (Rank, Seed, Standing,
/// Season Standings, or the team's order when the tab is <see cref="StandingsTab.Ranked"/>), a final place (Place
/// Finished, Finish, Playoffs, Playoff Ending: "3rd (Semi-Finals)") and a captain (Captain, or "Team (captain)").
/// A team is listed once, at its first row (a TeamInfo tab has a row per player).
/// </summary>
public static partial class StandingsParser
{
    private enum Field { Team, Wins, Losses, Record, Rank, Finish, Captain }

    private static readonly Dictionary<string, Field> Headers = new(StringComparer.Ordinal)
    {
        ["team"] = Field.Team, ["teams"] = Field.Team, ["team name"] = Field.Team, ["team names"] = Field.Team, ["team standings"] = Field.Team,
        ["w"] = Field.Wins, ["win"] = Field.Wins, ["wins"] = Field.Wins,
        ["l"] = Field.Losses, ["loss"] = Field.Losses, ["losses"] = Field.Losses,
        ["record"] = Field.Record, ["w-l"] = Field.Record,
        ["rank"] = Field.Rank, ["seed"] = Field.Rank, ["standing"] = Field.Rank, ["standings"] = Field.Rank, ["season standing"] = Field.Rank, ["season standings"] = Field.Rank,
        ["place"] = Field.Finish, ["place finished"] = Field.Finish, ["finish"] = Field.Finish, ["final place"] = Field.Finish, ["playoffs"] = Field.Finish,
        ["playoff ending"] = Field.Finish, ["ending"] = Field.Finish,
        ["captain"] = Field.Captain,
    };

    /// <summary>The tab's teams, in its order; null when it has no table (no anchor cell, or no team header).</summary>
    public static List<ArchiveTeam>? Parse(SheetTable sheet, StandingsTab tab)
    {
        if (Anchor(sheet, tab) is not var (row, column))
            return null;
        var teams = tab.Layout switch
        {
            StandingsLayout.Columns => ByColumn(sheet, row, column, tab),
            StandingsLayout.List => ByList(sheet, row, column, tab),
            _ => ByRow(sheet, row, column, tab),
        };
        return teams.Count == 0 ? null : teams;
    }

    /// <summary>A place: the number a cell starts with ("3rd (Semi-Finals)", "7th Place", "Seed 5", "T-3", "12"), 1 or
    /// more; null otherwise, and for a record ("15-14") or anything else ("Draft Spot 1", "#N/A").</summary>
    public static int? Place(string? cell)
        => cell is not null && PlaceRegex().Match(cell) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var place) && place >= 1 ? place : null;

    private static List<ArchiveTeam> ByRow(SheetTable sheet, int headerRow, int teamColumn, StandingsTab tab)
    {
        var columns = RowColumns(sheet.Rows[headerRow], teamColumn);
        var teams = new Teams(tab);
        for (var r = headerRow + 1; r < sheet.Rows.Count; r++)
        {
            var name = sheet.Cell(r, teamColumn).Trim();
            // The header again: another division's standings, ranked on their own
            if (FieldOf(name) == Field.Team)
            {
                teams.NewBlock();
                continue;
            }
            var row = r;
            string Value(Field field) => columns.TryGetValue(field, out var c) ? sheet.Cell(row, c).Trim() : "";
            if (name.Length > 0 && IsTeamRow(columns.Keys, Value))
                teams.Add(name, Value);
        }
        return teams.List;
    }

    /// <summary>A table's columns: from its team column to the right, up to another table's team header, and left of it
    /// too (a TeamInfo tab's "Seed #" in A).</summary>
    private static Dictionary<Field, int> RowColumns(string[] header, int teamColumn)
    {
        var columns = new Dictionary<Field, int>();
        for (var c = teamColumn + 1; c < header.Length; c++)
        {
            var field = FieldOf(header[c]);
            if (field == Field.Team)
                break;
            if (field is { } known)
                columns.TryAdd(known, c);
        }
        for (var c = teamColumn - 1; c >= 0; c--)
            if (FieldOf(header[c]) is { } field and not Field.Team)
                columns.TryAdd(field, c);
        return columns;
    }

    /// <summary>A team's row has a number of the table's (a win, a loss, a record, a place) or, a table of only names and
    /// captains, a value: a row without (a note, another table's) isn't one.</summary>
    private static bool IsTeamRow(IEnumerable<Field> columns, Func<Field, string> value)
    {
        var fields = columns.ToList();
        var numbers = fields.Where(f => f is not Field.Captain).ToList();
        return numbers.Count > 0 ? numbers.Any(f => IsNumber(f, value(f))) : fields.Any(f => value(f).Length > 0);
    }

    private static List<ArchiveTeam> ByColumn(SheetTable sheet, int namesRow, int labelColumn, StandingsTab tab)
    {
        // The labelled rows: the label's first row
        var rows = new Dictionary<Field, int>();
        for (var r = 0; r < sheet.Rows.Count; r++)
            if (r != namesRow && FieldOf(sheet.Cell(r, labelColumn)) is { } field and not Field.Team)
                rows.TryAdd(field, r);
        var teams = new Teams(tab);
        var names = sheet.Rows[namesRow];
        for (var c = labelColumn + 1; c < names.Length; c++)
        {
            var name = names[c].Trim();
            if (FieldOf(name) == Field.Team)
                break;
            if (name.Length > 0)
                teams.Add(name, field => rows.TryGetValue(field, out var r) ? sheet.Cell(r, c).Trim() : "");
        }
        return teams.List;
    }

    private static List<ArchiveTeam> ByList(SheetTable sheet, int anchorRow, int column, StandingsTab tab)
    {
        var teams = new Teams(tab);
        for (var r = anchorRow + 1; r < sheet.Rows.Count; r++)
        {
            var cell = sheet.Cell(r, column).Trim();
            // A team's cell has its record: any other (a note) isn't one
            if (Brackets(cell).Record is not null)
                teams.Add(cell, _ => "");
        }
        return teams.List;
    }

    /// <summary>The tab's teams, once each, in its order.</summary>
    private sealed class Teams(StandingsTab tab)
    {
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The teams before the current block (a division's standings): its first team ranks 1st.</summary>
        private int _blockStart;
        public List<ArchiveTeam> List { get; } = [];

        public void NewBlock() => _blockStart = List.Count;

        public void Add(string cell, Func<Field, string> value)
        {
            var (name, record, others) = Brackets(cell);
            var captain = value(Field.Captain);
            if (tab.CaptainInName && others.Count > 0)
            {
                if (captain.Length == 0)
                    captain = others[0];
                others.RemoveAt(0);
            }
            // A note in brackets stays on the name ("Junkyard Dogs (Winners)"): the archive's builder matches it to the
            // stats' team without it. A record's bracket, "(12-8)", is the record.
            name = string.Concat(name, string.Concat(others.Select(o => $" ({o})")));
            if (!_names.Add(name))
                return;
            var (wins, losses) = (Count(value(Field.Wins)), Count(value(Field.Losses)));
            if (wins is null && losses is null && (RecordRegex().Match(value(Field.Record)) is { Success: true } m ? m : record) is { Success: true } r)
                (wins, losses) = (int.Parse(r.Groups[1].Value), int.Parse(r.Groups[2].Value));
            List.Add(new ArchiveTeam
            {
                TeamName = name,
                Captain = captain.Length == 0 ? null : captain,
                Wins = wins,
                Losses = losses,
                Rank = Place(value(Field.Rank)) ?? (tab.Ranked ? List.Count - _blockStart + 1 : null),
                Finish = Place(value(Field.Finish)),
            });
        }
    }

    /// <summary>A cell's name and its trailing brackets: the first that is a record ("(17-3)"), and the others in order.</summary>
    private static (string Name, Match? Record, List<string> Others) Brackets(string cell)
    {
        var name = cell.Trim();
        var brackets = new List<string>();
        while (TrailingBracketRegex().Match(name) is { Success: true } m && m.Index > 0)
        {
            brackets.Insert(0, m.Groups[1].Value.Trim());
            name = name[..m.Index].TrimEnd();
        }
        Match? record = null;
        var others = new List<string>();
        foreach (var bracket in brackets)
        {
            if (RecordRegex().Match(bracket) is { Success: true } r && r.Length == bracket.Length)
                record ??= r;
            else if (record is null)
                others.Add(bracket);
        }
        return (name, record, others);
    }

    /// <summary>The table's corner: the tab's anchor text, or the first team header with another header of the table beside
    /// it (rows) or a team name after it (columns).</summary>
    private static (int Row, int Column)? Anchor(SheetTable sheet, StandingsTab tab)
    {
        var anchor = tab.Anchor is null ? null : Normalize(tab.Anchor);
        var found = 0;
        for (var r = 0; r < sheet.Rows.Count; r++)
            for (var c = 0; c < sheet.Rows[r].Length; c++)
            {
                var cell = sheet.Rows[r][c];
                if ((anchor is not null ? Normalize(cell) == anchor : FieldOf(cell) == Field.Team && HasTable(sheet.Rows[r], c, tab.Layout))
                    && ++found == tab.AnchorNumber)
                    return (r, c);
            }
        return null;
    }

    private static bool HasTable(string[] row, int column, StandingsLayout layout)
        => layout == StandingsLayout.Columns
            ? row.Skip(column + 1).Any(cell => cell.Trim().Length > 0)
            : row.Where((_, c) => c != column).Any(cell => FieldOf(cell) is { } field && field != Field.Team);

    private static Field? FieldOf(string cell) => Headers.TryGetValue(Normalize(cell), out var field) ? field : null;

    private static string Normalize(string cell)
        => SpacesRegex().Replace(cell.Trim().TrimEnd(':', '#').Trim().ToLowerInvariant(), " ");

    private static int? Count(string cell) => int.TryParse(cell, out var count) && count >= 0 ? count : null;

    private static bool IsNumber(Field field, string cell) => field switch
    {
        Field.Wins or Field.Losses => Count(cell) is not null,
        Field.Record => RecordRegex().IsMatch(cell),
        _ => Place(cell) is not null,
    };

    [GeneratedRegex(@"^\s*(?:seed\s*|T-?)?(\d+)(?:st|nd|rd|th)?\b(?!\s*-\s*\d)", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceRegex();

    [GeneratedRegex(@"\(([^()]*)\)\s*$")]
    private static partial Regex TrailingBracketRegex();

    [GeneratedRegex(@"^\s*(\d+)\s*-\s*(\d+)")]
    private static partial Regex RecordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
