using System.Globalization;
using System.Text.RegularExpressions;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>
/// A source's database tables of games (<see cref="GamesTable"/>: the grifball.com and grifball.info backups) as recorded
/// games, each with its players' lines where the database has them, and forfeits. Each game goes under its season's key
/// in the <see cref="StatsTable"/> the config names (the same season IDs, team lookup and divisions), so it joins the
/// season its stats make. What can't be placed (a season the stats table doesn't name, a game without both sides, a game
/// listed twice, a player line of no listed game or side) is left out and counted in one warning per table.
/// </summary>
public static partial class GamesTableParser
{
    /// <summary>The games and forfeits of a source's game tables.</summary>
    public sealed record Result(List<RecordedGameInput> Games, List<ArchiveForfeitInput> Forfeits);

    public static Result Parse(string sourceId, HistoricalSourceConfig config, SheetWorkbook workbook, List<string> warnings)
    {
        var result = new Result([], []);
        foreach (var table in config.GameTables)
        {
            var stats = config.Tables.FirstOrDefault(t => string.Equals(t.Sheet, table.Stats, StringComparison.OrdinalIgnoreCase));
            var sheet = workbook.Sheet(table.Sheet);
            if (stats is null || sheet is null || sheet.Rows.Count < 2)
            {
                warnings.Add($"{sourceId}: games table '{table.Sheet}' not read ({(stats is null ? $"no stats table '{table.Stats}'" : "no such sheet, or it is empty")})");
                continue;
            }
            var reader = new TableReader(sourceId, table, stats, workbook);
            switch (table.Layout)
            {
                case GamesTableLayout.TeamRows:
                    reader.TeamRows(sheet, result);
                    break;
                case GamesTableLayout.Schedule:
                    reader.Schedule(sheet, result);
                    break;
                case GamesTableLayout.Reported:
                    reader.Reported(sheet, result);
                    break;
                case GamesTableLayout.ScheduleResults:
                    reader.ScheduleResults(sheet, result);
                    break;
                default:
                    reader.Matches(sheet, result);
                    break;
            }
            warnings.AddRange(reader.Problems());
        }
        return result;
    }

    /// <summary>A header's columns, found by name (lower case, spaces out, as <see cref="ArchiveCells.HeaderKey"/>).</summary>
    private sealed class Header(string[] header)
    {
        private readonly List<string> _keys = header.Select(ArchiveCells.HeaderKey).ToList();

        /// <summary>The column of a name; -1 for none, and for no name (never a blank header cell).</summary>
        public int this[string name] => name.Length == 0 ? -1 : _keys.IndexOf(name);
    }

    /// <summary>A player lines sheet's header and its rows by game.</summary>
    private sealed record PlayerLines(Header Header, Dictionary<string, List<string[]>> ByGame);

    /// <summary>The columns of a player line, -1 for one the table hasn't.</summary>
    private sealed record LineColumns(int Gamertag, int Kills, int Deaths, int Assists, int Goals, int Betrayals, int Grabs, int Hammer, int Sword, int Ball, int Stops)
    {
        public static LineColumns Of(Header h) => new(h["gamertag"], h["kills"], h["deaths"], h["assists"], h["goals"], h["betrayals"], h["grabs"],
            h["hammer_kills"], h["sword_kills"], h["bomb_kills"], h["bombstops"]);

        public RecordedPlayerInput Line(string[] row, string gamertag, int side) => new()
        {
            Gamertag = gamertag,
            Side = side,
            Kills = Int(row, Kills) ?? 0,
            Deaths = Int(row, Deaths) ?? 0,
            Assists = Int(row, Assists) ?? 0,
            Goals = Int(row, Goals),
            Betrayals = Int(row, Betrayals),
            Grabs = Int(row, Grabs),
            HammerKills = Int(row, Hammer),
            SwordKills = Int(row, Sword),
            BallKills = Int(row, Ball),
            CarrierKills = Int(row, Stops),
        };
    }

