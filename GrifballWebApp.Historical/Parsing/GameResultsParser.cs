using System.Globalization;
using System.Text.RegularExpressions;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A season's result list (<see cref="ResultsTab"/>): a game or a series per entry, the two teams by name, who won and the
/// score when it says, and the game's host, VOD, Halo game and map where it has them. Its games are recorded games without
/// players, side 1 the team the list names first (the winner, or the home side), each in the series its source puts it in
/// (<see cref="RecordedGameInput.Series"/>). The layouts (<see cref="ResultsLayout"/>):
/// <list type="bullet">
/// <item><see cref="ResultsLayout.Table"/>: "Winner | 5-2 | Loser | Host | VOD" rows under a header row;</item>
/// <item><see cref="ResultsLayout.Series"/>: "A over B 2-0 (5-1, 5-4)" cells, anywhere on the tab;</item>
/// <item><see cref="ResultsLayout.GameHistory"/>: a Halo Infinite game history's rows without a match link.</item>
/// </list>
/// The games' season, and their game when neither the list nor the tab says, are their season's (the builder sets them).
/// </summary>
public static partial class GameResultsParser
{
    public static List<RecordedGameInput> Parse(string sourceId, ResultsTab tab, SheetTable sheet, List<string> warnings)
    {
        var parse = new ResultsParse(sourceId, tab, sheet);
        switch (tab.Layout)
        {
            case ResultsLayout.Series:
                SeriesCells(parse);
                break;
            case ResultsLayout.GameHistory:
                History(parse);
                break;
            default:
                Table(parse);
                break;
        }
        if (parse.OneSided > 0)
            warnings.Add($"{sourceId}/{tab.Sheet}: {parse.OneSided} results naming one team only left out");
        if (parse.Unread.Count > 0)
            warnings.Add($"{sourceId}/{tab.Sheet}: results not read: {string.Join("; ", parse.Unread)}");
        return parse.Games;
    }

    /// <summary>A parse under way: the tab, its games so far, and the series the last game was in.</summary>
    private sealed class ResultsParse(string sourceId, ResultsTab tab, SheetTable sheet)
    {
        public string SourceId { get; } = sourceId;
        public ResultsTab Tab { get; } = tab;
        public SheetTable Sheet { get; } = sheet;
        public List<RecordedGameInput> Games { get; } = [];
        public int OneSided { get; set; }
        public List<string> Unread { get; } = [];
        public bool Playoffs { get; set; } = tab.IsPlayoffs;

        /// <summary>The last game's series and teams: a game of the same two teams right after it is of its series.</summary>
        private (string Key, string Low, string High)? _series;

        public string Key(string where) => $"{SourceId}/{Tab.Sheet}#{where}";

        /// <summary>The series of a game between these teams: the last game's when it was theirs, a new one (keyed where it starts) otherwise.</summary>
        public string SeriesOf(string a, string b, string where)
        {
            var (low, high) = string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? (a, b) : (b, a);
            if (_series is { } last && string.Equals(last.Low, low, StringComparison.OrdinalIgnoreCase) && string.Equals(last.High, high, StringComparison.OrdinalIgnoreCase))
                return last.Key;
            _series = (Key(where), low, high);
            return _series.Value.Key;
        }

        /// <summary>The next game is of a new series, whoever plays it (a week, the playoffs, a played game between).</summary>
        public void EndSeries() => _series = null;

        /// <summary>A row that is only a week or playoffs marker sets the phase of the games below it; true when it is one.</summary>
        public bool Phase(string first)
        {
            if (!PhasePattern().IsMatch(first))
                return false;
            Playoffs = Tab.IsPlayoffs || PlayoffsPattern().IsMatch(first);
            EndSeries();
            return true;
        }
    }

    /// <summary>What a game's entry says: its two teams (side 1 first), the outcome for side 1, the scores, and the rest.</summary>
    private sealed record GameResult(string First, string Second, Outcomes FirstOutcome, int? FirstScore, int? SecondScore)
    {
        public bool IsForfeit { get; init; }
        /// <summary>The second side's outcome when it isn't the first's other one (a double forfeit: both lost).</summary>
        public Outcomes? SecondOutcome { get; init; }
        public string? Game { get; init; }
        public string? Map { get; init; }
        public string? Vod { get; init; }
        public string? Host { get; init; }
    }

