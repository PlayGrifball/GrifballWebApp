using GrifballWebApp.Domain.League;
using System.Globalization;
using System.Text.RegularExpressions;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A Halo Infinite season's game history tab: the Waypoint (or Halo Data Hive, HaloQuery) match links in its cells,
/// and the two names its Home and Away columns give each row's games. Its first column splits it into weeks; from a row
/// whose first cell says Playoffs (Finals, Semis, Quarters, Bracket) on, games are playoff games, until a row names a
/// week again.
/// </summary>
public static partial class WaypointMatchParser
{
    /// <summary>Cells of a Home or Away column that are not a team: headers repeated mid-sheet, placeholders, statuses.</summary>
    private static readonly HashSet<string> ExcludedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "home", "away", "team", "vs", "vs.",
        "home team", "home squad", "home roster",
        "away team", "away squad", "away roster",
        "team name", "team names", "team 1", "team 2",
        "tbd", "tba", "bye", "n/a", "na", "-", "--", "none",
        "forfeit", "ff", "double forfeit",
        "postponed", "rescheduled", "cancelled", "canceled"
    };

    /// <summary>Words that make a Home/Away header a column of something else than the team (its score, its link).</summary>
    private static readonly string[] NotTeamHeaderWords = ["win", "loss", "score", "pt", "goal", "link", "url", "result", "seed", "rank", "stat"];

    public static (List<string> Regular, List<string> Playoffs, List<ScheduledGame> Schedule) Parse(SheetTable sheet, List<string> unparsed, bool bareIds = false,
        string? sourceId = null, Dictionary<string, ArchiveOrigin>? origins = null)
    {
        var at = new SheetOrigins(sourceId, sheet);
        var regular = new List<string>();
        var playoffs = new List<string>();
        var schedule = new List<ScheduledGame>();
        var inPlayoffs = false;
        var (homeCol, awayCol, headerRow) = FindScheduleColumns(sheet);
        // A row of a series' next games with blank Home and Away cells (merged on the sheet) is the row above's teams
        (string Home, string Away)? sides = null;

        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var first = row.Length > 0 ? row[0].Trim() : "";
            if (first.Length > 0)
                inPlayoffs = PlayoffsPattern().IsMatch(first);

            var found = RowMatchIds(sheet.Name, row, unparsed, bareIds);
            var ids = found.Select(f => f.Id).ToList();
            // Each match's first link cell
            foreach (var (id, column) in found)
                if (origins is not null && at.Cell(r, column) is { } cell)
                    origins.TryAdd(id, cell);
            (inPlayoffs ? playoffs : regular).AddRange(ids);

            if (headerRow < 0 || r <= headerRow)
                continue;
            var (homeCell, awayCell) = (sheet.Cell(r, homeCol).Trim(), sheet.Cell(r, awayCol).Trim());
            if (homeCell.Length > 0 || awayCell.Length > 0)
                sides = Sides(homeCell, awayCell);
            if (sides is { } both)
                schedule.AddRange(ids.Select(id => new ScheduledGame { MatchId = id, Home = both.Home, Away = both.Away, Origin = at.Row(r) }));
        }
        return (regular, playoffs, schedule);
    }

    /// <summary>The match IDs of a row's links, each with its column; a link without one goes in <paramref name="unparsed"/>.</summary>
    private static List<(string Id, int Column)> RowMatchIds(string sheetName, string[] row, List<string> unparsed, bool bareIds)
    {
        var ids = new List<(string Id, int Column)>();
        for (var c = 0; c < row.Length; c++)
        {
            var cellIds = MatchIds(row[c], bareIds);
            if (cellIds is null)
                unparsed.Add($"{sheetName}: {row[c].Trim()}");
            else
            {
                var column = c;
                ids.AddRange(cellIds.Select(id => (id, column)));
            }
        }
        return ids;
    }

    /// <summary>The two teams a row's Home and Away cells name; null unless both name a team, and not the same one.</summary>
    private static (string Home, string Away)? Sides(string homeCell, string awayCell)
        => TeamName(homeCell) is { } home && TeamName(awayCell) is { } away && !string.Equals(home, away, StringComparison.OrdinalIgnoreCase)
            ? (home, away)
            : null;

    /// <summary>
    /// The team (or captain) a Home or Away cell names, cleaned like a gamertag (role notes such as "(c)" dropped) and cut
    /// to the column's length as the import cuts every team name; null for a cell that is no team: a number, a score, a
    /// time, a link, a placeholder (TBD, Bye, Forfeit) or a whole-cell marker (Week 3, Playoffs, Semifinals).
    /// </summary>
    public static string? TeamName(string? cell)
    {
        var name = HistoricalKeys.CleanGamertag(ArchiveCells.Gamertag(cell).Gamertag);
        if (name.Length == 0
            || ExcludedTokens.Contains(name)
            || double.TryParse(name, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            || LinkPattern().IsMatch(name)
            || GuidPattern().IsMatch(name)
            || MarkerPattern().IsMatch(name)
            || ScorePattern().IsMatch(name)
            || TimePattern().IsMatch(name))
            return null;
        return name.Length > Team.TeamNameColumnLength ? name[..Team.TeamNameColumnLength].TrimEnd() : name;
    }

    /// <summary>
    /// The Home and Away columns, from a header row among the first five. A team header ("Home Team", "Away Captain") is
    /// preferred over a bare "Home"/"Away", which some sheets use for the scores beside the team columns.
    /// </summary>
    private static (int Home, int Away, int HeaderRow) FindScheduleColumns(SheetTable sheet)
    {
        for (var h = 0; h < Math.Min(sheet.Rows.Count, 5); h++)
        {
            var headers = sheet.Rows[h].Select(c => c.Trim().ToLowerInvariant()).ToList();
            var (home, away) = (TeamColumn(headers, "home"), TeamColumn(headers, "away"));
            if (home >= 0 && away >= 0)
                return (home, away, h);
        }
        return (-1, -1, -1);
    }

    private static int TeamColumn(List<string> headers, string side)
    {
        var named = headers.FindIndex(h => h.StartsWith(side + " ") && !NotTeamHeaderWords.Any(w => h.Contains(w)));
        return named >= 0 ? named : headers.IndexOf(side);
    }

    /// <summary>The match IDs a cell links to (several links are several games); null for a link without one.</summary>
    public static List<string>? MatchIds(string? cell, bool bareIds = false)
    {
        var text = cell ?? "";
        if (!LinkPattern().IsMatch(text))
            return bareIds && ExactGuidPattern().IsMatch(text.Trim()) ? [text.Trim().ToLowerInvariant()] : [];
        var found = new List<string>();
        foreach (var part in text.Split([' ', '\t', '\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries).Where(p => LinkPattern().IsMatch(p)))
        {
            var ids = GuidPattern().Matches(part);
            if (ids.Count == 0)
                return null;
            // A match link's ID is its last GUID (/players/<gamertag>/matches/<id>, /Infinite/Match/<id>)
            found.Add(ids[^1].Value.ToLowerInvariant());
        }
        return found.Count > 0 ? found : null;
    }

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase)]
    private static partial Regex ExactGuidPattern();

    [GeneratedRegex(@"(halowaypoint\.com|halodatahive\.com|haloquery\.com)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("playoff|final|semi|quarter|bracket", RegexOptions.IgnoreCase)]
    private static partial Regex PlayoffsPattern();

    /// <summary>A cell that is only a week or playoff round marker ("Week 3", "Playoffs Round 1", "Grand Finals"), not a
    /// name that has such a word in it ("Bracket Busters", "Weekend Warriors").</summary>
    [GeneratedRegex(@"^(week\s*\d+|(winners\s+|losers\s+|upper\s+|lower\s+)?(playoffs?|grand\s+finals?|finals?|semi-?finals?|semis|quarter-?finals?|quarters|bracket)(\s+(round|rd|game|week)\s*\d+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex MarkerPattern();

    [GeneratedRegex(@"^\s*\d+\s*[-–]\s*\d+(\s*\([a-z0-9]+\)|\s*[a-z]+)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"^\s*(\d{1,2}:\d{2}\s*(am|pm)?|(sun|mon|tue|wed|thu|fri|sat\w*)\s+\d{1,2}(:\d{2})?\s*(am|pm)?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();
}