    /// <summary>One game table: its stats table's seasons and teams, its player lines and deleted games, and what it left out.</summary>
    private sealed class TableReader
    {
        private readonly string _sourceId;
        private readonly GamesTable _table;
        private readonly StatsTable _stats;
        private readonly SheetWorkbook _workbook;
        private readonly Dictionary<string, string> _canonical;
        private readonly Dictionary<string, string[]> _teams = new(StringComparer.Ordinal);
        private readonly int _teamName;
        private readonly int _division;
        private readonly HashSet<string> _deleted = new(StringComparer.Ordinal);
        private readonly SortedDictionary<string, int> _unknownSeasons = new(StringComparer.Ordinal);
        private int _noDivision;
        private int _incomplete;
        private int _duplicateGames;
        private int _duplicateLines;
        private int _strayLines;

        public TableReader(string sourceId, GamesTable table, StatsTable stats, SheetWorkbook workbook)
        {
            (_sourceId, _table, _stats, _workbook) = (sourceId, table, stats, workbook);
            _canonical = StatsTableParser.CanonicalSeasonIds(stats);
            var (teamsHeader, teams) = Table(stats.Teams?.Sheet);
            var key = teamsHeader[ArchiveCells.HeaderKey(stats.Teams?.Key ?? "")];
            (_teamName, _division) = (teamsHeader[ArchiveCells.HeaderKey(stats.Teams?.Column ?? "")], teamsHeader[ArchiveCells.HeaderKey(stats.DivisionColumn ?? "")]);
            foreach (var row in teams)
                if (Value(row, key) is { } id)
                    _teams.TryAdd(id, row);
            var (deletedHeader, deleted) = Table(table.DeletedSheet);
            _deleted.UnionWith(deleted.Select(r => Value(r, deletedHeader["gameid"])).OfType<string>());
        }

        /// <summary>A sheet's header and the rows below it; none for no sheet.</summary>
        private (Header Header, IEnumerable<string[]> Rows) Table(string? name)
            => name is not null && _workbook.Sheet(name) is { } sheet ? (new Header(sheet.Rows.FirstOrDefault() ?? []), sheet.Rows.Skip(1)) : (new Header([]), []);

        private string Key(string id) => $"{_sourceId}/{_table.Sheet}#{id}";

        /// <summary>A team's name and its row in the stats table's teams tab, by the key the games give (an ID or a name).</summary>
        private (string Name, string[]? Row) Team(string key)
            => _teams.TryGetValue(key, out var row) ? (Value(row, _teamName) ?? key, row) : (key, null);

        /// <summary>The key of the season (and division, by <paramref name="team"/>'s) a game of <paramref name="seasonId"/>
        /// is under; null, counted, for a season the stats table doesn't name or a team of none of its divisions.</summary>
        private string? SeasonKey(string? seasonId, string[]? team)
        {
            if (seasonId is null || !_stats.Seasons.TryGetValue(seasonId, out var season))
            {
                var id = seasonId ?? "(none)";
                _unknownSeasons[id] = _unknownSeasons.GetValueOrDefault(id) + 1;
                return null;
            }
            string? division = null;
            if (season.Divisions is not null && (team is null || Value(team, _division) is not { } value || !season.Divisions.TryGetValue(value, out division)))
            {
                _noDivision++;
                return null;
            }
            return HistoricalKeys.SeasonSourceId(_sourceId, $"{_stats.Sheet}#s{_canonical[seasonId]}" + (division is null ? "" : $"_{division}"));
        }

        /// <summary>The player lines sheet's rows by a game's key column, read once; none without the sheet.</summary>
        private PlayerLines Lines(string gameColumn)
        {
            var (header, rowsBelow) = Table(_table.PlayersSheet);
            var column = header[gameColumn];
            var byGame = new Dictionary<string, List<string[]>>(StringComparer.Ordinal);
            foreach (var row in rowsBelow)
            {
                if (Value(row, column) is not { } game)
                    continue;
                if (!byGame.TryGetValue(game, out var rows))
                    byGame[game] = rows = [];
                rows.Add(row);
            }
            return new PlayerLines(header, byGame);
        }

