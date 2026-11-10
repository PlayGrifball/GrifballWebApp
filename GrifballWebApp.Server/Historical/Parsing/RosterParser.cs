using System.Text.RegularExpressions;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>
/// A tab of the teams' rosters, as the stats tool's sheets have them, read into each player's team (by normalized
/// gamertag, <see cref="HistoricalKeys.Normalize"/>). Two layouts:
/// <list type="bullet">
/// <item>A table (TeamInfo): a header with "Team Name" (or "Team", "Team Names") and "Player Name" (or "Player", "Gamertag"), a row per player.</item>
/// <item>A draft sheet (Draft, Draft Sheet, Teams): a row labelled "Team Name" with a team per column, and below it rows
/// labelled Captain, Player 2, 1st Round, ... with each team's player in its column. Other rows (grades, records) are
/// skipped (blank rows aren't read: a block runs to the next Team Name row). A row can hold more than one block side by side, each starting at its label.</item>
/// </list>
/// A team with no name (a blank cell over its column) is named after its captain, as schedules call teams. A player on
/// two teams' rosters keeps the first.
/// </summary>
public static partial class RosterParser
{
    public static Dictionary<string, string> Parse(SheetTable sheet)
    {
        var teams = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Table(sheet, teams))
            return teams;
        DraftSheet(sheet, teams);
        return teams;
    }

    private static bool Table(SheetTable sheet, Dictionary<string, string> teams)
    {
        for (var h = 0; h < Math.Min(sheet.Rows.Count, 5); h++)
        {
            var header = sheet.Rows[h].Select(c => c.Trim().ToLowerInvariant()).ToList();
            var team = header.FindIndex(c => c is "team name" or "team names" or "team");
            var player = header.FindIndex(c => c is "player name" or "player" or "gamertag");
            if (team < 0 || player < 0)
                continue;
            for (var r = h + 1; r < sheet.Rows.Count; r++)
                Add(teams, sheet.Cell(r, player), sheet.Cell(r, team));
            return true;
        }
        return false;
    }

    private static void DraftSheet(SheetTable sheet, Dictionary<string, string> teams)
    {
        // The columns of the block being read: its label's column and the team in each column after it (null till its captain names it)
        Dictionary<int, Dictionary<int, string?>>? blocks = null;
        // Rows end at their last filled cell: a block runs to the widest row's end, so a row of labels only has its columns
        var width = sheet.Rows.Count == 0 ? 0 : sheet.Rows.Max(r => r.Length);
        foreach (var row in sheet.Rows)
        {
            var labels = Enumerable.Range(0, row.Length).Where(c => TeamNameLabel().IsMatch(row[c])).ToList();
            if (labels.Count > 0)
            {
                blocks = [];
                for (var i = 0; i < labels.Count; i++)
                {
                    var end = i + 1 < labels.Count ? labels[i + 1] : width;
                    blocks[labels[i]] = Enumerable.Range(labels[i] + 1, end - labels[i] - 1)
                        .ToDictionary(c => c, c => c >= row.Length || string.IsNullOrWhiteSpace(row[c]) ? null : Spaces().Replace(row[c].Trim(), " "));
                }
                continue;
            }
            if (blocks is null)
                continue;
            foreach (var (label, columns) in blocks)
            {
                if (label >= row.Length || !PlayerLabel().IsMatch(row[label]))
                    continue;
                var captains = CaptainLabel().IsMatch(row[label]);
                foreach (var column in columns.Keys.Where(c => c < row.Length).ToList())
                {
                    if (columns[column] is null && captains && ArchiveCells.Gamertag(row[column]).Gamertag is { Length: > 0 } captain)
                        columns[column] = captain;
                    Add(teams, row[column], columns[column]);
                }
            }
        }
    }

    private static void Add(Dictionary<string, string> teams, string? gamertag, string? team)
    {
        var (clean, _, _) = ArchiveCells.Gamertag(gamertag);
        if (clean.Length == 0 || string.IsNullOrWhiteSpace(team) || HistoricalKeys.IsExcludedTeamName(team))
            return;
        teams.TryAdd(HistoricalKeys.Normalize(clean), team.Trim());
    }

    [GeneratedRegex(@"^\s*captain\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CaptainLabel();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^\s*team\s*name\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TeamNameLabel();

    [GeneratedRegex(@"^\s*(captain|player\s*\d+|\d+\s*(st|nd|rd|th)\s*round)\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLabel();
}
