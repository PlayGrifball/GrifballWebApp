using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Historical.Sources;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>
/// Per-game stats tabs (MCC carnage reports copied into the sheets) as recorded games. Two layouts:
/// <list type="bullet">
/// <item><see cref="GamesLayout.Carnage"/>: a row per player per game (Player, Team, GamesPlayed, Wins, Loses, Goals,
/// Kills, Assists, Deaths, ..., TimeStamp). A game is the rows sharing a timestamp; without one (a column of zeros, or none),
/// a run of rows that ends when a player or a side comes back or the game time jumps. Rows naming a week or the playoffs
/// mark the phase of the games below.</item>
/// <item><see cref="GamesLayout.Blocks"/> (AGLA 2019): a title row per game ("Week 1 - 1/28/19 - A over B (5-2)"), the
/// players below it, each side its team's rows (subs play for the side above them).</item>
/// </list>
/// A game with only one side is left out (reported): it is a partial copy.
/// </summary>
public static partial class RecordedGameParser
{
    /// <summary>Game times within this many seconds are one game's (players who joined late have shorter ones).</summary>
    private const int GameTimeTolerance = 60;

    public static List<RecordedGameInput> Parse(string sourceId, GamesTab tab, SheetWorkbook workbook, List<string> warnings)
    {
        var sheet = workbook.Sheet(tab.Sheet);
        if (sheet is null || sheet.Rows.Count < 2)
        {
            warnings.Add($"{sourceId}: no games sheet '{tab.Sheet}', or it is empty");
            return [];
        }
        var games = tab.Layout == GamesLayout.Blocks ? Blocks(sourceId, tab, sheet) : Carnage(sourceId, tab, sheet);
        var complete = games.Where(g => g.Teams.Count >= 2).ToList();
        if (complete.Count < games.Count)
            warnings.Add($"{sourceId}/{tab.Sheet}: {games.Count - complete.Count} games with only one side left out");
        return complete;
    }

    private sealed class Columns(string[] header)
    {
        private readonly List<string> _keys = header.Select(ArchiveCells.HeaderKey).ToList();

        public int Find(params string[] names) => names.Select(n => _keys.IndexOf(n)).FirstOrDefault(i => i >= 0, -1);
    }

    private sealed class GameRows
    {
        public required int FirstRow { get; init; }
        public DateTime? PlayedAt { get; init; }
        public string? TimestampText { get; init; }
        public int? GameTime { get; init; }
        public bool Playoffs { get; init; }
        public List<string[]> Rows { get; } = [];
        public HashSet<string> Players { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Sides { get; } = [];
        public string? LastSide { get; set; }
    }

