using System.Text.RegularExpressions;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A season's draft (<see cref="DraftTab"/>) read into <see cref="ArchiveTeam"/>s with their captain, their captain's place in
/// the draft order and their picks by round. Two layouts:
/// <list type="bullet">
/// <item>A board (<see cref="DraftLayout.Board"/>): a row labelled "Team Name" with a team per column (blank: the team is
/// its captain's, which the archive's builder finds; a column with neither name nor captain ends it), and below it rows labelled Captain and 1st Round, Round 2, Second
/// Round, "Duo/1st Round" (each team's pick in its column), up to the next Team Name row. Other rows (grades, records,
/// a "Player 2" roster) are skipped. A row of numbers just above a round's row numbers that round's picks. A row can
/// hold more than one board side by side, each starting at its label, when the label's column has a Captain row. A sheet
/// with no Team Name row has its boards at their first Captain row ("Captain 1", a second captain's row below it), each
/// team its captain's.</item>
/// <item>Blocks (<see cref="DraftLayout.Blocks"/>): a block per team, a "Captain" cell with its captain in the next cell,
/// and below it its rounds' labels ("Round One"), each with its pick in the next cell, up to the next Captain cell; the
/// blocks in reading order (row by row, left to right), a captain's row repeated below it read once.</item>
/// <item>A table (<see cref="DraftLayout.Table"/>): a row per player under a header with Team Name and Player Name, the
/// team's place in the draft order in "Draft Order" or "Seed #" ("Draft Spot 3"), or each player's in "Draft Pick"
/// ("C3": the team's captain, 3rd in the order; "R2P3": a pick of round 2); a blank team cell is the team above it.</item>
/// </list>
/// A cell's notes in brackets ("Gingy(R1P10)", "Rayzyr (c)") are not the player's name.
/// </summary>
public static partial class DraftParser
{
    /// <summary>The tab's teams, in its order; null when it has no board or table.</summary>
    /// <param name="sheet">The draft tab.</param>
    /// <param name="tab">Its config.</param>
    /// <param name="config">The workbook's config, for the teams it names another way (<see cref="HistoricalSourceConfig.TeamNames"/>).</param>
    public static List<ArchiveTeam>? Parse(SheetTable sheet, DraftTab tab, HistoricalSourceConfig? config = null)
    {
        // A cell's team name, as the workbook's config names it: a placeholder never filled in ("Team Name") is a team's
        string Named(string cell) => config?.TeamName(Name(cell)) ?? Name(cell);
        var teams = tab.Layout switch
        {
            DraftLayout.Table => Table(sheet, Named),
            DraftLayout.Blocks => Blocks(sheet, tab.Order),
            _ => Board(sheet, tab.Order, Named),
        };
        return teams.Count == 0 ? null : teams;
    }

