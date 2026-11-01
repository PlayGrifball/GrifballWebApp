using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>
/// A Halo Infinite season's game history tab: the Waypoint (or Halo Data Hive, HaloQuery) match links in its cells. Its
/// first column splits it into weeks; from a row whose first cell says Playoffs (Finals, Semis, Quarters, Bracket) on,
/// games are playoff games, until a row names a week again.
/// </summary>
public static partial class WaypointMatchParser
{
    public static (List<string> Regular, List<string> Playoffs) Parse(SheetTable sheet, List<string> unparsed)
    {
        var regular = new List<string>();
        var playoffs = new List<string>();
        var inPlayoffs = false;
        foreach (var row in sheet.Rows)
        {
            var first = row.Length > 0 ? row[0].Trim() : "";
            if (first.Length > 0)
                inPlayoffs = PlayoffsPattern().IsMatch(first);
            foreach (var cell in row)
            {
                var ids = MatchIds(cell);
                if (ids is null)
                    unparsed.Add($"{sheet.Name}: {cell.Trim()}");
                else
                    (inPlayoffs ? playoffs : regular).AddRange(ids);
            }
        }
        return (regular, playoffs);
    }

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
}