    private static List<RecordedGameInput> Carnage(string sourceId, GamesTab tab, SheetTable sheet)
    {
        var columns = new Columns(sheet.Rows[0]);
        var player = Math.Max(columns.Find("player", "gamertag"), 0);
        var team = columns.Find("team", "teamid");
        var gamesPlayed = columns.Find("gamesplayed", "games");
        var (wins, losses, goals, kills, assists, deaths) = (columns.Find("wins"), columns.Find("loses", "losses"), columns.Find("goals"),
            columns.Find("kills"), columns.Find("assists"), columns.Find("deaths"));
        var (betrayals, hammer, sword, ball, stops) = (columns.Find("betrayals"), columns.Find("hammerkills"), columns.Find("swordkills"),
            columns.Find("ballkills", "bombkills"), columns.Find("bombstops", "stops", "carrierkills"));
        var (timestamp, gameTime, game) = (columns.Find("timestamp"), columns.Find("gametime"), columns.Find("game"));
        if (kills < 0)
            return [];

        var blocks = new List<GameRows>();
        GameRows? current = null;
        var playoffs = false;
        for (var r = 1; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var name = Cell(row, player).Trim();
            var (k, d, g, gp) = (Int(row, kills), Int(row, deaths), Int(row, goals), Int(row, gamesPlayed));
            // A week or playoffs marker (in the player column, or the next one), or a row of zeros
            if (name.Length == 0 || ((k ?? 0) == 0 && (d ?? 0) == 0 && (g ?? 0) == 0 && (gp ?? 0) == 0))
            {
                var first = row.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? "";
                if (PhasePattern().IsMatch(first))
                    playoffs = PlayoffsPattern().IsMatch(first);
                continue;
            }
            if (k is null)
                continue;
            var stampText = timestamp < 0 ? null : Cell(row, timestamp).Trim();
            var playedAt = Timestamp(stampText);
            var time = Int(row, gameTime);
            var side = team < 0 ? "" : Cell(row, team).Trim();
            var split = current is null
                || current.PlayedAt != playedAt
                || (playedAt is null && (current.Players.Contains(name)
                    || (side != current.LastSide && current.Sides.Contains(side))
                    || (time is not null && current.GameTime is not null && Math.Abs(time.Value - current.GameTime.Value) > GameTimeTolerance)));
            if (split)
            {
                current = new GameRows { FirstRow = r, PlayedAt = playedAt, TimestampText = playedAt is null ? null : stampText, GameTime = time, Playoffs = playoffs };
                blocks.Add(current);
            }
            current!.Rows.Add(row);
            current.Players.Add(name);
            current.Sides.Add(side);
            current.LastSide = side;
        }

        // A timestamp shared by two games (rare) keeps its keys apart
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var games = new List<RecordedGameInput>();
        foreach (var block in blocks)
        {
            var key = block.PlayedAt is DateTime at ? $"{sourceId}/{tab.Sheet}@{at:yyyyMMddHHmmss}" : $"{sourceId}/{tab.Sheet}#{block.FirstRow}";
            var seen = keys.GetValueOrDefault(key);
            keys[key] = seen + 1;
            if (seen > 0)
                key += $"-{seen + 1}";

            var players = block.Rows.Select(row => new RecordedPlayerInput
            {
                Gamertag = ArchiveCells.Gamertag(Cell(row, player)).Gamertag,
                Side = Side(team < 0 ? "" : Cell(row, team)),
                Kills = Int(row, kills) ?? 0,
                Deaths = Int(row, deaths) ?? 0,
                Assists = Int(row, assists) ?? 0,
                Goals = Int(row, goals),
                Betrayals = Int(row, betrayals),
                HammerKills = Int(row, hammer),
                SwordKills = Int(row, sword),
                BallKills = Int(row, ball),
                CarrierKills = Int(row, stops),
            }).Where(p => p.Gamertag.Length > 0).ToList();
            var teams = block.Rows.GroupBy(row => Side(team < 0 ? "" : Cell(row, team))).OrderBy(s => s.Key).Select(s => new RecordedTeamInput
            {
                Side = s.Key,
                Outcome = s.Sum(row => Int(row, wins) ?? 0) > 0 ? Outcomes.Won : s.Sum(row => Int(row, losses) ?? 0) > 0 ? Outcomes.Lost : Outcomes.DidNotFinish,
                Score = s.Sum(row => Int(row, goals) ?? 0),
            }).ToList();
            var gameName = game < 0 ? null : block.Rows.Select(row => Cell(row, game).Trim()).FirstOrDefault(n => n.Length > 0);
            games.Add(new RecordedGameInput
            {
                SeasonSourceId = tab.Season,
                SourceKey = key,
                GameVersion = HistoricalKeys.GameVersionSlug(gameName) ?? tab.Game,
                PlayedAt = block.PlayedAt,
                IsPlayoffs = block.Playoffs,
                IsScrim = tab.Scrims,
                Teams = teams,
                Players = players,
            });
        }
        return games;
    }

