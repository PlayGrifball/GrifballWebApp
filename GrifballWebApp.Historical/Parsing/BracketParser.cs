using System.Globalization;
using System.Text.RegularExpressions;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A season's playoff bracket from its sheets (<see cref="BracketTab"/>): a drawn bracket read cell by cell, or a tab per
/// round of game rows. Each series' winner is the side whose name went on to the cell its config names (the next round's,
/// the champion's), else the one its score names or gives more wins, else the one of its teams that plays on in the
/// bracket (a forfeit: no games, but the winner plays the next round). A grand final followed by its sudden death was
/// won by the losers' bracket's team (else there would have been none). A score is the winner's first ("vs (2-1)"), so
/// a score whose winner nothing says is left out.
/// </summary>
public static partial class BracketParser
{
    /// <summary>What a side's cell says when no team is there yet (a template's placeholders).</summary>
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase) { "team name", "tbd", "winner", "loser", "bye", "vs", "champion", "champions" };

    private sealed class Draft
    {
        public Bracket Bracket { get; set; }
        public int Round { get; set; }
        public required string Home { get; init; }
        public required string Away { get; init; }
        public int? HomeSeed { get; init; }
        public int? AwaySeed { get; init; }
        public int? HomeWins { get; set; }
        public int? AwayWins { get; set; }
        public SeasonMatchSide? Winner { get; set; }
        public int? BestOf { get; init; }
        /// <summary>A score whose winner its cells don't say: the winner's wins and the loser's, for once it is known.</summary>
        public (int Winner, int Loser)? Unplaced { get; set; }
    }

    /// <summary>The bracket; null (a warning says why) when its tab isn't in the workbook.</summary>
    public static ArchiveBracket? Parse(string sourceId, BracketTab tab, SheetWorkbook workbook, List<string> warnings)
    {
        var drafts = tab.Rounds.Count > 0 ? FromRounds(sourceId, tab, workbook, warnings) : FromCells(sourceId, tab, workbook, warnings);
        if (drafts is null)
            return null;
        Decide(drafts);
        foreach (var draft in drafts.Where(d => d.Unplaced is not null && d.Winner is not null))
            (draft.HomeWins, draft.AwayWins) = draft.Winner is SeasonMatchSide.Home ? (draft.Unplaced!.Value.Winner, draft.Unplaced.Value.Loser) : (draft.Unplaced!.Value.Loser, draft.Unplaced.Value.Winner);
        var winners = 0;
        var losers = 0;
        return new ArchiveBracket
        {
            Series = [.. drafts.Select(d => new ArchiveBracketSeries
            {
                Bracket = d.Bracket,
                Round = d.Round,
                // The app's numbering: the losers' bracket from 1 again, the grand final on from the winners' bracket
                Number = d.Bracket is Bracket.Loser ? ++losers : ++winners,
                Home = d.Home,
                Away = d.Away,
                HomeSeed = d.HomeSeed,
                AwaySeed = d.AwaySeed,
                HomeWins = d.HomeWins,
                AwayWins = d.AwayWins,
                Winner = d.Winner,
                BestOf = d.BestOf,
            })],
        };
    }

    private static List<Draft>? FromCells(string sourceId, BracketTab tab, SheetWorkbook workbook, List<string> warnings)
    {
        if (tab.Sheet is null || workbook.Sheet(tab.Sheet) is not { } sheet)
        {
            warnings.Add($"{sourceId}: no bracket sheet '{tab.Sheet}'");
            return null;
        }
        var drafts = new List<Draft>();
        foreach (var slot in tab.Slots)
        {
            var (home, homeSeed) = Side(sheet.At(slot.Home), tab.SeedsInNames);
            var (away, awaySeed) = Side(sheet.At(slot.Away), tab.SeedsInNames);
            // A BYE, or a series not played (yet)
            if (home is null || away is null)
                continue;
            var draft = new Draft
            {
                Bracket = slot.Bracket,
                Round = slot.Round,
                Home = home,
                Away = away,
                HomeSeed = slot.HomeSeed is null ? homeSeed : Seed(sheet.At(slot.HomeSeed)),
                AwaySeed = slot.AwaySeed is null ? awaySeed : Seed(sheet.At(slot.AwaySeed)),
                BestOf = slot.BestOf,
            };
            Score(draft, sheet, slot);
            drafts.Add(draft);
        }
        return drafts;
    }

    /// <summary>The series' winner and wins from its cells.</summary>
    private static void Score(Draft draft, SheetTable sheet, BracketSlotCells slot)
    {
        if (slot.Winner is not null && Side(sheet.At(slot.Winner), false).Name is { } winner)
            draft.Winner = SideOf(draft, winner);
        if (slot.HomeScore is not null && slot.AwayScore is not null
            && ArchiveCells.Number(sheet.At(slot.HomeScore)) is decimal home && ArchiveCells.Number(sheet.At(slot.AwayScore)) is decimal away
            && home + away > 0)
        {
            (draft.HomeWins, draft.AwayWins) = ((int)home, (int)away);
            draft.Winner ??= SideWithMore(home, away);
            return;
        }
        if (slot.Score is null || ScorePattern().Match(sheet.At(slot.Score)) is not { Success: true } score)
            return;
        var (first, second) = (int.Parse(score.Groups["first"].Value, CultureInfo.InvariantCulture), int.Parse(score.Groups["second"].Value, CultureInfo.InvariantCulture));
        if (first + second == 0)
            return;
        if (score.Groups["named"].Value.Trim() is { Length: > 0 } named)
            draft.Winner ??= SideOf(draft, named);
        // The winner's first: its sides are placed once the winner is known (a score nothing says the winner of is left out)
        draft.Unplaced = (Math.Max(first, second), Math.Min(first, second));
    }

    private static List<Draft>? FromRounds(string sourceId, BracketTab tab, SheetWorkbook workbook, List<string> warnings)
    {
        var drafts = new List<Draft>();
        foreach (var round in tab.Rounds)
        {
            if (workbook.Sheet(round.Sheet) is not { } sheet)
            {
                warnings.Add($"{sourceId}: no bracket round sheet '{round.Sheet}'");
                return null;
            }
            // A tab's winners' bracket (or grand final) is played before its losers' round, but a grand final after it
            drafts.AddRange(RoundSeries(sourceId, sheet, round, warnings).OrderBy(d => d.Bracket is Bracket.Loser ? (round.Bracket is Bracket.GrandFinal ? 0 : 1) : (round.Bracket is Bracket.GrandFinal ? 1 : 0)));
        }
        return drafts;
    }

    /// <summary>A round tab's series: its sections (the winners' rows, then each "Losers Bracket: Round N"), each a run of
    /// teams' rows paired off in order.</summary>
    private static List<Draft> RoundSeries(string sourceId, SheetTable sheet, BracketRoundTab round, List<string> warnings)
    {
        var header = sheet.Rows.FindIndex(r => r.Any(c => ArchiveCells.HeaderKey(c) == "teamname"));
        if (header < 0)
            return [];
        var columns = sheet.Rows[header].Select(ArchiveCells.HeaderKey).ToList();
        var drafts = new List<Draft>();
        foreach (var (section, rows) in Sections(sheet.Rows.Skip(header + 1), round))
            drafts.AddRange(Pair(sourceId, sheet.Name, section, Runs(Blocks(rows, columns)), warnings));
        return drafts;
    }

    /// <summary>A round tab's rows by section: the tab's own bracket first, then each losers' round its header starts.</summary>
    private static List<((Bracket Bracket, int Round) Section, List<string[]> Rows)> Sections(IEnumerable<string[]> rows, BracketRoundTab round)
    {
        var sections = new List<((Bracket Bracket, int Round) Section, List<string[]> Rows)> { ((round.Bracket, round.Round), []) };
        foreach (var row in rows)
        {
            var first = row.Length > 0 ? row[0].Trim() : "";
            if (first.StartsWith("Losers Bracket", StringComparison.OrdinalIgnoreCase))
                sections.Add(((Bracket.Loser, LosersRound(first) ?? round.Round), []));
            else
                sections[^1].Rows.Add(row);
        }
        return sections;
    }

    private static int? LosersRound(string header)
        => RoundPattern().Match(header) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>A section's runs of rows of one team: its seed (its first row's) and its wins. A row without its team's name
    /// is the team above's; rows before any team are nobody's.</summary>
    private static List<(string Team, int? Seed, int Rows, decimal Wins)> Blocks(List<string[]> rows, List<string> columns)
    {
        var (seedColumn, teamColumn, winsColumn) = (columns.IndexOf("seed"), columns.IndexOf("teamname"), columns.IndexOf("wins"));
        var blocks = new List<(string Team, int? Seed, int Rows, decimal Wins)>();
        string? team = null;
        foreach (var row in rows)
        {
            team = NonEmpty(Cell(row, teamColumn)) ?? team;
            if (team is not null)
                AddRow(blocks, team, Seed(Cell(row, seedColumn)), ArchiveCells.Number(Cell(row, winsColumn)) ?? 0);
        }
        return blocks;
    }

    private static string Cell(string[] row, int column) => column >= 0 && column < row.Length ? row[column] : "";

    private static string? NonEmpty(string cell) => string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();

    /// <summary>A player's row: one more of its team's run of rows, or the start of the next run.</summary>
    private static void AddRow(List<(string Team, int? Seed, int Rows, decimal Wins)> blocks, string team, int? seed, decimal wins)
    {
        if (blocks.Count > 0 && blocks[^1].Team == team)
            blocks[^1] = (team, blocks[^1].Seed, blocks[^1].Rows + 1, blocks[^1].Wins + wins);
        else
            blocks.Add((team, seed, 1, wins));
    }

    /// <summary>The teams' runs: a run inside another team's rows (a player listed under another team in the middle of a
    /// game's, <see cref="IsStray"/>) is that team's.</summary>
    private static List<(string Team, int? Seed, int Rows, decimal Wins)> Runs(List<(string Team, int? Seed, int Rows, decimal Wins)> blocks)
    {
        var runs = new List<(string Team, int? Seed, int Rows, decimal Wins)>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (IsStray(runs, blocks, i))
            {
                runs[^1] = (runs[^1].Team, runs[^1].Seed, runs[^1].Rows + blocks[i + 1].Rows, runs[^1].Wins + blocks[i + 1].Wins);
                i++;
                continue;
            }
            runs.Add(blocks[i]);
        }
        return runs;
    }

    /// <summary>A section's games: its runs two by two, a game each. A grand final's second game of its teams is its sudden death.</summary>
    private static List<Draft> Pair(string sourceId, string sheet, (Bracket Bracket, int Round) section, List<(string Team, int? Seed, int Rows, decimal Wins)> runs, List<string> warnings)
    {
        if (runs.Count % 2 == 1)
            warnings.Add($"{sourceId}: bracket round '{sheet}' has a team without an opponent ({runs[^1].Team}); left out");
        var games = new List<Draft>();
        for (var i = 0; i + 1 < runs.Count; i += 2)
        {
            var (home, away) = (runs[i], runs[i + 1]);
            var winner = SideWithMore(home.Wins, away.Wins);
            var suddenDeath = section.Bracket is Bracket.GrandFinal && i > 0;
            games.Add(new Draft
            {
                Bracket = suddenDeath ? Bracket.GrandFinalSuddenDeath : section.Bracket,
                Round = suddenDeath ? section.Round + 1 : section.Round,
                Home = home.Team,
                Away = away.Team,
                HomeSeed = home.Seed,
                AwaySeed = away.Seed,
                // One game
                HomeWins = GameWon(winner, SeasonMatchSide.Home),
                AwayWins = GameWon(winner, SeasonMatchSide.Away),
                Winner = winner,
                BestOf = 1,
            });
        }
        return games;
    }

    /// <summary>The side with more wins; null for even wins.</summary>
    private static SeasonMatchSide? SideWithMore(decimal home, decimal away)
        => home == away ? null : home > away ? SeasonMatchSide.Home : SeasonMatchSide.Away;

    /// <summary>A side's wins of a game: 1 for its winner, 0 for its loser, none without one.</summary>
    private static int? GameWon(SeasonMatchSide? winner, SeasonMatchSide side) => winner is null ? null : winner == side ? 1 : 0;

    /// <summary>A single row between two runs of the same team's, which together are no more than a team's four: a player
    /// listed under another team in the middle of that team's game (two games of the same teams are two runs of four).</summary>
    private static bool IsStray(List<(string Team, int? Seed, int Rows, decimal Wins)> runs, List<(string Team, int? Seed, int Rows, decimal Wins)> blocks, int i)
        => runs.Count > 0 && i + 1 < blocks.Count && blocks[i].Rows == 1 && runs[^1].Team == blocks[i + 1].Team && runs[^1].Rows + blocks[i + 1].Rows <= 4;

    /// <summary>
    /// The winners the sheets leave unsaid: a series' team that plays on in the bracket (its winners' bracket and grand
    /// final for a winners' bracket series; anywhere later for a losers' one) when its opponent doesn't, and the losers'
    /// bracket's team of a grand final that went to a sudden death.
    /// </summary>
    private static void Decide(List<Draft> drafts)
    {
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            if (draft.Winner is not null)
                continue;
            if (draft.Bracket is Bracket.GrandFinal && drafts.Skip(i + 1).Any(d => d.Bracket is Bracket.GrandFinalSuddenDeath))
            {
                draft.Winner = SeasonMatchSide.Away;
                continue;
            }
            var later = drafts.Skip(i + 1).Where(d => draft.Bracket is Bracket.Loser || d.Bracket is not Bracket.Loser).ToList();
            bool PlaysOn(string team) => later.Any(d => Same(d.Home, team) || Same(d.Away, team));
            var (home, away) = (PlaysOn(draft.Home), PlaysOn(draft.Away));
            // A series without a winner has even wins, if any: whoever plays on won it
            if (home != away)
                draft.Winner = home ? SeasonMatchSide.Home : SeasonMatchSide.Away;
        }
    }

    /// <summary>A side's team and the seed its name carries ("Rated R (4)", "Beer - Seed 1"); no team for an empty cell or
    /// a template's placeholder ("Team Name", "Match 3").</summary>
    public static (string? Name, int? Seed) Side(string cell, bool seedsInNames)
    {
        var text = cell.Trim();
        if (text.Length == 0 || Placeholders.Contains(text) || MatchLabelPattern().IsMatch(text))
            return (null, null);
        if (!seedsInNames)
            return (text, null);
        if (DashSeedPattern().Match(text) is { Success: true } dashed)
            return (dashed.Groups["name"].Value.Trim(), dashed.Groups["other"].Success ? (int?)null : int.Parse(dashed.Groups["seed"].Value, CultureInfo.InvariantCulture));
        if (BracketSeedPattern().Match(text) is { Success: true } bracketed)
            return (bracketed.Groups["name"].Value.Trim(), int.Parse(bracketed.Groups["seed"].Value, CultureInfo.InvariantCulture));
        return (text, null);
    }

    /// <summary>A seed cell: "S3", "Seed 6", "1st", "4"; null for an ambiguous one ("S1/2") or none.</summary>
    public static int? Seed(string cell)
    {
        var text = cell.Trim();
        if (SeedPattern().Match(text) is { Success: true } seed)
            return int.Parse(seed.Groups[1].Value, CultureInfo.InvariantCulture) is var parsed and >= 1 ? parsed : null;
        return ArchiveCells.Number(text) is decimal number && number >= 1 && number == decimal.Truncate(number) ? (int)number : null;
    }

    /// <summary>Which side a name is: the same letters, or the same but for a note in brackets ("Like Clockwork (Sab)").</summary>
    private static SeasonMatchSide? SideOf(Draft draft, string name)
        => Same(draft.Home, name) ? SeasonMatchSide.Home : Same(draft.Away, name) ? SeasonMatchSide.Away : (SeasonMatchSide?)null;

    private static bool Same(string a, string b)
        => Key(a) == Key(b) || Key(StandingsParser.WithoutNote(a)) == Key(StandingsParser.WithoutNote(b));

    private static string Key(string name) => new([.. name.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    [GeneratedRegex(@"^(?<first>\d+)\s*-\s*(?<second>\d+)\s*(?<named>.*)$|^vs\.?\s*\(\s*(?<first>\d+)\s*-\s*(?<second>\d+)\s*\)\s*(?<named>)$", RegexOptions.IgnoreCase)]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"Round\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RoundPattern();

    [GeneratedRegex(@"^Match\s*\d+", RegexOptions.IgnoreCase)]
    private static partial Regex MatchLabelPattern();

    [GeneratedRegex(@"^(?<name>.+?)\s+-\s+seed\s*(?<seed>\d+)(?<other>/\d+)?\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex DashSeedPattern();

    [GeneratedRegex(@"^(?<name>.+?)\s*\((?<seed>\d+)\)$")]
    private static partial Regex BracketSeedPattern();

    [GeneratedRegex(@"^(?:S|Seed\s*)?(\d+)(?:st|nd|rd|th)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SeedPattern();
}