    private static List<ArchiveTeam> Board(SheetTable sheet, DraftOrder order, Func<string, string> named)
    {
        var teams = new List<ArchiveTeam>();
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var labels = Enumerable.Range(0, sheet.Rows[r].Length).Where(c => TeamNameLabel().IsMatch(sheet.Rows[r][c]) && HasCaptainRow(sheet, r, c)).ToList();
            for (var i = 0; i < labels.Count; i++)
            {
                var end = i + 1 < labels.Count ? labels[i + 1] : sheet.Rows.Max(row => row.Length);
                teams.AddRange(Block(sheet, r, labels[i], end, order, named));
            }
        }
        return teams.Count > 0 ? teams : CaptainBoards(sheet, order);
    }

    /// <summary>A sheet's boards without a Team Name row, each at its first Captain row (not one below another's: a
    /// team's second captain), when a round's row is below it: its teams are unnamed, each its captain's.</summary>
    private static List<ArchiveTeam> CaptainBoards(SheetTable sheet, DraftOrder order)
    {
        var teams = new List<ArchiveTeam>();
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var labels = Enumerable.Range(0, sheet.Rows[r].Length)
                .Where(c => CaptainLabel().IsMatch(sheet.Cell(r, c)) && (r == 0 || !CaptainLabel().IsMatch(sheet.Cell(r - 1, c))) && HasRoundRow(sheet, r, c))
                .ToList();
            for (var i = 0; i < labels.Count; i++)
            {
                var end = i + 1 < labels.Count ? labels[i + 1] : sheet.Rows.Max(row => row.Length);
                teams.AddRange(Block(sheet, r, labels[i], end, order, _ => "", captainRow: r));
            }
        }
        return teams;
    }

    /// <summary>A round's row below the Captain row, before the next board's first Captain row.</summary>
    private static bool HasRoundRow(SheetTable sheet, int captainRow, int column)
    {
        for (var r = captainRow + 1; r < sheet.Rows.Count; r++)
        {
            if (Round(sheet.Cell(r, column)) is not null)
                return true;
            if (CaptainLabel().IsMatch(sheet.Cell(r, column)) && !CaptainLabel().IsMatch(sheet.Cell(r - 1, column)))
                return false;
        }
        return false;
    }

    /// <summary>A sheet's draft blocks: each Captain cell with a captain beside it and a pick below it, in reading order.</summary>
    private static List<ArchiveTeam> Blocks(SheetTable sheet, DraftOrder order)
    {
        var blocks = new List<(string Captain, List<(int Round, string Gamertag)> Picks)>();
        for (var r = 0; r < sheet.Rows.Count; r++)
            for (var c = 0; c < sheet.Rows[r].Length; c++)
            {
                if (BlockCaptain(sheet, r, c) is not { } captain)
                    continue;
                var picks = BlockPicks(sheet, r, c, captain);
                if (picks.Count > 0)
                    blocks.Add((captain, picks));
            }
        return [.. blocks.Select((block, i) => new ArchiveTeam
        {
            TeamName = "",
            Captain = block.Captain,
            DraftOrder = order == DraftOrder.None ? null : i + 1,
            Picks = [.. block.Picks.Select(p => new ArchiveDraftPick(p.Gamertag, p.Round, order == DraftOrder.Linear ? (p.Round - 1) * blocks.Count + i + 1 : null))],
        })];
    }

    /// <summary>The label's column has a Captain row below it, before the next board's names: a "Team Name" cell that is
    /// a team's name (a team called that) has none.</summary>
    private static bool HasCaptainRow(SheetTable sheet, int namesRow, int column)
    {
        for (var r = namesRow + 1; r < sheet.Rows.Count && !TeamNameLabel().IsMatch(sheet.Cell(r, column)); r++)
            if (CaptainLabel().IsMatch(sheet.Cell(r, column)))
                return true;
        return false;
    }

    /// <summary>The captain of the block a Captain cell starts; null for any other cell, and for a captain's row repeated
    /// below it (the same block's).</summary>
    private static string? BlockCaptain(SheetTable sheet, int r, int c)
    {
        var captain = Player(sheet.Cell(r, c + 1));
        if (!CaptainLabel().IsMatch(sheet.Cell(r, c)) || captain.Length == 0)
            return null;
        return r > 0 && CaptainLabel().IsMatch(sheet.Cell(r - 1, c)) && Player(sheet.Cell(r - 1, c + 1)) == captain ? null : captain;
    }

    /// <summary>A block's picks: each round's label below its Captain cell with the pick beside it, up to another captain's cell.</summary>
    private static List<(int Round, string Gamertag)> BlockPicks(SheetTable sheet, int r, int c, string captain)
    {
        var picks = new List<(int Round, string Gamertag)>();
        for (var b = r + 1; b < sheet.Rows.Count; b++)
        {
            var isCaptain = CaptainLabel().IsMatch(sheet.Cell(b, c));
            if (isCaptain && Player(sheet.Cell(b, c + 1)) != captain)
                break;
            if (!isCaptain && Round(sheet.Cell(b, c)) is int round && Player(sheet.Cell(b, c + 1)) is { Length: > 0 } pick)
                picks.Add((round, pick));
        }
        return picks;
    }

    /// <summary>A board's teams: its columns after its label's, each a team with a name or a captain, up to <paramref name="end"/>
    /// or a column with neither (what is beside the board: a seed list, notes). A board with no Team Name row gives its Captain
    /// row as both the names row and the captain row.</summary>
    private static List<ArchiveTeam> Block(SheetTable sheet, int namesRow, int label, int end, DraftOrder order, Func<string, string> named, int? captainRow = null)
    {
        var rows = Enumerable.Range(namesRow + 1, sheet.Rows.Count - namesRow - 1).TakeWhile(r => !TeamNameLabel().IsMatch(sheet.Cell(r, label))).ToList();
        var captains = captainRow ?? rows.First(r => CaptainLabel().IsMatch(sheet.Cell(r, label)));
        var columns = Enumerable.Range(label + 1, Math.Max(0, end - label - 1))
            .TakeWhile(c => Name(sheet.Cell(namesRow, c)).Length > 0 || Player(sheet.Cell(captains, c)).Length > 0)
            .Where(c => named(sheet.Cell(namesRow, c)) is not { Length: > 0 } name || !HistoricalKeys.IsExcludedTeamName(name))
            .ToList();
        var rounds = Rounds(sheet, rows, label, columns);
        return columns.Select((c, i) =>
        {
            var place = i + 1;
            var picks = rounds.Select(round => (round.Round, Gamertag: Player(sheet.Cell(round.Row, c)), Number: round.Numbers?.GetValueOrDefault(c)))
                .Where(p => p.Gamertag.Length > 0)
                .Select(p => new ArchiveDraftPick(p.Gamertag, p.Round, p.Number ?? (order == DraftOrder.Linear ? (p.Round - 1) * columns.Count + place : (int?)null)))
                .ToList();
            var captain = Player(sheet.Cell(captains, c));
            return new ArchiveTeam
            {
                TeamName = named(sheet.Cell(namesRow, c)),
                Captain = captain.Length == 0 ? null : captain,
                DraftOrder = order == DraftOrder.None ? null : place,
                Picks = picks,
            };
        }).ToList();
    }

    /// <summary>The block's round rows, in order, each with its picks' numbers when the row above it gives them.</summary>
    private static List<(int Row, int Round, Dictionary<int, int>? Numbers)> Rounds(SheetTable sheet, List<int> rows, int label, List<int> columns)
    {
        var rounds = new List<(int Row, int Round, Dictionary<int, int>? Numbers)>();
        Dictionary<int, int>? numbers = null;
        foreach (var r in rows)
        {
            if (Round(sheet.Cell(r, label)) is int round)
            {
                rounds.Add((r, round, numbers));
                numbers = null;
                continue;
            }
            // A row of the picks' numbers: no label, a number in every team's column, each another
            var cells = columns.Select(c => (Column: c, Number: Number(sheet.Cell(r, c).Trim()))).ToList();
            numbers = sheet.Cell(r, label).Trim().Length == 0 && cells.All(c => c.Number is not null) && cells.DistinctBy(c => c.Number).Count() == cells.Count
                ? cells.ToDictionary(c => c.Column, c => c.Number!.Value)
                : null;
        }
        return rounds;
    }

    /// <summary>A round's label: "1st Round", "Round 2", "Second Round", "Duo/1st Round"; null for any other.</summary>
    public static int? Round(string label)
    {
        var m = RoundLabel().Match(label);
        if (!m.Success)
            return null;
        if (m.Groups["number"].Success)
            return Number(m.Groups["number"].Value);
        if (m.Groups["cardinal"].Success)
            return Array.IndexOf(Cardinals, m.Groups["cardinal"].Value.ToLowerInvariant()) + 1;
        return Array.IndexOf(Ordinals, m.Groups["word"].Value.ToLowerInvariant()) + 1;
    }

    private static readonly string[] Ordinals = ["first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth"];
    private static readonly string[] Cardinals = ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];

    private static List<ArchiveTeam> Table(SheetTable sheet, Func<string, string> named)
    {
        for (var h = 0; h < Math.Min(sheet.Rows.Count, 5); h++)
        {
            var header = sheet.Rows[h].Select(c => SpacesRegex().Replace(c.Trim().TrimEnd(':').Trim().ToLowerInvariant(), " ")).ToList();
            var team = header.FindIndex(c => c is "team name" or "team names" or "team");
            var player = header.FindIndex(c => c is "player name" or "player" or "gamertag");
            if (team < 0 || player < 0)
                continue;
            var order = header.FindIndex(c => c is "draft order" or "seed #" or "seed");
            var pick = header.FindIndex(c => c is "draft pick");
            return TableRows(sheet, h, (team, player, order, pick), named);
        }
        return [];
    }

    private static List<ArchiveTeam> TableRows(SheetTable sheet, int header, (int Team, int Player, int Order, int Pick) columns, Func<string, string> named)
    {
        var (teamColumn, playerColumn, orderColumn, pickColumn) = columns;
        var teams = new List<TableTeam>();
        var last = "";
        for (var r = header + 1; r < sheet.Rows.Count; r++)
        {
            // A blank team cell is the team above it (its name in a merged cell)
            var cell = named(sheet.Cell(r, teamColumn));
            var name = cell.Length > 0 ? cell : last;
            last = name;
            if (HistoricalKeys.IsExcludedTeamName(name))
                continue;
            var team = teams.Find(t => string.Equals(t.TeamName, name, StringComparison.OrdinalIgnoreCase));
            if (team is null)
                teams.Add(team = new TableTeam(name));
            if (orderColumn >= 0)
                team.Order ??= Place(sheet.Cell(r, orderColumn));
            if (pickColumn >= 0)
                team.Take(Player(sheet.Cell(r, playerColumn)), sheet.Cell(r, pickColumn).Trim());
        }
        return [.. teams.Where(t => t.Order is not null || t.Captain is not null || t.Picks.Count > 0)
            .Select(t => new ArchiveTeam { TeamName = t.TeamName, DraftOrder = t.Order, Captain = t.Captain, Picks = t.Picks })];
    }

    /// <summary>A team of a draft table, as its rows are read.</summary>
    private sealed class TableTeam(string name)
    {
        public string TeamName { get; } = name;
        public int? Order { get; set; }
        public string? Captain { get; private set; }
        public List<ArchiveDraftPick> Picks { get; } = [];

        /// <summary>A player's Draft Pick code: "C3" makes them the captain, 3rd in the order; "R2P3" a pick of round 2.</summary>
        public void Take(string gamertag, string code)
        {
            if (gamertag.Length == 0)
                return;
            var captain = CaptainCode().Match(code);
            var pick = PickCode().Match(code);
            if (captain.Success)
            {
                Captain ??= gamertag;
                Order ??= Number(captain.Groups[1].Value);
            }
            else if (pick.Success && Number(pick.Groups[1].Value) is int round)
                Picks.Add(new ArchiveDraftPick(gamertag, round));
        }
    }

    /// <summary>A place in the draft order: "3", "Draft Spot 3"; null otherwise.</summary>
    private static int? Place(string cell)
    {
        var spot = DraftSpot().Match(cell);
        return spot.Success ? Number(spot.Groups[1].Value) : null;
    }

    /// <summary>A count of 1 or more (a round, a place); null for 0 or a number too long to be one.</summary>
    private static int? Number(string digits) => int.TryParse(digits, out var number) && number >= 1 ? number : null;

    private static string Name(string cell) => SpacesRegex().Replace(cell.Trim(), " ");

    /// <summary>A board's player cell: the gamertag without its notes in brackets.</summary>
    private static string Player(string cell)
    {
        var text = ArchiveCells.Gamertag(cell).Gamertag;
        for (var bare = StandingsParser.WithoutNote(text); bare != text; bare = StandingsParser.WithoutNote(text))
            text = bare;
        return text;
    }

    [GeneratedRegex(@"^\s*team\s*names?\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TeamNameLabel();

    /// <summary>"Captain", "Captain:", "Captains", "Captain 1" (a team's first of two).</summary>
    [GeneratedRegex(@"^\s*captains?\s*\d?\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CaptainLabel();

    [GeneratedRegex(@"^\s*(?:duo\s*/\s*)?(?:(?<number>\d+)\s*(?:st|nd|rd|th)\s+round|round\s*(?<number>\d+)|round\s+(?<cardinal>one|two|three|four|five|six|seven|eight|nine|ten)|(?<word>first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth)\s+round)\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex RoundLabel();

    [GeneratedRegex(@"^\s*C(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CaptainCode();

    [GeneratedRegex(@"^\s*R(\d+)\s*P\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PickCode();

    [GeneratedRegex(@"^\s*(?:draft\s*spot\s*)?(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DraftSpot();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
