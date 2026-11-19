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
/// Finished, Finish, Playoffs, Playoff Ending: "3rd (Semi-Finals)"), a captain (Captain, or "Team (captain)") and its other
/// players (Player 1, Player 2, ..., a gamertag each, a nickname in brackets after it left off).
/// A team is listed once, at its first row (a TeamInfo tab has a row per player).
/// </summary>
public static partial class StandingsParser
{
    private enum Field { Team, Wins, Losses, Record, Rank, Finish, Captain, Player }

    private static readonly Dictionary<string, Field> Headers = new(StringComparer.Ordinal)
    {
        ["team"] = Field.Team, ["teams"] = Field.Team, ["team name"] = Field.Team, ["team names"] = Field.Team, ["team standings"] = Field.Team,
        ["w"] = Field.Wins, ["win"] = Field.Wins, ["wins"] = Field.Wins, ["won"] = Field.Wins,
        ["l"] = Field.Losses, ["loss"] = Field.Losses, ["losses"] = Field.Losses,
        ["record"] = Field.Record, ["w-l"] = Field.Record,
        ["rank"] = Field.Rank, ["seed"] = Field.Rank, ["place"] = Field.Rank, ["standing"] = Field.Rank, ["standings"] = Field.Rank, ["season standing"] = Field.Rank, ["season standings"] = Field.Rank, ["ps"] = Field.Rank,
        ["place finished"] = Field.Finish, ["finish"] = Field.Finish, ["final place"] = Field.Finish, ["playoffs"] = Field.Finish,
        ["playoff ending"] = Field.Finish, ["ending"] = Field.Finish,
        ["captain"] = Field.Captain,
    };

    /// <summary>The tab's teams, in its order; null when it has no table (no anchor cell, or no team header).</summary>
    public static List<ArchiveTeam>? Parse(SheetTable sheet, StandingsTab tab)
    {
        if (tab.Layout == StandingsLayout.CaptainRows)
        {
            var captainTeams = ByCaptainRows(sheet, tab);
            return captainTeams.Count == 0 ? null : captainTeams;
        }
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

    /// <summary>A place: the number a cell starts with ("3rd (Semi-Finals)", "7th Place", "Seed 5", "T-3", "12", a range
    /// "5th-8th"), 1 or more; null otherwise, and for a record ("15-14") or anything else ("Draft Spot 1", "#N/A").</summary>
    public static int? Place(string? cell)
    {
        if (cell is null)
            return null;
        var m = PlaceRegex().Match(cell);
        return m.Success && int.TryParse(m.Groups[1].Value, out var place) && place >= 1 ? place : null;
    }

    private static List<ArchiveTeam> ByRow(SheetTable sheet, int headerRow, int teamColumn, StandingsTab tab)
    {
        var (columns, players) = RowColumns(sheet.Rows[headerRow], teamColumn);
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
            // An empty team cell after its teams ends the table, unless another division's header comes next: what's
            // further down the column (another list, a schedule) isn't the standings'. So does a players table's header
            // (a roster tab's Gamertag | Restrictions below its teams)
            if ((name.Length == 0 && teams.List.Count > 0 && !NextNameIsAHeader(sheet, r, teamColumn)) || IsPlayersHeader(name))
                break;
            var row = r;
            string Value(Field field) => columns.TryGetValue(field, out var c) ? sheet.Cell(row, c).Trim() : "";
            var roster = players.Select(c => Gamertag(sheet.Cell(row, c))).Where(p => p.Length > 0).ToList();
            if (name.Length > 0 && IsTeamRow(columns.Keys, Value, roster.Count > 0))
                teams.Add(name, Value, roster);
        }
        return teams.List;
    }

