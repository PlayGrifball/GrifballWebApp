using System.Text.RegularExpressions;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A season's awards (<see cref="AwardsTab"/>), read into <see cref="ArchiveAward"/>s in the sheet's order, a row per
/// winner: named season awards under their labels, MVPs in a record book's season blocks, lists of award winners or
/// ranked players down columns, and a records table. A winner the tab names by a nickname is put under its gamertag
/// (<see cref="AwardsTab.Names"/>).
/// </summary>
public static partial class AwardsParser
{
    /// <summary>How far right of its label a <see cref="AwardsLayout.Labels"/> award's winner can be (a label and its winner
    /// are merged cells, the winner's text a column or two in; further right is the next label's, WL23's Best Hybrid beside Tool Man).</summary>
    private const int LabelSpan = 3;

    /// <summary>An award won: the season it is of when the tab names one per block (<see cref="AwardsLayout.Seasons"/>, the
    /// season's source ID), null for the tab's season.</summary>
    public sealed record ParsedAward(string? Season, ArchiveAward Award);

    /// <summary>The tab's awards, in its order; empty when it has none the layout finds.</summary>
    public static List<ParsedAward> Parse(SheetTable sheet, AwardsTab tab)
    {
        var awards = tab.Layout switch
        {
            AwardsLayout.Labels => ByLabel(sheet, tab),
            AwardsLayout.Seasons => BySeason(sheet, tab),
            AwardsLayout.Records => ByRecord(sheet),
            _ => ByColumn(sheet, tab),
        };
        // A nickname is the gamertag it stands for; an empty one is no player at all
        var named = new List<ParsedAward>();
        foreach (var parsed in awards)
        {
            if (parsed.Award.IsTeam)
                named.Add(parsed);
            else if (Gamertag(parsed.Award.Winner, tab) is { } gamertag)
                named.Add(parsed with { Award = parsed.Award with { Winner = gamertag } });
        }
        return named;
    }

    /// <summary>The gamertag of a winner as the sheet names them: a nickname's (<see cref="AwardsTab.Names"/>), null for one
    /// that is no player.</summary>
    private static string? Gamertag(string winner, AwardsTab tab)
    {
        var name = tab.Names.FirstOrDefault(n => string.Equals(n.Key.Trim(), winner, StringComparison.OrdinalIgnoreCase));
        if (name.Key is null)
            return winner;
        return string.IsNullOrWhiteSpace(name.Value) ? null : name.Value.Trim();
    }

    private static List<ParsedAward> ByColumn(SheetTable sheet, AwardsTab tab)
    {
        var awards = new List<ParsedAward>();
        var anchor = tab.Anchor is null ? 0 : Find(sheet, tab.Anchor)?.Row;
        if (anchor is not int headerRow || headerRow >= sheet.Rows.Count)
            return awards;
        for (var column = 0; column < sheet.Rows[headerRow].Length; column++)
        {
            var label = Text(sheet.Cell(headerRow, column));
            if (label.Length == 0)
                continue;
            var name = Renamed(label, tab);
            var place = 0;
            for (var row = headerRow + 1; row < sheet.Rows.Count; row++)
            {
                // A ranked list's "2. EllisGotMyPills"
                var winner = RankPrefix().Replace(Text(sheet.Cell(row, column)), "");
                if (winner.Length == 0)
                    break;
                place++;
                awards.Add(new ParsedAward(null, new ArchiveAward { Name = name, Winner = winner, Value = tab.Ranked ? $"#{place}" : null }));
            }
        }
        return awards;
    }

    private static List<ParsedAward> ByLabel(SheetTable sheet, AwardsTab tab)
    {
        var awards = new List<ParsedAward>();
        var labels = tab.Awards.Keys.Select(Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var label in tab.Awards.Keys)
        {
            if (Find(sheet, label) is not var (labelRow, labelColumn))
                continue;
            var name = Renamed(Text(label), tab);
            // The first text below the label, in its columns: another award's label there means this one has no winner
            var winners = Below(sheet, labelRow, labelColumn);
            if (winners is null || labels.Contains(winners))
                continue;
            foreach (var winner in Winners(winners))
                awards.Add(new ParsedAward(null, new ArchiveAward { Name = name, Winner = winner }));
        }
        return awards;
    }

