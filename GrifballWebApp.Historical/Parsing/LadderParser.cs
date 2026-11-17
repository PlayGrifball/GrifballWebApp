using System.Text.RegularExpressions;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A Late League season's ladder (<see cref="LadderTab"/>): a row per player under a row of headers, read into
/// <see cref="LadderStanding"/>s with the player's place (their order among the tab's players), division, league, rank, LP,
/// streak, MMR and placement record (P/ Win, P/ Loss). A league written "(2) Bronze" is division 2, league Bronze. Rows with
/// no gamertag (a ladder's empty slots) and the bot's unnamed sign-ups ("LateLeague#4522") are no players: left out, and
/// given no place. And the workbook's past finishes (<see cref="ParseFinishes"/>).
/// </summary>
public static partial class LadderParser
{
    private enum Field { Division, League, Rank, LeaguePoints, Streak, Mmr, Wins, Losses }

    /// <summary>The headers, compared as <see cref="ArchiveCells.HeaderKey"/>: "Skill" and "LS" are not a rank (always 0).</summary>
    private static readonly Dictionary<string, Field> Headers = new(StringComparer.Ordinal)
    {
        ["division"] = Field.Division,
        ["league"] = Field.League,
        ["rank"] = Field.Rank,
        ["lp"] = Field.LeaguePoints,
        ["streak"] = Field.Streak, ["w/streak"] = Field.Streak,
        ["mmr"] = Field.Mmr,
        ["p/win"] = Field.Wins,
        ["p/loss"] = Field.Losses,
    };

    /// <summary>The tab's players in its order; null when it has no header row (no cell reads League).</summary>
    public static List<LadderStanding>? Parse(SheetTable sheet, LadderTab tab)
    {
        var headerRow = sheet.Rows.FindIndex(r => r.Any(c => ArchiveCells.HeaderKey(c) == "league"));
        if (headerRow < 0)
            return null;
        var columns = new Dictionary<Field, int>();
        var header = sheet.Rows[headerRow];
        for (var c = 0; c < header.Length; c++)
        {
            if (c != tab.PlayerColumn && Headers.TryGetValue(ArchiveCells.HeaderKey(header[c]), out var field))
                columns.TryAdd(field, c);
        }
        if (tab.DivisionColumn is int division)
            columns[Field.Division] = division;

        var players = new List<LadderStanding>();
        for (var r = headerRow + 1; r < sheet.Rows.Count; r++)
        {
            var gamertag = sheet.Cell(r, tab.PlayerColumn).Trim();
            if (!IsPlayer(gamertag))
                continue;
            var row = r;
            string Text(Field f) => columns.TryGetValue(f, out var c) ? sheet.Cell(row, c).Trim() : "";
            var (tier, league) = League(Text(Field.League));
            players.Add(new LadderStanding
            {
                Gamertag = gamertag,
                Position = players.Count + 1,
                Division = Value(Text(Field.Division)) ?? tier,
                League = league,
                Rank = Whole(Text(Field.Rank)),
                LeaguePoints = Whole(Text(Field.LeaguePoints)),
                Streak = Whole(Text(Field.Streak)),
                Mmr = Whole(Text(Field.Mmr)),
                Wins = Count(Text(Field.Wins)),
                Losses = Count(Text(Field.Losses)),
            });
        }
        return players;
    }

    /// <summary>
    /// The finishes tab (Late League's "Past Seasons"): a season heading over each group of League, Rank and Pos columns, a
    /// row per player below with the gamertag in the first column. Each heading's players who finished that season (a
    /// league, rank or place), by heading (trimmed, any case), their place the sheet's Pos. A heading with no Pos column
    /// under it is left out; null when the tab has no Pos column at all.
    /// </summary>
    public static Dictionary<string, List<LadderStanding>>? ParseFinishes(SheetTable sheet)
    {
        var subRow = sheet.Rows.FindIndex(r => r.Any(c => ArchiveCells.HeaderKey(c) == "pos"));
        if (subRow < 1)
            return null;
        var groups = FinishGroups(sheet.Rows[subRow - 1], sheet.Rows[subRow]);
        var finishes = groups.Keys.ToDictionary(k => k, _ => new List<LadderStanding>(), StringComparer.OrdinalIgnoreCase);
        for (var r = subRow + 1; r < sheet.Rows.Count; r++)
        {
            var gamertag = sheet.Cell(r, 0).Trim();
            if (!IsPlayer(gamertag))
                continue;
            foreach (var (heading, columns) in groups)
                if (Finish(sheet, r, gamertag, columns) is { } finish)
                    finishes[heading].Add(finish);
        }
        return finishes;
    }