    private static List<ArchiveTeam> ByCaptainRows(SheetTable sheet, StandingsTab tab)
    {
        var teams = new Teams(tab);
        var (winsCol, lossesCol) = (-1, -1);
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var w = Enumerable.Range(0, row.Length).FirstOrDefault(c => FieldOf(row[c]) == Field.Wins, -1);
            var l = Enumerable.Range(0, row.Length).FirstOrDefault(c => FieldOf(row[c]) == Field.Losses, -1);
            if (w >= 0 || l >= 0)
            {
                (winsCol, lossesCol) = (w, l);
                if (teams.List.Count > 0)
                    teams.NewBlock();
                continue;
            }
            if (r + 1 >= sheet.Rows.Count)
                continue;
            var below = sheet.Rows[r + 1];
            var captainCol = Enumerable.Range(1, Math.Max(0, below.Length - 1))
                .FirstOrDefault(c => FieldOf(below[c]) == Field.Captain, -1);
            if (captainCol <= 0)
                continue;
            var teamCol = Enumerable.Range(0, captainCol).FirstOrDefault(c => !string.IsNullOrWhiteSpace(sheet.Cell(r, c)), -1);
            if (teamCol < 0)
                continue;
            var teamName = sheet.Cell(r, teamCol).Trim();
            var captain = Gamertag(sheet.Cell(r, captainCol));
            var end = winsCol >= 0 ? winsCol : lossesCol >= 0 ? lossesCol : row.Length;
            var roster = Enumerable.Range(captainCol, Math.Max(0, Math.Min(end, row.Length) - captainCol))
                .Select(c => Gamertag(row[c]))
                .Where(p => p.Length > 0)
                .ToList();
            var wins = winsCol >= 0 ? sheet.Cell(r, winsCol).Trim() : "";
            var losses = lossesCol >= 0 ? sheet.Cell(r, lossesCol).Trim() : "";
            teams.Add(teamName, f => f switch
            {
                Field.Captain => captain,
                Field.Wins => wins,
                Field.Losses => losses,
                _ => "",
            }, roster);
        }
        return teams.List;
    }

    private static bool IsPlayersHeader(string cell) => Normalize(cell) is "gamertag" or "gamertags" or "player" or "players" or "player name";

    /// <summary>A player's cell as a gamertag: without the nickname in brackets after it ("Ire Paroxysm (Hide)") or a role
    /// note ("(c)").</summary>
    public static string Gamertag(string cell) => ArchiveCells.Gamertag(WithoutNote(cell)).Gamertag;

    /// <summary>The next row below <paramref name="row"/> with a name in the team column is a team header.</summary>
    private static bool NextNameIsAHeader(SheetTable sheet, int row, int teamColumn)
    {
        for (var r = row + 1; r < sheet.Rows.Count; r++)
        {
            var next = sheet.Cell(r, teamColumn).Trim();
            if (next.Length > 0)
                return FieldOf(next) == Field.Team;
        }
        return false;
    }

    /// <summary>A table's columns: from its team column to the right, and to the left (a TeamInfo tab's "Seed #" in A),
    /// each way up to another table's team header or a gap of two empty header cells (a table beside it); and its players'
    /// columns, in order.</summary>
    private static (Dictionary<Field, int> Columns, List<int> Players) RowColumns(string[] header, int teamColumn)
    {
        var fields = Scan(header, teamColumn, 1).Concat(Scan(header, teamColumn, -1)).ToList();
        var columns = new Dictionary<Field, int>();
        foreach (var (field, c) in fields.Where(f => f.Field != Field.Player))
            columns.TryAdd(field, c);
        return (columns, fields.Where(f => f.Field == Field.Player).Select(f => f.Column).Order().ToList());
    }

    /// <summary>The header's known columns from the team column one way, up to another table's team header or two empty cells.</summary>
    private static IEnumerable<(Field Field, int Column)> Scan(string[] header, int teamColumn, int step)
    {
        var empty = 0;
        for (var c = teamColumn + step; c >= 0 && c < header.Length; c += step)
        {
            // Two empty header cells in a row end the table (one is a merged header's)
            empty = header[c].Trim().Length == 0 ? empty + 1 : 0;
            var field = FieldOf(header[c]);
            if (empty == 2 || field == Field.Team)
                yield break;
            if (field is { } known)
                yield return (known, c);
        }
    }

    /// <summary>A team's row has a number of the table's (a win, a loss, a record, a place) or, a table of only names,
    /// captains and players, a name: a row without (a note, another table's) isn't one.</summary>
    private static bool IsTeamRow(IEnumerable<Field> columns, Func<Field, string> value, bool hasPlayers)
    {
        var fields = columns.ToList();
        var numbers = fields.Where(f => f is not Field.Captain).ToList();
        return numbers.Count > 0 ? numbers.Any(f => IsNumber(f, value(f))) : hasPlayers || fields.Any(f => value(f).Length > 0);
    }

    private static List<ArchiveTeam> ByColumn(SheetTable sheet, int namesRow, int labelColumn, StandingsTab tab)
    {
        var rows = LabelledRows(sheet, namesRow, labelColumn);
        var teams = new Teams(tab);
        var names = sheet.Rows[namesRow];
        // A team's standing: its record or its rank (not a final place, which only the playoff teams have)
        var standings = rows.Keys.Where(f => f is Field.Wins or Field.Losses or Field.Record or Field.Rank).ToList();
        string Value(int column, Field field) => rows.TryGetValue(field, out var r) ? sheet.Cell(r, column).Trim() : "";
        bool HasStanding(int column) => standings.Any(f => IsNumber(f, Value(column, f)));
        var columns = new List<int>();
        for (var c = labelColumn + 1; c < names.Length; c++)
        {
            var name = names[c].Trim();
            if (name.Length == 0)
                continue;
            // Another table's header ends the teams: one of the table's labels (a list of records beside it), or a team
            // header over no standing (a team named "Team Name" has one: H3 Action 2022)
            if (FieldOf(name) is { } field && (field != Field.Team || !HasStanding(c)))
                break;
            columns.Add(c);
        }
        // The columns after the last team with a standing are a table beside the teams (H3 Action 2022's chart)
        var last = columns.FindLastIndex(HasStanding);
        foreach (var column in last < 0 ? columns : columns.Take(last + 1))
            teams.Add(names[column].Trim(), field => Value(column, field));
        return teams.List;
    }

    /// <summary>A team-per-column block's labelled rows: below its names, up to the next block's names row; and above them
    /// (a chart's "Season Standings" over "Team Name:") when no block is above it. Never another block's.</summary>
    private static Dictionary<Field, int> LabelledRows(SheetTable sheet, int namesRow, int labelColumn)
    {
        bool IsNamesRow(int r) => FieldOf(sheet.Cell(r, labelColumn)) == Field.Team;
        var rows = new Dictionary<Field, int>();
        void Take(int r)
        {
            if (FieldOf(sheet.Cell(r, labelColumn)) is { } field)
                rows.TryAdd(field, r);
        }
        for (var r = namesRow + 1; r < sheet.Rows.Count && !IsNamesRow(r); r++)
            Take(r);
        if (!Enumerable.Range(0, namesRow).Any(IsNamesRow))
            for (var r = namesRow - 1; r >= 0; r--)
                Take(r);
        return rows;
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

        public void Add(string cell, Func<Field, string> value, List<string>? roster = null)
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
            if (wins is null && losses is null)
            {
                // The Record column's, else the name's bracket
                var column = RecordRegex().Match(value(Field.Record));
                var r = column.Success ? column : record;
                if (r?.Success == true)
                    (wins, losses) = (int.Parse(r.Groups[1].Value), int.Parse(r.Groups[2].Value));
            }
            List.Add(new ArchiveTeam
            {
                TeamName = name,
                Captain = captain.Length == 0 ? null : captain,
                Wins = wins,
                Losses = losses,
                Rank = Place(value(Field.Rank)) ?? (tab.Ranked ? List.Count - _blockStart + 1 : null),
                Finish = Place(value(Field.Finish)),
                Roster = roster ?? [],
            });
        }
    }

    /// <summary>A name without the note in brackets at its end: "Puffy Grifbussy (WINNER)" is "Puffy Grifbussy"; a name
    /// that is all brackets stays as it is.</summary>
    public static string WithoutNote(string name)
    {
        var trimmed = name.Trim();
        var m = TrailingBracketRegex().Match(trimmed);
        return m.Success && m.Index > 0 ? trimmed[..m.Index].TrimEnd() : trimmed;
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
            : row.Where((_, c) => c != column).Any(cell => FieldOf(cell) is { } and not (Field.Team or Field.Player));

    private static Field? FieldOf(string cell)
    {
        var normalized = Normalize(cell);
        if (Headers.TryGetValue(normalized, out var field))
            return field;
        return PlayerRegex().IsMatch(normalized) ? Field.Player : null;
    }

    private static string Normalize(string cell)
        => SpacesRegex().Replace(cell.Trim().TrimEnd(':', '#').Trim().ToLowerInvariant(), " ");

    private static int? Count(string cell) => int.TryParse(cell, out var count) && count >= 0 ? count : null;

    private static bool IsNumber(Field field, string cell) => field switch
    {
        Field.Wins or Field.Losses => Count(cell) is not null,
        Field.Record => RecordRegex().IsMatch(cell),
        _ => Place(cell) is not null,
    };

    [GeneratedRegex(@"^\s*(?:seed\s*|T-?)?(\d+)(?:(?:st|nd|rd|th)\b|\b(?!\s*-\s*\d))", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceRegex();

    [GeneratedRegex(@"\(([^()]*)\)\s*$")]
    private static partial Regex TrailingBracketRegex();

    [GeneratedRegex(@"^\s*(\d+)\s*-\s*(\d+)")]
    private static partial Regex RecordRegex();

    [GeneratedRegex(@"^player \d+$")]
    private static partial Regex PlayerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