    /// <summary>The name an award is recorded under: the one <see cref="AwardsTab.Awards"/> gives its label, or the label.</summary>
    private static string Renamed(string label, AwardsTab tab)
    {
        var award = tab.Awards.FirstOrDefault(a => string.Equals(Text(a.Key), label, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(award.Value) ? label : award.Value.Trim();
    }

    private static string? Below(SheetTable sheet, int labelRow, int labelColumn)
    {
        for (var row = labelRow + 1; row < sheet.Rows.Count; row++)
        {
            for (var column = labelColumn; column < labelColumn + LabelSpan; column++)
            {
                var text = Text(sheet.Cell(row, column));
                if (text.Length > 0)
                    return text;
            }
        }
        return null;
    }

    /// <summary>"Mavbrid And MysticalNovice, Jussshhhyyy And Scullen cx": each of the players a cell names.</summary>
    private static IEnumerable<string> Winners(string text)
        => WinnerSeparator().Split(text).Select(Text).Where(w => w.Length > 0);

    private static List<ParsedAward> BySeason(SheetTable sheet, AwardsTab tab)
    {
        var awards = new List<ParsedAward>();
        var seasons = tab.Seasons.OrderByDescending(s => s.Key.Length).ToList();
        string? SeasonOf(int row)
        {
            var title = Text(sheet.Cell(row, 0));
            return seasons.FirstOrDefault(s => title.Contains(s.Key.Trim(), StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        }
        for (var row = 0; row < sheet.Rows.Count; row++)
        {
            if (SeasonOf(row) is not { } season)
                continue;
            // The block's "MVP: Luxe" and "FMVP: Fancy", in its title's row and the next (unless that is the next block's title)
            var cells = row + 1 < sheet.Rows.Count && SeasonOf(row + 1) is null ? sheet.Rows[row].Concat(sheet.Rows[row + 1]) : sheet.Rows[row];
            awards.AddRange(cells.Select(c => Labelled(Text(c), tab)).OfType<ArchiveAward>().Select(a => new ParsedAward(season, a)));
        }
        return awards;
    }

    /// <summary>A "MVP: Luxe" cell's award, when its label is one of the tab's and it names a winner; null otherwise.</summary>
    private static ArchiveAward? Labelled(string cell, AwardsTab tab)
    {
        var colon = cell.IndexOf(':');
        if (colon <= 0)
            return null;
        var label = cell[..colon].Trim();
        var winner = Text(cell[(colon + 1)..]);
        if (winner.Length == 0 || !tab.Awards.Keys.Any(a => string.Equals(Text(a), label, StringComparison.OrdinalIgnoreCase)))
            return null;
        return new ArchiveAward { Name = Renamed(label, tab), Winner = winner };
    }

    private static List<ParsedAward> ByRecord(SheetTable sheet)
    {
        var awards = new List<ParsedAward>();
        if (RecordsHeader(sheet) is not var (headerRow, columns))
            return awards;
        for (var row = headerRow + 1; row < sheet.Rows.Count; row++)
        {
            if (Record(sheet, row, columns) is { } award)
                awards.Add(new ParsedAward(null, award));
        }
        return awards;
    }

    /// <summary>The records table's header row and its columns by name; null when no row has a description and a holder.</summary>
    private static (int Row, Dictionary<string, int> Columns)? RecordsHeader(SheetTable sheet)
    {
        for (var row = 0; row < sheet.Rows.Count; row++)
        {
            var columns = sheet.Rows[row].Select((h, i) => (Header: Text(h).ToLowerInvariant(), Index: i))
                .Where(h => h.Header.Length > 0).GroupBy(h => h.Header).ToDictionary(g => g.Key, g => g.First().Index);
            if (columns.ContainsKey("description") && columns.ContainsKey("holder"))
                return (row, columns);
        }
        return null;
    }

    /// <summary>A records table's row: a player's record with their team, or a team's (its holder is the team, the next
    /// column its captain); null for a row without a description or a holder.</summary>
    private static ArchiveAward? Record(SheetTable sheet, int row, Dictionary<string, int> columns)
    {
        string Value(string header) => columns.TryGetValue(header, out var c) ? Text(sheet.Cell(row, c)) : "";
        var name = Value("description");
        var winner = Value("holder");
        if (name.Length == 0 || winner.Length == 0)
            return null;
        var isTeam = Value("category").StartsWith("team", StringComparison.OrdinalIgnoreCase);
        var team = Value("team_or_captain");
        var value = Value("record_value");
        return new ArchiveAward
        {
            Name = isTeam && !name.Contains("team", StringComparison.OrdinalIgnoreCase) ? $"Team {name}" : name,
            Winner = winner,
            IsTeam = isTeam,
            Team = isTeam || team.Length == 0 ? null : team,
            Value = value.Length > 0 ? value : null,
        };
    }

    /// <summary>The first cell (in reading order) whose text is <paramref name="text"/>, any case.</summary>
    private static (int Row, int Column)? Find(SheetTable sheet, string text)
    {
        var wanted = Text(text);
        for (var row = 0; row < sheet.Rows.Count; row++)
            for (var column = 0; column < sheet.Rows[row].Length; column++)
                if (string.Equals(Text(sheet.Rows[row][column]), wanted, StringComparison.OrdinalIgnoreCase))
                    return (row, column);
        return null;
    }

    /// <summary>A cell's text, its whitespace runs one space.</summary>
    private static string Text(string cell) => Whitespace().Replace(cell, " ").Trim();

    [GeneratedRegex(@"^\d+\.\s*")]
    private static partial Regex RankPrefix();

    [GeneratedRegex(@"\s+and\s+|,", RegexOptions.IgnoreCase)]
    private static partial Regex WinnerSeparator();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
