using GrifballWebApp.Domain.Halo;
using System.Globalization;
using System.Text.RegularExpressions;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Parsing;

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

    /// <summary>A Carnage sheet's columns (-1 for one it hasn't): the player's, and the stats a game's rows give.</summary>
    private sealed record CarnageColumns(int Player, int Team, int GamesPlayed, int Wins, int Losses, int Goals, int Kills, int Assists, int Deaths,
        int Betrayals, int Hammer, int Sword, int Ball, int Stops, int Stamp, int GameTime, int Game)
    {
        public static CarnageColumns Of(string[] header)
        {
            var columns = new Columns(header);
            return new CarnageColumns(Math.Max(columns.Find("player", "gamertag"), 0), columns.Find("team", "teamid"), columns.Find("gamesplayed", "games"),
                columns.Find("wins"), columns.Find("loses", "losses"), columns.Find("goals"), columns.Find("kills"), columns.Find("assists"), columns.Find("deaths"),
                columns.Find("betrayals"), columns.Find("hammerkills"), columns.Find("swordkills"), columns.Find("ballkills", "bombkills"),
                columns.Find("bombstops", "stops", "carrierkills"), columns.Find("timestamp"), columns.Find("gametime"), columns.Find("game"));
        }

        public string TeamOf(string[] row) => Team < 0 ? "" : Cell(row, Team);
    }

    private static List<RecordedGameInput> Carnage(string sourceId, GamesTab tab, SheetTable sheet)
    {
        var c = CarnageColumns.Of(sheet.Rows[0]);
        if (c.Kills < 0)
            return [];

        // A timestamp shared by two games (rare) keeps its keys apart
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var games = new List<RecordedGameInput>();
        foreach (var block in CarnageBlocks(sheet, c))
        {
            var key = block.PlayedAt is DateTime at ? $"{sourceId}/{tab.Sheet}@{at:yyyyMMddHHmmss}" : $"{sourceId}/{tab.Sheet}#{block.FirstRow}";
            var seen = keys.GetValueOrDefault(key);
            keys[key] = seen + 1;
            if (seen > 0)
                key += $"-{seen + 1}";
            games.Add(CarnageGame(tab, key, block, c));
        }
        return games;
    }

    /// <summary>The sheet's rows, a game's at a time: a new game at a new timestamp, or (without one) at a player or side seen
    /// already, or a game time too far from the game's.</summary>
    private static List<GameRows> CarnageBlocks(SheetTable sheet, CarnageColumns c)
    {
        var blocks = new List<GameRows>();
        GameRows? current = null;
        var playoffs = false;
        for (var r = 1; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var name = Cell(row, c.Player).Trim();
            if (IsSummaryOrHeader(name, c.TeamOf(row)))
                continue;
            var (k, d, g, gp) = (Int(row, c.Kills), Int(row, c.Deaths), Int(row, c.Goals), Int(row, c.GamesPlayed));
            // A week or playoffs marker (in the player column, or the next one), or a row of zeros
            if (name.Length == 0 || IsAllZero(k, d, g, gp))
            {
                playoffs = PhaseOf(row) ?? playoffs;
                continue;
            }
            if (k is null)
                continue;
            var stampText = c.Stamp < 0 ? null : Cell(row, c.Stamp).Trim();
            var playedAt = Timestamp(stampText);
            var time = Int(row, c.GameTime);
            var side = c.TeamOf(row).Trim();
            if (current is null || StartsAnotherGame(current, playedAt, name, side, time))
            {
                current = new GameRows { FirstRow = r, PlayedAt = playedAt, TimestampText = playedAt is null ? null : stampText, GameTime = time, Playoffs = playoffs };
                blocks.Add(current);
            }
            current.Rows.Add(row);
            current.Players.Add(name);
            current.Sides.Add(side);
            current.LastSide = side;
        }
        return blocks;
    }

    private static bool IsSummaryOrHeader(string name, string? team)
        => SeasonStatsParser.IsTotalsLabel(name) || SeasonStatsParser.IsRepeatingHeader(name, team);

    private static bool IsAllZero(params int?[] counts) => counts.All(n => (n ?? 0) == 0);

    /// <summary>A marker row's phase: whether it starts the playoffs, or null for a row that marks none.</summary>
    private static bool? PhaseOf(string[] row)
    {
        var first = row.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? "";
        return PhasePattern().IsMatch(first) ? PlayoffsPattern().IsMatch(first) : null;
    }

    /// <summary>Whether a row is another game's than <paramref name="current"/>'s.</summary>
    private static bool StartsAnotherGame(GameRows current, DateTime? playedAt, string name, string side, int? time)
        => current.PlayedAt != playedAt
            || (playedAt is null && (current.Players.Contains(name)
                || (side != current.LastSide && current.Sides.Contains(side))
                || (time is not null && current.GameTime is not null && Math.Abs(time.Value - current.GameTime.Value) > GameTimeTolerance)));

    private static RecordedGameInput CarnageGame(GamesTab tab, string key, GameRows block, CarnageColumns c)
    {
        var players = block.Rows.Select(row => new RecordedPlayerInput
        {
            Gamertag = ArchiveCells.Gamertag(Cell(row, c.Player)).Gamertag,
            Side = Side(c.TeamOf(row)),
            Kills = Int(row, c.Kills) ?? 0,
            Deaths = Int(row, c.Deaths) ?? 0,
            Assists = Int(row, c.Assists) ?? 0,
            Goals = Int(row, c.Goals),
            Betrayals = Int(row, c.Betrayals),
            HammerKills = Int(row, c.Hammer),
            SwordKills = Int(row, c.Sword),
            BallKills = Int(row, c.Ball),
            CarrierKills = Int(row, c.Stops),
        }).Where(p => p.Gamertag.Length > 0).ToList();
        var teams = block.Rows.GroupBy(row => Side(c.TeamOf(row))).OrderBy(s => s.Key).Select(s => new RecordedTeamInput
        {
            Side = s.Key,
            Outcome = s.Sum(row => Int(row, c.Wins) ?? 0) > 0 ? Outcomes.Won : s.Sum(row => Int(row, c.Losses) ?? 0) > 0 ? Outcomes.Lost : Outcomes.DidNotFinish,
            Score = s.Sum(row => Int(row, c.Goals) ?? 0),
        }).ToList();
        var gameName = c.Game < 0 ? null : block.Rows.Select(row => Cell(row, c.Game).Trim()).FirstOrDefault(n => n.Length > 0);
        return new RecordedGameInput
        {
            SeasonSourceId = tab.Season,
            SourceKey = key,
            GameVersion = HistoricalKeys.GameVersionSlug(gameName) ?? tab.Game,
            PlayedAt = block.PlayedAt,
            IsPlayoffs = block.Playoffs,
            IsScrim = tab.Scrims,
            Teams = teams,
            Players = players,
        };
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
            if (BlockHeaderRow(sheet, r) is not { } headerRow)
            {
                // A forfeit has a title only: nobody played it
                if (ForfeitGame(sourceId, tab, title, titleMatch, titleRow) is { } forfeit)
                    games.Add(forfeit);
                continue;
            }
            var block = BlockPlayers(sheet, headerRow);
            r = block.End - 1;
            games.Add(BlockGame(sourceId, tab, title, titleMatch, titleRow, block));
        }
        return games;
    }

    /// <summary>The header row after a block's title (after a "Postgame Page" banner row); null when there is none before
    /// the next title (a forfeit's title has no block).</summary>
    private static int? BlockHeaderRow(SheetTable sheet, int titleRow)
    {
        var headerRow = titleRow + 1;
        while (headerRow < sheet.Rows.Count && headerRow <= titleRow + 3 && ArchiveCells.HeaderKey(sheet.Cell(headerRow, 1)) != "player" && !TitlePattern().IsMatch(sheet.Cell(headerRow, 0).Trim()))
            headerRow++;
        return headerRow >= sheet.Rows.Count || ArchiveCells.HeaderKey(sheet.Cell(headerRow, 1)) != "player" ? null : headerRow;
    }

    /// <summary>A block's players, each side's wins and name, and the row after its last (the next title's, or the sheet's end).</summary>
    private sealed record BlockRows(List<RecordedPlayerInput> Players, Dictionary<int, int> SideWins, Dictionary<int, string> SideNames, int End);

    private static BlockRows BlockPlayers(SheetTable sheet, int headerRow)
    {
        var columns = new Columns(sheet.Rows[headerRow]);
        var (played, wins, goals, kills, assists, deaths, stops) = (columns.Find("gamesplayed"), columns.Find("wins"), columns.Find("goals"),
            columns.Find("kills"), columns.Find("assists"), columns.Find("deaths"), columns.Find("carrierkills", "carrierkill"));

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
            var rawPlayer = Cell(row, 1).Trim();
            if (SeasonStatsParser.IsTotalsLabel(first) || IsSummaryOrHeader(rawPlayer, first))
                continue;
            var named = first.Length > 0 && !first.Equals("Sub", StringComparison.OrdinalIgnoreCase);
            if (named && !sideOf.TryGetValue(first, out side))
                sideOf[first] = side = sideOf.Count + 1;
            var gamertag = ArchiveCells.Gamertag(rawPlayer).Gamertag;
            if (gamertag.Length == 0 || !Played(row, played, kills, deaths))
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
            if (named)
                sideNames.TryAdd(side, first);
        }
        return new BlockRows(players, sideWins, sideNames, end);
    }

    private static RecordedGameInput BlockGame(string sourceId, GamesTab tab, string title, System.Text.RegularExpressions.Match titleMatch, int titleRow, BlockRows block)
    {
        var tied = titleMatch.Groups["verb"].Value.Equals("tied", StringComparison.OrdinalIgnoreCase);
        var score = ScorePattern().Match(title);
        var (high, low) = score.Success ? (int.Parse(score.Groups[1].Value), int.Parse(score.Groups[2].Value)) : ((int?)null, (int?)null);
        var teams = block.Players.Select(p => p.Side).Distinct().Order().Select(s =>
        {
            var won = block.SideWins.GetValueOrDefault(s) > 0;
            return new RecordedTeamInput
            {
                Side = s,
                Name = block.SideNames.GetValueOrDefault(s),
                Outcome = tied ? Outcomes.Tie : won ? Outcomes.Won : Outcomes.Lost,
                Score = won || tied ? high : low,
            };
        }).ToList();
        CompleteFromTitle(title, teams, tied, high, low);
        return new RecordedGameInput
        {
            SeasonSourceId = tab.Season,
            SourceKey = $"{sourceId}/{tab.Sheet}#{titleRow}",
            GameVersion = tab.Game,
            PlayedAt = BlockDate(title),
            IsPlayoffs = PlayoffsPattern().IsMatch(titleMatch.Groups["phase"].Value),
            IsScrim = tab.Scrims,
            Vod = TitleLink(title),
            Teams = teams,
            Players = block.Players,
        };
    }

    /// <summary>The sides a block lacks, from its title: a partial copy's other side (<see cref="OtherSide"/>), or both sides of
    /// a block without stats (NO STATS), whose title is all there is of the game.</summary>
    private static void CompleteFromTitle(string title, List<RecordedTeamInput> teams, bool tied, int? high, int? low)
    {
        if (teams.Count == 1 && OtherSide(title, teams[0], tied, high, low) is { } other)
            teams.Add(other);
        if (teams.Count > 0 || TitleTeams(title) is not { } named)
            return;
        teams.Add(new RecordedTeamInput { Side = 1, Name = named.Winner, Outcome = tied ? Outcomes.Tie : Outcomes.Won, Score = high });
        teams.Add(new RecordedTeamInput { Side = 2, Name = named.Loser, Outcome = tied ? Outcomes.Tie : Outcomes.Lost, Score = tied ? high : low });
    }

    /// <summary>
    /// The side a block has no players of (a partial copy), from its title: the team the title names that the block's
    /// side is not (by its name, else by its result: the winner is named first). Null when the title says too little.
    /// </summary>
    private static RecordedTeamInput? OtherSide(string title, RecordedTeamInput side, bool tied, int? high, int? low)
    {
        if (TitleTeams(title) is not { } named)
            return null;
        var key = HistoricalArchiveBuilder.TeamKey(side.Name ?? "");
        string? other = key == HistoricalArchiveBuilder.TeamKey(named.Winner) ? named.Loser : key == HistoricalArchiveBuilder.TeamKey(named.Loser) ? named.Winner : null;
        if (other is null && !tied)
            other = side.Outcome == Outcomes.Won ? named.Loser : named.Winner;
        if (other is null)
            return null;
        var outcome = side.Outcome switch { Outcomes.Won => Outcomes.Lost, Outcomes.Lost => Outcomes.Won, _ => side.Outcome };
        return new RecordedTeamInput { Side = side.Side + 1, Name = other, Outcome = outcome, Score = outcome == Outcomes.Lost ? low : high };
    }

    /// <summary>A title's game without a block, when the title says it was forfeited ("A over B - Forfeit", "A FF Win over B",
    /// "A (FF W) over B"): the first team won it.</summary>
    private static RecordedGameInput? ForfeitGame(string sourceId, GamesTab tab, string title, System.Text.RegularExpressions.Match titleMatch, int titleRow)
    {
        if (!ForfeitPattern().IsMatch(title) || TitleTeams(title) is not { } named)
            return null;
        return new RecordedGameInput
        {
            SeasonSourceId = tab.Season,
            SourceKey = $"{sourceId}/{tab.Sheet}#{titleRow}",
            GameVersion = tab.Game,
            PlayedAt = BlockDate(title),
            IsPlayoffs = PlayoffsPattern().IsMatch(titleMatch.Groups["phase"].Value),
            IsScrim = tab.Scrims,
            IsForfeit = true,
            Teams =
            [
                new RecordedTeamInput { Side = 1, Name = named.Winner, Outcome = Outcomes.Won },
                new RecordedTeamInput { Side = 2, Name = named.Loser, Outcome = Outcomes.Lost },
            ],
        };
    }

    /// <summary>The two teams a block's title names: the one before "over" (the winner, or one side of a tie) and the one after,
    /// without the forfeit marks, score, notes and links after them.</summary>
    private static (string Winner, string Loser)? TitleTeams(string title)
        => title.Split(" - ").Select(s => TitleMatchupPattern().Match(s.Trim())).Where(m => m.Success)
            .Select(m => ((string Winner, string Loser)?)(m.Groups["a"].Value.Trim(), m.Groups["b"].Value.Trim())).FirstOrDefault();

    /// <summary>The first link in a title (the game's video).</summary>
    private static string? TitleLink(string title)
    {
        var link = LinkPattern().Match(title);
        return link.Success ? link.Value : null;
    }

    /// <summary>Whether a block's row played: a player who didn't has 0 games; a block without a games column (DJBluePDX's)
    /// lists only who played, by their kills or deaths.</summary>
    private static bool Played(string[] row, int played, int kills, int deaths)
        => played >= 0 ? (Int(row, played) ?? 0) != 0 : Int(row, kills) is not null || Int(row, deaths) is not null;

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

    // "Week 1 - 1/28/19 - A over B (5-2)", or the winner first with no phase: "A over B 5-3 - host" (DJBluePDX's, one
    // title of which spells it "pver"), a score or a " - host" after it: a team's row ("Thunder Over Dallas") has neither
    [GeneratedRegex(@"^(?:(?<phase>[^-]*?)\s*-.*\s(?<verb>over|pver|tied)\s|[^-]+?\s(?<verb>over|pver|tied)\s.*?(?:\d{1,2}-\d{1,2}|\s-\s))", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"\(?\b(\d{1,2})-(\d{1,2})\b\)?")]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>"A over B (5-2) RECORDED https://...", "A FF Win over B", "A (FF W) over B": the teams, without what follows.</summary>
    [GeneratedRegex(@"^(?<a>.+?)(?:\s+\(FF W\)|\s+FF Win)?\s+(?:over|tied)\s+(?<b>.+?)(?=\s*(?:\(\d|\(FF|\d{1,2}\s*-\s*\d|RECORDED|NO STATS|https?://|$))", RegexOptions.IgnoreCase)]
    private static partial Regex TitleMatchupPattern();

    [GeneratedRegex(@"\bforfeit\b|\bFF\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForfeitPattern();

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();
}