    private static List<RecordedGameInput> Blocks(string sourceId, GamesTab tab, SheetTable sheet)
    {
        var games = new List<RecordedGameInput>();
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var title = sheet.Rows[r].Length > 0 ? sheet.Rows[r][0].Trim() : "";
            var titleMatch = TitlePattern().Match(title);
            if (!titleMatch.Success)
                continue;
            var titleRow = r;
            // The header follows (after a "Postgame Page" banner row)
            var headerRow = r + 1;
            while (headerRow < sheet.Rows.Count && headerRow <= r + 3 && ArchiveCells.HeaderKey(sheet.Cell(headerRow, 1)) != "player")
                headerRow++;
            if (headerRow >= sheet.Rows.Count || ArchiveCells.HeaderKey(sheet.Cell(headerRow, 1)) != "player")
                continue;
            var columns = new Columns(sheet.Rows[headerRow]);
            var (played, wins, goals, kills, assists, deaths, stops) = (columns.Find("gamesplayed"), columns.Find("wins"), columns.Find("goals"),
                columns.Find("kills"), columns.Find("assists"), columns.Find("deaths"), columns.Find("carrierkills"));

            // Sides by team name in order of appearance; a sub plays for the side above them
            var side = 1;
            var sideOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var players = new List<RecordedPlayerInput>();
            var sideWins = new Dictionary<int, int>();
            var sideNames = new Dictionary<int, string>();
            var end = headerRow + 1;
            for (; end < sheet.Rows.Count && !TitlePattern().IsMatch(sheet.Cell(end, 0).Trim()); end++)
            {
                var row = sheet.Rows[end];
                var first = Cell(row, 0).Trim();
                if (first.StartsWith("Totals", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (first.Length > 0 && !first.Equals("Sub", StringComparison.OrdinalIgnoreCase))
                {
                    if (!sideOf.TryGetValue(first, out side))
                        sideOf[first] = side = sideOf.Count + 1;
                }
                var gamertag = ArchiveCells.Gamertag(Cell(row, 1)).Gamertag;
                if (gamertag.Length == 0 || (Int(row, played) ?? 0) == 0)
                    continue;
                players.Add(new RecordedPlayerInput
                {
                    Gamertag = gamertag,
                    Side = side,
                    Kills = Int(row, kills) ?? 0,
                    Deaths = Int(row, deaths) ?? 0,
                    Assists = Int(row, assists) ?? 0,
                    Goals = Int(row, goals),
                    CarrierKills = Int(row, stops),
                });
                sideWins[side] = sideWins.GetValueOrDefault(side) + (Int(row, wins) ?? 0);
                if (!first.Equals("Sub", StringComparison.OrdinalIgnoreCase) && first.Length > 0)
                    sideNames.TryAdd(side, first);
            }
            r = end - 1;

            var tied = titleMatch.Groups["verb"].Value.Equals("tied", StringComparison.OrdinalIgnoreCase);
            var score = ScorePattern().Match(title);
            var (high, low) = score.Success ? (int.Parse(score.Groups[1].Value), int.Parse(score.Groups[2].Value)) : ((int?)null, (int?)null);
            var teams = players.Select(p => p.Side).Distinct().Order().Select(s =>
            {
                var won = sideWins.GetValueOrDefault(s) > 0;
                return new RecordedTeamInput
                {
                    Side = s,
                    Name = sideNames.GetValueOrDefault(s),
                    Outcome = tied ? Outcomes.Tie : won ? Outcomes.Won : Outcomes.Lost,
                    Score = won || tied ? high : low,
                };
            }).ToList();
            games.Add(new RecordedGameInput
            {
                SeasonSourceId = tab.Season,
                SourceKey = $"{sourceId}/{tab.Sheet}#{titleRow}",
                GameVersion = tab.Game,
                PlayedAt = BlockDate(title),
                IsPlayoffs = PlayoffsPattern().IsMatch(titleMatch.Groups["phase"].Value),
                IsScrim = tab.Scrims,
                Teams = teams,
                Players = players,
            });
        }
        return games;
    }

    private static string Cell(string[] row, int column) => column >= 0 && column < row.Length ? row[column] : "";

    private static int? Int(string[] row, int column) => ArchiveCells.Number(Cell(row, column)) is decimal n ? (int)Math.Round(n, MidpointRounding.AwayFromZero) : null;

    /// <summary>Sides as the sheet numbers them (0 and 1) are 1 and 2.</summary>
    private static int Side(string value) => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var side) ? side + 1 : 1;

    /// <summary>A carnage report's timestamp, "Mon Jul 11 18:02:15 2022" (taken as UTC); null for none (or a 0).</summary>
    public static DateTime? Timestamp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var clean = Whitespace().Replace(text.Trim(), " ");
        string[] formats = ["ddd MMM d HH:mm:ss yyyy", "M/d/yyyy H:mm:ss", "M/d/yyyy H:mm"];
        return DateTime.TryParseExact(clean, formats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at)
            ? DateTime.SpecifyKind(at, DateTimeKind.Utc)
            : null;
    }

    /// <summary>The m/d/yy in a block's title.</summary>
    private static DateTime? BlockDate(string title)
    {
        var match = DatePattern().Match(title);
        if (!match.Success)
            return null;
        var year = int.Parse(match.Groups[3].Value);
        year = year < 100 ? 2000 + year : year;
        try
        {
            return new DateTime(year, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 0, 0, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^(week|weel|playoff|final|semi|quarter|round|championship)", RegexOptions.IgnoreCase)]
    private static partial Regex PhasePattern();

    [GeneratedRegex("playoff|final|semi|quarter|championship", RegexOptions.IgnoreCase)]
    private static partial Regex PlayoffsPattern();

    [GeneratedRegex(@"^(?<phase>[^-]*?)\s*-.*\s(?<verb>over|tied)\s", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"\(?\b(\d{1,2})-(\d{1,2})\b\)?")]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