        /// <summary>The lines of the games not listed: counted, as left out.</summary>
        private void CountStray(PlayerLines lines, HashSet<string> listed)
            => _strayLines += lines.ByGame.Where(g => !listed.Contains(g.Key)).Sum(g => g.Value.Count);

        /// <summary>A game's players: a line per player, the first kept (a second for the same gamertag is counted).</summary>
        private List<RecordedPlayerInput> Distinct(IEnumerable<RecordedPlayerInput> lines)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var players = new List<RecordedPlayerInput>();
            foreach (var line in lines)
            {
                if (seen.Add(HistoricalKeys.Normalize(line.Gamertag)))
                    players.Add(line);
                else
                    _duplicateLines++;
            }
            return players;
        }

        private static RecordedTeamInput Side(int side, string name, Outcomes outcome, int? score) => new() { Side = side, Name = name, Outcome = outcome, Score = score, NamesItsTeam = true };

        private string GameVersionOf(string? seasonId)
            => seasonId is not null && _canonical.TryGetValue(seasonId, out var canonical) && _stats.Seasons.TryGetValue(canonical, out var season) && !string.IsNullOrWhiteSpace(season.Game)
                ? season.Game
                : _stats.Game;

        private RecordedGameInput Game(string season, string id, string[] teams, (Outcomes Outcome, int? Score)[] results, List<RecordedPlayerInput> players, bool playoffs = false, DateTime? playedAt = null, string? gameVersion = null) => new()
        {
            SeasonSourceId = season,
            SourceKey = Key(id),
            GameVersion = gameVersion ?? _stats.Game,
            PlayedAt = playedAt,
            IsPlayoffs = playoffs,
            Teams = [Side(1, teams[0], results[0].Outcome, results[0].Score), Side(2, teams[1], results[1].Outcome, results[1].Score)],
            Players = players,
        };

        private ArchiveForfeitInput Forfeit(string season, string id, string winner, string loser, bool both, DateTime? playedAt = null, bool playoffs = false) => new()
        {
            IsPlayoffs = playoffs,
            SeasonSourceId = season,
            SourceKey = Key(id),
            Winner = winner,
            Loser = loser,
            BothForfeited = both,
            PlayedAt = playedAt,
        };

        private static Outcomes Outcome(int? score, int? other) => score > other ? Outcomes.Won : score < other ? Outcomes.Lost : Outcomes.Tie;

