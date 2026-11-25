using System.Text.RegularExpressions;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Sources;

namespace GrifballWebApp.Historical.Parsing;

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
    /// <param name="sheet">The roster tab.</param>
    /// <param name="config">The workbook's config, for the teams it names another way (<see cref="HistoricalSourceConfig.TeamNames"/>): a
    /// placeholder never filled in ("Team Name") would otherwise be no team.</param>
    /// <param name="playerNames">Players the tab names otherwise than the stats (nicknames or duo-captain shorthands).</param>
    public static Dictionary<string, string> Parse(SheetTable sheet, HistoricalSourceConfig? config = null, IReadOnlyDictionary<string, string>? playerNames = null)
    {
        var teams = new Teams(config, playerNames);
        if (!Table(sheet, teams))
        {
            DraftSheet(sheet, teams);
            if (teams.ByPlayer.Count == 0)
                CaptainRows(sheet, teams);
            if (teams.ByPlayer.Count == 0)
                TeamStatBlocks(sheet, teams);
        }
        return teams.ByPlayer;
    }

    private static bool Table(SheetTable sheet, Teams teams)
    {
        for (var h = 0; h < Math.Min(sheet.Rows.Count, 5); h++)
        {
            var header = sheet.Rows[h].Select(c => c.Trim().ToLowerInvariant()).ToList();
            var team = header.FindIndex(c => c is "team name" or "team names" or "team");
            var player = header.FindIndex(c => c is "player name" or "player" or "gamertag");
            if (team < 0 || player < 0)
                continue;
            for (var r = h + 1; r < sheet.Rows.Count; r++)
                teams.Add(sheet.Cell(r, player), sheet.Cell(r, team));
            return true;
        }
        return false;
    }

    private static void DraftSheet(SheetTable sheet, Teams teams)
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
                blocks = Blocks(row, labels, width);
                continue;
            }
            if (blocks is null)
                continue;
            foreach (var (label, columns) in blocks)
                AddBlockRow(teams, row, label, columns);
        }
    }

    /// <summary>A row of team-name labels: each label's block, its columns up to the next label's (or the widest row's end), each with the team the row names there.</summary>
    private static Dictionary<int, Dictionary<int, string?>> Blocks(string[] row, List<int> labels, int width)
    {
        var blocks = new Dictionary<int, Dictionary<int, string?>>();
        for (var i = 0; i < labels.Count; i++)
        {
            var end = i + 1 < labels.Count ? labels[i + 1] : width;
            blocks[labels[i]] = Enumerable.Range(labels[i] + 1, end - labels[i] - 1)
                .ToDictionary(c => c, c => c >= row.Length || string.IsNullOrWhiteSpace(row[c]) ? null : Spaces().Replace(row[c].Trim(), " "));
        }
        return blocks;
    }

    /// <summary>A block's row of players (its label a player's, or the captains', which name a team still unnamed).</summary>
    private static void AddBlockRow(Teams teams, string[] row, int label, Dictionary<int, string?> columns)
    {
        if (label >= row.Length || !PlayerLabel().IsMatch(row[label]))
            return;
        var captains = CaptainLabel().IsMatch(row[label]);
        foreach (var column in columns.Keys.Where(c => c < row.Length).ToList())
        {
            var parts = captains && row[column].Contains('/')
                ? row[column].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [row[column]];
            foreach (var part in parts)
            {
                if (columns[column] is null && captains && ArchiveCells.Gamertag(part).Gamertag is { Length: > 0 } captain)
                    columns[column] = teams.NamedPlayer(captain);
                teams.Add(part, columns[column]);
            }
        }
    }

    /// <summary>A roster tab whose row below each team has "Captain" under the captain column and teammates to its right
    /// (up to any Wins/Losses columns at the end).</summary>
    private static void CaptainRows(SheetTable sheet, Teams teams)
    {
        foreach (var team in StandingsParser.Parse(sheet, new StandingsTab { Sheet = sheet.Name, Layout = StandingsLayout.CaptainRows }) ?? [])
        {
            if (!string.IsNullOrWhiteSpace(team.Captain))
                teams.Add(team.Captain, team.TeamName);
            foreach (var player in team.Roster)
                teams.Add(player, team.TeamName);
        }
    }

    /// <summary>A team stat sheet (tab named for the team): each player's gamertag sits in column 0 directly above their "Goals:" stat row.</summary>
    private static void TeamStatBlocks(SheetTable sheet, Teams teams)
    {
        var team = sheet.Name.Trim();
        if (team.Length == 0 || HistoricalKeys.IsExcludedTeamName(team))
            return;
        for (var r = 1; r < sheet.Rows.Count; r++)
        {
            if (sheet.Cell(r, 0).Trim().Equals("Goals:", StringComparison.OrdinalIgnoreCase))
            {
                var gamertag = sheet.Cell(r - 1, 0).Trim();
                if (!gamertag.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
                    teams.Add(gamertag, team);
            }
        }
    }

    /// <summary>Each player's team (by normalized gamertag), the first a roster gives them.</summary>
    private sealed class Teams(HistoricalSourceConfig? config, IReadOnlyDictionary<string, string>? playerNames)
    {
        public Dictionary<string, string> ByPlayer { get; } = new(StringComparer.Ordinal);

        public string NamedPlayer(string gamertag)
            => playerNames?.FirstOrDefault(n => string.Equals(n.Key.Trim(), gamertag.Trim(), StringComparison.OrdinalIgnoreCase)).Value ?? gamertag;

        public void Add(string? gamertag, string? team)
        {
            var (clean, _, _) = ArchiveCells.Gamertag(gamertag);
            if (clean.Length == 0 || string.IsNullOrWhiteSpace(team))
                return;
            clean = NamedPlayer(clean);
            team = config?.TeamName(team.Trim()) ?? team.Trim();
            if (!HistoricalKeys.IsExcludedTeamName(team))
                ByPlayer.TryAdd(HistoricalKeys.Normalize(clean), team);
        }
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