    private static void Add(ResultsParse parse, string key, string series, GameResult result)
    {
        var second = result.SecondOutcome ?? result.FirstOutcome switch
        {
            Outcomes.Won => Outcomes.Lost,
            Outcomes.Lost => Outcomes.Won,
            _ => result.FirstOutcome,
        };
        parse.Games.Add(new RecordedGameInput
        {
            SeasonSourceId = "",
            SourceKey = key,
            GameVersion = HistoricalKeys.GameVersionSlug(result.Game) ?? parse.Tab.Game ?? "",
            IsPlayoffs = parse.Playoffs,
            Series = series,
            IsForfeit = result.IsForfeit,
            Map = result.Map,
            Vod = result.Vod,
            Host = result.Host,
            Teams =
            [
                new RecordedTeamInput { Side = 1, Name = result.First, Outcome = result.FirstOutcome, Score = result.FirstScore },
                new RecordedTeamInput { Side = 2, Name = result.Second, Outcome = second, Score = result.SecondScore },
            ],
        });
    }

    /// <summary>The columns of a <see cref="ResultsLayout.Table"/> list, by its header row's names (-1 for none).</summary>
    private sealed record TableColumns(int Winner, int Loser, int Result, int Host, int Vod, int Game, int Map)
    {
        /// <summary>The columns a header row names; one it doesn't keep the last header's (a header repeated mid-sheet
        /// without its Game column, the games still beside it). The score is the column after the winner's when no header
        /// names it.</summary>
        public static TableColumns? Of(string[] row, TableColumns? last)
        {
            var keys = row.Select(ArchiveCells.HeaderKey).ToList();
            int Find(int previous, params string[] names) => names.Select(n => keys.IndexOf(n)).FirstOrDefault(i => i >= 0, previous);
            var (winner, loser) = (keys.IndexOf("winner"), keys.IndexOf("loser"));
            if (winner < 0 || loser < 0)
                return null;
            var score = Find(-1, "score", "result");
            return new TableColumns(winner, loser, score >= 0 ? score : winner + 1 == loser ? -1 : winner + 1,
                Find(last?.Host ?? -1, "host"), Find(last?.Vod ?? -1, "vod", "vods", "link", "video"), Find(last?.Game ?? -1, "game", "gameplayed", "halo"),
                Find(last?.Map ?? -1, "map"));
        }
    }

