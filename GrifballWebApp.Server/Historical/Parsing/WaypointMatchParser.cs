using System.Text.RegularExpressions;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>
/// A Halo Infinite season's game history tab: the Waypoint (or Halo Data Hive, HaloQuery) match links in its cells,
/// and the teams in its Home and Away columns. Its first column splits it into weeks; from a row whose first cell says
/// Playoffs (Finals, Semis, Quarters, Bracket) on, games are playoff games, until a row names a week again.
/// </summary>
public static partial class WaypointMatchParser
{
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

    public static (List<string> Regular, List<string> Playoffs, List<string> Teams) Parse(SheetTable sheet, List<string> unparsed)
    {
        var regular = new List<string>();
        var playoffs = new List<string>();
        var teams = new List<string>();
        var teamSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inPlayoffs = false;

        var (homeCol, awayCol, headerRow) = FindScheduleColumns(sheet);

        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var first = row.Length > 0 ? row[0].Trim() : "";
            if (first.Length > 0)
                inPlayoffs = PlayoffsPattern().IsMatch(first);

            if (r > headerRow && homeCol >= 0 && awayCol >= 0)
            {
                foreach (var c in new[] { homeCol, awayCol })
                {
                    if (c < row.Length)
                    {
                        var cell = row[c].Trim();
                        if (IsValidTeamCell(cell))
                        {
                            var (clean, _, _) = ArchiveCells.Gamertag(cell);
                            if (clean.Length > 0 && !ExcludedTokens.Contains(clean) && !double.TryParse(clean, out _) && teamSet.Add(clean))
                                teams.Add(clean);
                        }
                    }
                }
            }

            foreach (var cell in row)
            {
                var ids = MatchIds(cell);
                if (ids is null)
                    unparsed.Add($"{sheet.Name}: {cell.Trim()}");
                else
                    (inPlayoffs ? playoffs : regular).AddRange(ids);
            }
        }
        return (regular, playoffs, teams);
    }

    private static bool IsValidTeamCell(string cell)
    {
        if (string.IsNullOrWhiteSpace(cell) || cell.Length > Team.TeamNameColumnLength)
            return false;
        if (double.TryParse(cell, out _))
            return false;
        if (ExcludedTokens.Contains(cell.Trim()))
            return false;
        if (LinkPattern().IsMatch(cell)
            || GuidPattern().IsMatch(cell)
            || PlayoffsPattern().IsMatch(cell)
            || WeekPattern().IsMatch(cell)
            || ScorePattern().IsMatch(cell)
            || TimePattern().IsMatch(cell))
            return false;
        return true;
    }

    private static (int Home, int Away, int HeaderRow) FindScheduleColumns(SheetTable sheet)
    {
        for (var h = 0; h < Math.Min(sheet.Rows.Count, 5); h++)
        {
            var row = sheet.Rows[h];
            int home = -1, away = -1;
            for (var c = 0; c < row.Length; c++)
            {
                var lower = row[c].Trim().ToLowerInvariant();
                if (lower is "home" || (home < 0 && IsTeamColumnHeader(lower, "home")))
                    home = c;
                if (lower is "away" || (away < 0 && IsTeamColumnHeader(lower, "away")))
                    away = c;
            }
            if (home >= 0 && away >= 0)
                return (home, away, h);
        }
        return (-1, -1, -1);
    }

    private static bool IsTeamColumnHeader(string lower, string side) =>
        (lower == side || lower.StartsWith(side + " "))
        && !lower.Contains("win")
        && !lower.Contains("loss")
        && !lower.Contains("score")
        && !lower.Contains("pt")
        && !lower.Contains("goal")
        && !lower.Contains("link")
        && !lower.Contains("url")
        && !lower.Contains("result")
        && !lower.Contains("seed")
        && !lower.Contains("rank")
        && !lower.Contains("stat");

    /// <summary>The match IDs a cell links to (several links are several games); null for a link without one.</summary>
    public static List<string>? MatchIds(string? cell)
    {
        var text = cell ?? "";
        if (!LinkPattern().IsMatch(text))
            return [];
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

    [GeneratedRegex(@"(halowaypoint\.com|halodatahive\.com|haloquery\.com)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("playoff|final|semi|quarter|bracket", RegexOptions.IgnoreCase)]
    private static partial Regex PlayoffsPattern();

    [GeneratedRegex(@"^\s*week(end)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeekPattern();

    [GeneratedRegex(@"^\s*\d+\s*[-–]\s*\d+(\s*\([a-z0-9]+\)|\s*[a-z]+)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"^\s*(\d{1,2}:\d{2}\s*(am|pm)?|(sun|mon|tue|wed|thu|fri|sat\w*)\s+\d{1,2}(:\d{2})?\s*(am|pm)?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();
}