    /// <summary>Each season heading's League, Rank and Pos columns (-1: none), among the labels from its column up to the
    /// next heading. A row is cut at its last filled cell, so a heading's columns run to the labels' end.</summary>
    private static Dictionary<string, (int League, int Rank, int Pos)> FinishGroups(string[] headings, string[] labels)
    {
        var starts = Enumerable.Range(1, Math.Max(headings.Length - 1, 0)).Where(c => headings[c].Trim().Length > 0).ToList();
        var groups = new Dictionary<string, (int League, int Rank, int Pos)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < starts.Count; i++)
        {
            var from = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1] : labels.Length;
            int Column(string key) => Enumerable.Range(from, Math.Max(Math.Min(end, labels.Length) - from, 0))
                .Where(k => ArchiveCells.HeaderKey(labels[k]) == key).DefaultIfEmpty(-1).First();
            // A heading without a place is no finish: its ladder keeps its own order
            if (Column("pos") is var pos and >= 0)
                groups.TryAdd(headings[from].Trim(), (Column("league"), Column("rank"), pos));
        }
        return groups;
    }

    /// <summary>A row's finish under one heading; null when it gives none (no league, rank or place). A place below 1 is none.</summary>
    private static LadderStanding? Finish(SheetTable sheet, int row, string gamertag, (int League, int Rank, int Pos) columns)
    {
        string Cell(int column) => column < 0 ? "" : sheet.Cell(row, column);
        var league = Value(Cell(columns.League));
        var rank = Whole(Cell(columns.Rank));
        var place = Whole(Cell(columns.Pos));
        if (league is null && rank is null && place is null)
            return null;
        return new LadderStanding { Gamertag = gamertag, Position = place is >= 1 ? place : null, League = league, Rank = rank };
    }

    /// <summary>A gamertag: not empty, not the bot's unnamed sign-up ("LateLeague#4522"), not a header repeated
    /// ("Gamertag", "Players", "`") and not a totals row ("Total", "Totals").</summary>
    public static bool IsPlayer(string gamertag)
        => gamertag.Length > 0 && !BotSignupRegex().IsMatch(gamertag) && !NotAPlayer.Contains(ArchiveCells.HeaderKey(gamertag).TrimEnd(':'));

    private static readonly HashSet<string> NotAPlayer = new(StringComparer.Ordinal) { "gamertag", "gamertags", "player", "players", "`", "total", "totals" };

    /// <summary>"(2) Bronze" is division 2 of league Bronze; anything else is the league as written.</summary>
    private static (string? Division, string? League) League(string text)
    {
        var m = NumberedLeagueRegex().Match(text);
        return m.Success ? (m.Groups[1].Value, Value(m.Groups[2].Value)) : (null, Value(text));
    }

    /// <summary>A cell's text, or null for an empty cell and errors (#N/A, N/A).</summary>
    private static string? Value(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    /// <summary>A whole number (rounded), or null for anything else.</summary>
    private static int? Whole(string text)
        => ArchiveCells.Number(text) is decimal number && Math.Abs(number) <= int.MaxValue
            ? (int)Math.Round(number, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>A count: a whole number, never negative.</summary>
    private static int? Count(string text) => Whole(text) is int n && n >= 0 ? n : null;

    [GeneratedRegex(@"^LateLeague#\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex BotSignupRegex();

    [GeneratedRegex(@"^\((\d+)\)\s*(.+)$")]
    private static partial Regex NumberedLeagueRegex();
}