        /// <summary>grifball.com's Matches (<see cref="GamesTableLayout.Matches"/>).</summary>
        public void Matches(SheetTable sheet, Result result)
        {
            var h = new Header(sheet.Rows[0]);
            var (id, season, game, forfeit) = (h["match_id"], h[ArchiveCells.HeaderKey(_stats.SeasonColumn)], h["bungie_game_id"], h["is_forfeit"]);
            var (teamA, scoreA, teamB, scoreB) = (h["team1_name"], h["team1_score"], h["team2_name"], h["team2_score"]);
            var lines = Lines("match_id");
            var columns = LineColumns.Of(lines.Header);
            var side = lines.Header["side"];
            var listed = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in sheet.Rows.Skip(1))
            {
                if (Value(row, id) is not { } match || SeasonKey(Value(row, season), null) is not { } key)
                    continue;
                if (Value(row, teamA) is not { } a || Value(row, teamB) is not { } b)
                {
                    _incomplete++;
                    continue;
                }
                // A forfeit has no game: the site listed the side that won first
                if (Int(row, forfeit) == 1)
                {
                    result.Forfeits.Add(Forfeit(key, match, a, b, both: false));
                    continue;
                }
                if (Value(row, game) is { } gameId && !seen.Add(gameId))
                {
                    _duplicateGames++;
                    continue;
                }
                listed.Add(match);
                var (sa, sb) = (Int(row, scoreA), Int(row, scoreB));
                var players = lines.ByGame.GetValueOrDefault(match) ?? [];
                result.Games.Add(Game(key, match, [a, b], [(Outcome(sa, sb), sa), (Outcome(sb, sa), sb)],
                    Distinct(MatchLines(players, columns, side))));
            }
            CountStray(lines, listed);
        }

        private static IEnumerable<RecordedPlayerInput> MatchLines(IEnumerable<string[]> players, LineColumns columns, int side)
        {
            foreach (var p in players)
                if (Value(p, columns.Gamertag) is { } gamertag && !SeasonStatsParser.IsTotalsLabel(gamertag))
                    yield return columns.Line(p, gamertag, Int(p, side) ?? 0);
        }

        /// <summary>gb_teamstats' columns.</summary>
        private sealed record TeamRowColumns(int Game, int Team, int Season, int Playoffs, int Win, int Loss, int ForfeitWin, int ForfeitLoss, int Goals);

        /// <summary>grifball.info's gb_teamstats (<see cref="GamesTableLayout.TeamRows"/>): a game is its two rows, in game ID order.</summary>
        public void TeamRows(SheetTable sheet, Result result)
        {
            var h = new Header(sheet.Rows[0]);
            var c = new TeamRowColumns(h["gameid"], h["teamid"], h["t_season"], h["t_playoffs"], h["t_win"], h["t_loss"], h["t_fwin"], h["t_floss"], h["t_goals"]);
            var games = sheet.Rows.Skip(1)
                .Where(r => Value(r, c.Team) is not null)
                .GroupBy(r => Value(r, c.Game) ?? "0")
                .OrderBy(g => decimal.TryParse(g.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0);
            foreach (var rows in games.Where(g => !_deleted.Contains(g.Key)))
                TeamRowsGame(rows.Key, rows.ToList(), c, result);
        }

        /// <summary>One game of gb_teamstats: its two rows, a game or a forfeit.</summary>
        private void TeamRowsGame(string id, List<string[]> rows, TeamRowColumns c, Result result)
        {
            // A forfeit's rows have no game ID (0), so nothing pairs its two sides
            if (id == "0" || rows.Count != 2)
            {
                _incomplete += rows.Count;
                return;
            }
            var (ra, rb) = (rows[0], rows[1]);
            var (a, b) = (Team(Value(ra, c.Team)!), Team(Value(rb, c.Team)!));
            if (SeasonKey(Value(ra, c.Season), a.Row) is not { } key)
                return;
            if (TeamRowsForfeit(key, id, (ra, a.Name), (rb, b.Name), c) is { } forfeit)
            {
                result.Forfeits.Add(forfeit);
                return;
            }
            Outcomes SideOutcome(string[] r) => Int(r, c.Win) == 1 ? Outcomes.Won : Int(r, c.Loss) == 1 ? Outcomes.Lost : Outcomes.DidNotFinish;
            result.Games.Add(Game(key, id, [a.Name, b.Name], [(SideOutcome(ra), Int(ra, c.Goals)), (SideOutcome(rb), Int(rb, c.Goals))], [],
                playoffs: Int(ra, c.Playoffs) == 1));
        }

        /// <summary>The game as a forfeit, when a side forfeited (lost) or won by the other's forfeit; both when both forfeited.</summary>
        private ArchiveForfeitInput? TeamRowsForfeit(string key, string id, (string[] Row, string Name) a, (string[] Row, string Name) b, TeamRowColumns c)
        {
            var (aForfeited, bForfeited) = (Int(a.Row, c.ForfeitLoss) == 1, Int(b.Row, c.ForfeitLoss) == 1);
            var (aWon, bWon) = (Int(a.Row, c.ForfeitWin) == 1, Int(b.Row, c.ForfeitWin) == 1 || (aForfeited && !bForfeited));
            if (!aForfeited && !bForfeited && !aWon && !bWon)
                return null;
            var playoffs = Int(a.Row, c.Playoffs) == 1;
            return bWon ? Forfeit(key, id, b.Name, a.Name, both: false, playoffs: playoffs) : Forfeit(key, id, a.Name, b.Name, both: aForfeited && bForfeited, playoffs: playoffs);
        }

        /// <summary>h_games' columns, and its player lines and their gamertags.</summary>
        private sealed record ScheduleColumns(int Id, int Season, int TeamA, int TeamB, int Result, int Win, int Playoffs, int Link, int Reported,
            PlayerLines Lines, (int RowKey, Dictionary<string, string> ById) Gamertags, HashSet<string> Listed)
        {
            /// <summary>The lines' own columns: their stats, team, season, phase and result.</summary>
            public LineColumns Line { get; } = LineColumns.Of(Lines.Header);
            public (int Team, int Season, int Playoffs, int Wins, int Losses) Of { get; } = (Lines.Header["teamid"], Lines.Header["season"], Lines.Header["playoffs"], Lines.Header["wins"], Lines.Header["losses"]);
        }

        /// <summary>grifball.info's h_games (<see cref="GamesTableLayout.Schedule"/>): its played games and forfeits; its
        /// player lines are the stats table's rows with the game's link.</summary>
        public void Schedule(SheetTable sheet, Result result)
        {
            var h = new Header(sheet.Rows[0]);
            var lines = Lines("link");
            var c = new ScheduleColumns(h["id"], h["season"], h["teama"], h["teamb"], h["result"], h["win"], h["playoffs"], h["link"], h["date_reported"],
                lines, Gamertags(lines.Header), new HashSet<string>(StringComparer.Ordinal));
            foreach (var row in sheet.Rows.Skip(1))
                ScheduleRow(row, c, result);
            // A game the schedule doesn't list, its lines all there are of it (a placeholder link "0" is no game)
            foreach (var (link, rows) in c.Lines.ByGame.Where(g => g.Key != "0" && !c.Listed.Contains(g.Key)))
                UnlistedGame(link, rows, c, result);
            CountStray(c.Lines, c.Listed);
        }

        /// <summary>One row of h_games: a played game, a forfeit, or nothing yet (no result).</summary>
        private void ScheduleRow(string[] row, ScheduleColumns c, Result result)
        {
            if (Value(row, c.Id) is not { } game || Value(row, c.Result) is not { } text)
                return;
            var (a, b, winner) = (Value(row, c.TeamA), Value(row, c.TeamB), Value(row, c.Win));
            var score = ScorePattern().Match(text);
            var forfeit = text.Contains("forfeit", StringComparison.OrdinalIgnoreCase);
            // A double forfeit has no winner to name
            var both = text.StartsWith("double", StringComparison.OrdinalIgnoreCase);
            if (a is null || b is null || (winner != a && winner != b && !both) || (!forfeit && !score.Success))
            {
                _incomplete++;
                return;
            }
            var (teamOfA, teamOfB) = (Team(a), Team(b));
            if (SeasonKey(Value(row, c.Season), teamOfA.Row) is not { } key)
                return;
            var playedAt = DateTime.TryParse(Value(row, c.Reported), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ? at : (DateTime?)null;
            var aWon = winner == a;
            if (forfeit)
            {
                var playoffs = Int(row, c.Playoffs) == 1;
                result.Forfeits.Add(aWon
                    ? Forfeit(key, game, teamOfA.Name, teamOfB.Name, both, playedAt, playoffs)
                    : Forfeit(key, game, teamOfB.Name, teamOfA.Name, both, playedAt, playoffs));
                return;
            }
            var (x, y) = (int.Parse(score.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(score.Groups[2].Value, CultureInfo.InvariantCulture));
            var (high, low) = (Math.Max(x, y), Math.Min(x, y));
            result.Games.Add(Game(key, game, [teamOfA.Name, teamOfB.Name],
                aWon ? [(Outcomes.Won, high), (Outcomes.Lost, low)] : [(Outcomes.Lost, low), (Outcomes.Won, high)],
                SchedulePlayers(Value(row, c.Link), c, a, b), playoffs: Int(row, c.Playoffs) == 1, playedAt: playedAt));
        }

        /// <summary>A schedule game's players: the lines of its link, taken by the first game that names it.</summary>
        private List<RecordedPlayerInput> SchedulePlayers(string? link, ScheduleColumns c, string teamA, string teamB)
            => link is not null and not "0" && c.Lines.ByGame.GetValueOrDefault(link) is { } rows && c.Listed.Add(link)
                ? Distinct(rows.Select(p => PlayerOf(p, c, teamA, teamB)).OfType<RecordedPlayerInput>())
                : [];

        /// <summary>A game only the player lines have (its link in no schedule row): its two teams the lines', each side's
        /// result its lines' wins and losses, its season and phase theirs. Lines of one team, or of more than two, stay left out.</summary>
        private void UnlistedGame(string link, List<string[]> rows, ScheduleColumns c, Result result)
        {
            var teams = rows.Select(r => Value(r, c.Of.Team)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            if (teams.Count != 2)
                return;
            var (a, b) = (Team(teams[0]), Team(teams[1]));
            if (SeasonKey(Value(rows[0], c.Of.Season), a.Row) is not { } key)
                return;
            c.Listed.Add(link);
            Outcomes Outcome(string team) => rows.Any(r => Value(r, c.Of.Team) == team && Int(r, c.Of.Wins) == 1) ? Outcomes.Won
                : rows.Any(r => Value(r, c.Of.Team) == team && Int(r, c.Of.Losses) == 1) ? Outcomes.Lost : Outcomes.DidNotFinish;
            result.Games.Add(Game(key, link, [a.Name, b.Name], [(Outcome(teams[0]), null), (Outcome(teams[1]), null)],
                Distinct(rows.Select(p => PlayerOf(p, c, teams[0], teams[1])).OfType<RecordedPlayerInput>()), playoffs: Int(rows[0], c.Of.Playoffs) == 1));
        }

        /// <summary>The stats table's gamertags by player ID (its <see cref="StatsTable.Players"/> lookup), for lines with none.</summary>
        private (int RowKey, Dictionary<string, string> ById) Gamertags(Header lines)
        {
            var byId = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_stats.Players is not { } lookup)
                return (-1, byId);
            var (header, rows) = Table(lookup.Sheet);
            var (key, column) = (header[ArchiveCells.HeaderKey(lookup.Key)], header[ArchiveCells.HeaderKey(lookup.Column)]);
            foreach (var row in rows)
                if (Value(row, key) is { } id && Value(row, column) is { } gamertag)
                    byId.TryAdd(id, gamertag);
            return (lines[ArchiveCells.HeaderKey(lookup.RowKey ?? lookup.Key)], byId);
        }

        /// <summary>A schedule game's player line: its side by its team, its gamertag its own or the lookup's.</summary>
        private RecordedPlayerInput? PlayerOf(string[] row, ScheduleColumns c, string teamA, string teamB)
        {
            var columns = c.Line;
            var team = Value(row, c.Of.Team);
            var gamertag = Value(row, columns.Gamertag) ?? c.Gamertags.ById.GetValueOrDefault(Value(row, c.Gamertags.RowKey) ?? "");
            if (gamertag is null || SeasonStatsParser.IsTotalsLabel(gamertag) || (team != teamA && team != teamB))
            {
                _strayLines++;
                return null;
            }
            return columns.Line(row, gamertag, team == teamA ? 1 : 2);
        }

        /// <summary>agla_gamesreported's columns, and its player lines.</summary>
        private sealed record ReportedColumns(int Index, int Game, int Winning, int Losing, int Date, int Season, int Forfeit, PlayerLines Lines, HashSet<string> Listed)
        {
            /// <summary>The lines' own columns: their stats, team and phase.</summary>
            public LineColumns Line { get; } = LineColumns.Of(Lines.Header);
            public (int Team, int Playoffs) Of { get; } = (Lines.Header["teamname"], Lines.Header["playoffs"]);
        }

        /// <summary>grifball.info's agla_gamesreported (<see cref="GamesTableLayout.Reported"/>): each game the winner's and
        /// loser's, its player lines by game ID, a playoff game when they say so.</summary>
        public void Reported(SheetTable sheet, Result result)
        {
            var h = new Header(sheet.Rows[0]);
            var c = new ReportedColumns(h["index"], h["gameid"], h["winning_team"], h["losing_team"], h["game_date"], h["g_season"], h["forfeit"],
                Lines("gameid"), new HashSet<string>(StringComparer.Ordinal));
            foreach (var row in sheet.Rows.Skip(1))
                ReportedRow(row, c, result);
            CountStray(c.Lines, c.Listed);
        }

        /// <summary>One row of agla_gamesreported: a game or a forfeit.</summary>
        private void ReportedRow(string[] row, ReportedColumns c, Result result)
        {
            if (Value(row, c.Index) is not { } id)
                return;
            if (Value(row, c.Winning) is not { } w || Value(row, c.Losing) is not { } l)
            {
                _incomplete++;
                return;
            }
            var (won, lost) = (Team(w), Team(l));
            if (SeasonKey(Value(row, c.Season), won.Row) is not { } key)
                return;
            var playedAt = Int(row, c.Date) is int seconds and > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : (DateTime?)null;
            if (Int(row, c.Forfeit) is 1 or 2)
            {
                result.Forfeits.Add(Forfeit(key, id, won.Name, lost.Name, both: Int(row, c.Forfeit) == 2, playedAt));
                return;
            }
            if (ReportedGameId(Value(row, c.Game) ?? "0", c.Listed) is not { } gameId)
                return;
            var rows = c.Lines.ByGame.GetValueOrDefault(gameId) ?? [];
            var players = Distinct(rows.Select(p => SideLine(p, c, w, l)).OfType<RecordedPlayerInput>());
            var playoffs = rows.Any(p => Int(p, c.Of.Playoffs) == 1);
            result.Games.Add(Game(key, id, [won.Name, lost.Name], [(Outcomes.Won, null), (Outcomes.Lost, null)], players, playoffs, playedAt));
        }

        /// <summary>A reported game's ID; null for a deleted game, a game without one (counted) or a game reported again (counted).</summary>
        private string? ReportedGameId(string gameId, HashSet<string> listed)
        {
            if (_deleted.Contains(gameId))
                return null;
            if (gameId == "0")
                _incomplete++;
            else if (!listed.Add(gameId))
                _duplicateGames++;
            else
                return gameId;
            return null;
        }

        /// <summary>A reported game's player line: side 1 the winner's team, 2 the loser's; another team's is counted and left out.</summary>
        private RecordedPlayerInput? SideLine(string[] row, ReportedColumns c, string winner, string loser)
        {
            // The lines spell a team's name as they like ("Four Guys One Ball" of "Four guys one ball")
            var team = Value(row, c.Of.Team);
            var (isWinner, isLoser) = (string.Equals(team, winner, StringComparison.OrdinalIgnoreCase), string.Equals(team, loser, StringComparison.OrdinalIgnoreCase));
            if (Value(row, c.Line.Gamertag) is not { } gamertag || SeasonStatsParser.IsTotalsLabel(gamertag) || (!isWinner && !isLoser))
            {
                _strayLines++;
                return null;
            }
            return c.Line.Line(row, gamertag, isWinner ? 1 : 2);
        }

        /// <summary>Normalized schedule/match results (<see cref="GamesTableLayout.ScheduleResults"/>): skips unplayed rows without warning.</summary>
        public void ScheduleResults(SheetTable sheet, Result result)
        {
            var h = new Header(sheet.Rows[0]);
            var (seasonCol, divisionCol, teamACol, teamBCol) = (h[ArchiveCells.HeaderKey(_stats.SeasonColumn)], h["division"], h["team_a"], h["team_b"]);
            var (winnerCol, scoreACol, scoreBCol, forfeitCol, timeCol) = (h["winner"], h["score_a"], h["score_b"], h["is_forfeit"], h["match_time"]);
            var rowNumber = 0;
            foreach (var row in sheet.Rows.Skip(1))
            {
                rowNumber++;
                var rawSeason = Value(row, seasonCol);
                if (SeasonKey(rawSeason, null) is not { } key)
                    continue;
                if (Value(row, teamACol) is not { } a || Value(row, teamBCol) is not { } b)
                {
                    _incomplete++;
                    continue;
                }
                var winnerName = Value(row, winnerCol);
                var (sa, sb) = (Int(row, scoreACol), Int(row, scoreBCol));
                var isForfeit = Int(row, forfeitCol) == 1;
                if (!isForfeit && winnerName is null && (sa is null || sb is null))
                    continue;

                var playoffs = string.Equals(Value(row, divisionCol), "Playoffs", StringComparison.OrdinalIgnoreCase);
                var playedAt = ParseUtcDate(Value(row, timeCol));
                var id = rowNumber.ToString(CultureInfo.InvariantCulture);

                if (isForfeit)
                {
                    var (win, lose) = string.Equals(winnerName, b, StringComparison.OrdinalIgnoreCase) ? (b, a) : (a, b);
                    result.Forfeits.Add(Forfeit(key, id, win, lose, both: false, playedAt, playoffs));
                    continue;
                }

                var (oa, ob) = WinnerOutcomes(a, b, winnerName, sa, sb);
                result.Games.Add(Game(key, id, [a, b], [(oa, sa), (ob, sb)], [], playoffs, playedAt, GameVersionOf(rawSeason)));
            }
        }

        private static (Outcomes A, Outcomes B) WinnerOutcomes(string a, string b, string? winner, int? sa, int? sb)
        {
            if (string.Equals(winner, a, StringComparison.OrdinalIgnoreCase))
                return (Outcomes.Won, Outcomes.Lost);
            if (string.Equals(winner, b, StringComparison.OrdinalIgnoreCase))
                return (Outcomes.Lost, Outcomes.Won);
            return (Outcome(sa, sb), Outcome(sb, sa));
        }

        private static DateTime? ParseUtcDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || !YearPattern().IsMatch(text))
                return null;
            return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : null;
        }

        /// <summary>What the table left out, a warning per kind.</summary>
        public IEnumerable<string> Problems()
        {
            var at = $"{_sourceId}: {_table.Sheet}";
            foreach (var (id, count) in _unknownSeasons)
                yield return $"{at}: {count} game(s) of season {id}, which {_stats.Sheet}'s config doesn't name, skipped";
            if (_noDivision > 0)
                yield return $"{at}: {_noDivision} game(s) of a team in none of its season's divisions, skipped";
            if (_incomplete > 0)
                yield return $"{at}: {_incomplete} row(s) without both sides or a result, skipped";
            if (_duplicateGames > 0)
                yield return $"{at}: {_duplicateGames} game(s) listed twice, skipped";
            if (_duplicateLines > 0)
                yield return $"{at}: {_duplicateLines} second player line(s) for a player in one game, skipped";
            if (_strayLines > 0)
                yield return $"{at}: {_strayLines} player line(s) of no listed game or of neither side, skipped";
        }
    }

    private static string? Value(string[] row, int column) => ArchiveCells.TableValue(row, column);

    private static int? Int(string[] row, int column) => ArchiveCells.TableInt(row, column);

    /// <summary>A result's score, "(7 - 2)" or "7-2".</summary>
    [GeneratedRegex(@"(\d+)\s*-\s*(\d+)")]
    private static partial Regex ScorePattern();

    /// <summary>A 4-digit calendar year, so time-only schedule strings don't default to the current year.</summary>
    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();
}