    private static void Table(ResultsParse parse)
    {
        TableColumns? columns = null;
        for (var r = 0; r < parse.Sheet.Rows.Count; r++)
        {
            var row = parse.Sheet.Rows[r];
            if (TableColumns.Of(row, columns) is { } header)
                columns = header;
            else if (columns is not null)
                TableRow(parse, columns, row, r.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>A row of a table: a game, a week or playoffs marker, a result naming one team (reported), or neither.</summary>
    private static void TableRow(ResultsParse parse, TableColumns columns, string[] row, string where)
    {
        var (winner, loser) = (TeamName(Cell(row, columns.Winner)), TeamName(Cell(row, columns.Loser)));
        if (winner is null || loser is null)
        {
            // A week or playoffs row names no loser (a team may be named "Final Boss")
            var first = row.Select(c => c.Trim()).FirstOrDefault(c => c.Length > 0, "");
            if (!parse.Phase(first) && (winner ?? loser) is not null)
            {
                parse.OneSided++;
                parse.EndSeries();
            }
            return;
        }
        var score = Cell(row, columns.Result);
        var (high, low) = Score(score) is { } s ? (Math.Max(s.A, s.B), Math.Min(s.A, s.B)) : ((int?)null, (int?)null);
        var doubleForfeit = DoubleForfeitPattern().IsMatch(score.Trim());
        var result = new GameResult(winner, loser, doubleForfeit ? Outcomes.Lost : high is not null && high == low ? Outcomes.Tie : Outcomes.Won, high, low)
        {
            IsForfeit = IsForfeitMark(score),
            SecondOutcome = doubleForfeit ? Outcomes.Lost : null,
            Game = Text(Cell(row, columns.Game)),
            Map = Text(Cell(row, columns.Map)),
            Vod = Link(Cell(row, columns.Vod)),
            Host = Text(Cell(row, columns.Host)),
        };
        Add(parse, parse.Key(where), parse.SeriesOf(winner, loser, where), result);
    }

    private static void SeriesCells(ResultsParse parse)
    {
        for (var r = 0; r < parse.Sheet.Rows.Count; r++)
        {
            var row = parse.Sheet.Rows[r];
            var first = row.Select(c => c.Trim()).FirstOrDefault(c => c.Length > 0, "");
            parse.Phase(first);
            for (var c = 0; c < row.Length; c++)
            {
                if (SeriesEntry.Of(row[c].Trim()) is not { } entry)
                    continue;
                // The game scores after it, in the cells beside it (until a cell that is none)
                var scores = entry.Games;
                if (scores.Count == 0)
                    scores = row.Skip(c + 1).Select(x => x.Trim()).TakeWhile(x => Score(x) is not null || IsForfeitMark(x)).ToList();
                var where = $"{r}:{c}";
                parse.EndSeries();
                if (SeriesGames(entry, scores) is { } games)
                {
                    var series = parse.Key(where);
                    foreach (var (game, index) in games.Select((g, i) => (g, i + 1)))
                        Add(parse, $"{series}/{index}", series, game);
                }
                else
                    parse.Unread.Add(row[c].Trim());
            }
        }
    }

    /// <summary>A series cell: its teams (A, then B), what it says of the series ("over", "tied", or a score between the
    /// names), the two numbers after the names, and its game scores in brackets.</summary>
    private sealed record SeriesEntry(string A, string B, bool Tied, int? ScoreA, int? ScoreB, List<string> Games)
    {
        /// <summary>Its numbers are the series score whatever they are ("A 2-1 B"), not perhaps a game's.</summary>
        public bool SeriesScore { get; init; }

        public static SeriesEntry? Of(string cell)
        {
            var over = OverPattern().Match(cell);
            if (over.Success && TeamName(over.Groups["a"].Value) is { } a && TeamName(over.Groups["b"].Value) is { } b)
            {
                var games = over.Groups["games"].Success ? over.Groups["games"].Value.Split(',').Select(g => g.Trim()).Where(g => g.Length > 0).ToList() : [];
                return new SeriesEntry(a, b, over.Groups["verb"].Value.Equals("tied", StringComparison.OrdinalIgnoreCase),
                    Number(over.Groups["sa"]), Number(over.Groups["sb"]), games);
            }
            var between = BetweenPattern().Match(cell);
            if (between.Success && TeamName(between.Groups["a"].Value) is { } first && TeamName(between.Groups["b"].Value) is { } second)
                return new SeriesEntry(first, second, false, Number(between.Groups["sa"]), Number(between.Groups["sb"]), []) { SeriesScore = true };
            return null;
        }

        private static int? Number(Group group) => group.Success ? int.Parse(group.Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// A series cell's games: one per game score (A's score first; FF a forfeit), else as many as its series score adds up
    /// to, without scores; "A over B 5-3" (a number past a series' reach, 4 or more) is one game. Each game goes to A or B
    /// by its score; when those don't add up to the series score the cell gives (a tie's "(5-3, 5-3)" is written winner
    /// first), the series score decides, A's wins first. Null for a cell that says too little.
    /// </summary>
    private static List<GameResult>? SeriesGames(SeriesEntry entry, List<string> scores)
    {
        if (scores.Count == 0)
            return ScorelessGames(entry);
        var parsed = scores.Select(Score).ToList();
        if (parsed.Where((s, i) => s is null && !IsForfeitMark(scores[i])).Any())
            return null;
        // The series score the cell gives: its numbers, or a tie's even split; none (-1) otherwise
        var (winsA, winsB) = entry.ScoreA is int a && entry.ScoreB is int b
            ? (a, b)
            : entry.Tied ? (scores.Count / 2, scores.Count - scores.Count / 2) : (-1, -1);
        var scoredA = parsed.Count(s => s is { } x && x.A > x.B);
        var scoredB = parsed.Count(s => s is { } x && x.B > x.A);
        var forfeits = parsed.Count(s => s is null);
        // The scores, A's first, add up to the series score (its forfeits the wins left): each game by its score. Without a
        // series score, A (named first: "A over B") won its forfeits
        if (winsA < 0)
            return ByScore(entry, parsed, forfeits);
        if (scoredA <= winsA && scoredB <= winsB && scoredA + scoredB + forfeits == winsA + winsB)
            return ByScore(entry, parsed, winsA - scoredA);
        return WinnerFirst(entry, parsed, winsA);
    }

    /// <summary>A series cell without game scores: "A over B 5-3" one game, "A over B 2-0" two without scores.</summary>
    private static List<GameResult>? ScorelessGames(SeriesEntry entry)
    {
        if (entry.ScoreA is not int sa || entry.ScoreB is not int sb)
            return null;
        if (!entry.SeriesScore && Math.Max(sa, sb) >= 5)
            return [new GameResult(entry.A, entry.B, Outcome(sa, sb), sa, sb)];
        return Enumerable.Range(0, sa + sb).Select(i => new GameResult(entry.A, entry.B, i < sa ? Outcomes.Won : Outcomes.Lost, null, null)).ToList();
    }

    /// <summary>Each game by its score, A's first; a forfeit (no score) A's while <paramref name="forfeitsToA"/> last.</summary>
    private static List<GameResult> ByScore(SeriesEntry entry, List<(int A, int B)?> scores, int forfeitsToA)
    {
        var games = new List<GameResult>();
        foreach (var score in scores)
        {
            if (score is { } s)
                games.Add(new GameResult(entry.A, entry.B, Outcome(s.A, s.B), s.A, s.B));
            else
                games.Add(new GameResult(entry.A, entry.B, forfeitsToA-- > 0 ? Outcomes.Won : Outcomes.Lost, null, null) { IsForfeit = true });
        }
        return games;
    }

    /// <summary>Game scores written winner first: A won the first <paramref name="winsA"/>, B the rest.</summary>
    private static List<GameResult> WinnerFirst(SeriesEntry entry, List<(int A, int B)?> scores, int winsA)
        => scores.Select((score, i) =>
        {
            var toA = i < winsA;
            var (high, low) = score is { } s ? (Math.Max(s.A, s.B), Math.Min(s.A, s.B)) : ((int?)null, (int?)null);
            return new GameResult(entry.A, entry.B, toA ? Outcomes.Won : Outcomes.Lost, toA ? high : low, toA ? low : high) { IsForfeit = score is null };
        }).ToList();

    /// <summary>The first side's outcome by the two scores.</summary>
    private static Outcomes Outcome(int first, int second) => first == second ? Outcomes.Tie : first > second ? Outcomes.Won : Outcomes.Lost;

    /// <summary>The columns of a game history, from a header row among its first five that names Home and Away.</summary>
    private static (int Home, int Away, int Score, int Vod, int HeaderRow)? HistoryColumns(SheetTable sheet)
    {
        for (var h = 0; h < Math.Min(sheet.Rows.Count, 5); h++)
        {
            var keys = sheet.Rows[h].Select(ArchiveCells.HeaderKey).ToList();
            var (home, away) = (keys.FindIndex(k => k is "home" or "hometeam"), keys.FindIndex(k => k is "away" or "awayteam"));
            if (home >= 0 && away >= 0)
                return (home, away, keys.IndexOf("score"), keys.IndexOf("vod"), h);
        }
        return null;
    }

    private static void History(ResultsParse parse)
    {
        if (HistoryColumns(parse.Sheet) is not { } columns)
        {
            parse.Unread.Add("no Home and Away header");
            return;
        }
        for (var r = columns.HeaderRow + 1; r < parse.Sheet.Rows.Count; r++)
        {
            var row = parse.Sheet.Rows[r];
            var first = Cell(row, 0).Trim();
            if (first.Length > 0)
            {
                // A week or the playoffs: a new series
                parse.Playoffs = parse.Tab.IsPlayoffs || HistoryPlayoffsPattern().IsMatch(first);
                parse.EndSeries();
            }
            // A Waypoint game is the Waypoint import's: a played game between the forfeits and unlinked games
            if (row.Any(c => WaypointMatchParser.MatchIds(c) is not { Count: 0 }))
                parse.EndSeries();
            else
                HistoryRow(parse, columns, row, r.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>A game history row without a match link: a forfeit, a game played without a link, or (no score) none.</summary>
    private static void HistoryRow(ResultsParse parse, (int Home, int Away, int Score, int Vod, int HeaderRow) columns, string[] row, string where)
    {
        var (home, away) = (WaypointMatchParser.TeamName(Cell(row, columns.Home)), WaypointMatchParser.TeamName(Cell(row, columns.Away)));
        if (home is null || away is null)
            return;
        var forfeit = row.Where((_, c) => c != columns.Home && c != columns.Away).Select(c => c.Trim()).FirstOrDefault(IsForfeitMark);
        var score = Score(Cell(row, columns.Score)) ?? (forfeit is null ? null : ScoreIn(forfeit));
        var doubleForfeit = forfeit is not null && DoubleForfeitPattern().IsMatch(forfeit);
        if ((doubleForfeit ? Outcomes.Lost : HistoryOutcome(home, away, score, forfeit)) is not { } outcome)
        {
            if (forfeit is not null)
                parse.Unread.Add($"{home} v {away}: {forfeit}, no winner");
            return;
        }
        Add(parse, parse.Key(where), parse.SeriesOf(home, away, where), new GameResult(home, away, outcome, score?.A, score?.B)
        {
            IsForfeit = forfeit is not null,
            SecondOutcome = doubleForfeit ? Outcomes.Lost : null,
            Vod = Link(Cell(row, columns.Vod)),
        });
    }

    /// <summary>A game history row's outcome for its home side: by its score, else (a forfeit) the side its mark names
    /// ("FF Ducky Win"); null when nothing says.</summary>
    private static Outcomes? HistoryOutcome(string home, string away, (int A, int B)? score, string? forfeit)
    {
        if (score is { } s)
            return Outcome(s.A, s.B);
        if (forfeit is null)
            return null;
        var named = ForfeitWinnerPattern().Match(forfeit);
        if (!named.Success)
            return null;
        var winner = named.Groups["team"].Value.Trim();
        if (home.StartsWith(winner, StringComparison.OrdinalIgnoreCase))
            return Outcomes.Won;
        return away.StartsWith(winner, StringComparison.OrdinalIgnoreCase) ? Outcomes.Lost : null;
    }

    private static string Cell(string[] row, int column) => column >= 0 && column < row.Length ? row[column] : "";

    private static string? Text(string cell) => string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();

    /// <summary>A link (http...), the first of a cell that has several; null for text that is none ("N/a").</summary>
    private static string? Link(string cell)
        => cell.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(p => p.StartsWith("http", StringComparison.OrdinalIgnoreCase));

    /// <summary>A team as a list names it, cleaned like the import cleans a team; null for none.</summary>
    private static string? TeamName(string cell)
    {
        var name = HistoricalKeys.CleanGamertag(cell);
        return name.Length == 0 || name.Equals("TBD", StringComparison.OrdinalIgnoreCase) ? null : name;
    }

    /// <summary>"5-2" (or "5 - 2"): the two numbers, the first side's first; null for anything else.</summary>
    private static (int A, int B)? Score(string cell)
    {
        var match = ScorePattern().Match(cell.Trim());
        return match.Success ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)) : null;
    }

    /// <summary>A score in a forfeit's mark ("FF (5-0)").</summary>
    private static (int A, int B)? ScoreIn(string mark)
    {
        var match = ScoreInPattern().Match(mark);
        return match.Success ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)) : null;
    }

    private static bool IsForfeitMark(string cell) => ForfeitPattern().IsMatch(cell.Trim());

    /// <summary>A cell that is only a week or playoffs marker ("Week 7 (make-up)", "W5", "Fortnight 2", "Playoffs", "Semi Finals"),
    /// not a team with such a word in its name ("Final Boss").</summary>
    [GeneratedRegex(@"^((week|weel|weekend|fortnight|w)\s*\d+\b.*|((winners|losers|upper|lower)\s+)?(playoffs?|grand\s+finals?|finals?|semi-?\s*finals?|semis|quarter-?\s*finals?|quarters|championship|bracket|round|rd)(\s+((round|rd|game|week)\s*)?\d+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex PhasePattern();

    [GeneratedRegex("playoff|final|semi|quarter|championship|winners|losers|upper|lower|bracket", RegexOptions.IgnoreCase)]
    private static partial Regex PlayoffsPattern();

    [GeneratedRegex(@"^(double\s+forfeit|double\s+ff|dbl\s+ff)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DoubleForfeitPattern();

    [GeneratedRegex("playoff|final|semi|quarter|bracket", RegexOptions.IgnoreCase)]
    private static partial Regex HistoryPlayoffsPattern();

    [GeneratedRegex(@"^(\d{1,2})\s*-\s*(\d{1,2})$")]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"\b(\d{1,2})\s*-\s*(\d{1,2})\b")]
    private static partial Regex ScoreInPattern();

    [GeneratedRegex(@"^(ff|forfeit|double forfeit|dbl ff)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForfeitPattern();

    [GeneratedRegex(@"^ff\s+(?<team>.+?)\s+win", RegexOptions.IgnoreCase)]
    private static partial Regex ForfeitWinnerPattern();

    /// <summary>"A over B 2-0 (5-1, 5-4)", "A tied B (5-3, 5-3)", "A over B 5-3".</summary>
    [GeneratedRegex(@"^(?<a>.+?)\s+(?<verb>over|tied|beat|def\.?|defeated)\s+(?<b>.+?)(?:\s+(?<sa>\d{1,2})\s*-\s*(?<sb>\d{1,2}))?\s*(?:\((?<games>[^)]*)\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex OverPattern();

    /// <summary>"A 2-1 B": a series score (up to 4) between the names.</summary>
    [GeneratedRegex(@"^(?<a>.+?)\s+(?<sa>[0-4])\s*-\s*(?<sb>[0-4])\s+(?<b>.+)$")]
    private static partial Regex BetweenPattern();
}
