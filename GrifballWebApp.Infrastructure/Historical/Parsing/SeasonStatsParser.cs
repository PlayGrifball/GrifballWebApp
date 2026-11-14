using System.Text.RegularExpressions;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;

namespace GrifballWebApp.Infrastructure.Historical.Parsing;

/// <summary>
/// A workbook's season stats tabs as archive entries (what compile-all-seasons.mjs made): each tab's header names its
/// columns (<see cref="Columns"/>), each row below is a player's stint; a gamertag seen again in the tab is a later stint
/// (traded mid-season).
/// </summary>
public static partial class SeasonStatsParser
{
    /// <summary>Header (lower case) to the stat it is.</summary>
    public static readonly IReadOnlyDictionary<string, string> Columns = new Dictionary<string, string>
    {
        ["player"] = "gamertag", ["gamertag"] = "gamertag", ["players"] = "gamertag", ["name"] = "gamertag",
        ["team"] = "teamName", ["team name"] = "teamName",
        ["games"] = "gamesPlayed", ["games played"] = "gamesPlayed", ["gamesplayed"] = "gamesPlayed", ["played"] = "gamesPlayed", ["gp"] = "gamesPlayed",
        ["wins"] = "wins", ["win"] = "wins",
        ["losses"] = "losses", ["loses"] = "losses", ["loss"] = "losses",
        ["winpercent"] = "winPercent", ["win %"] = "winPercent", ["win percent"] = "winPercent", ["win/loss"] = "winPercent", ["win percentage"] = "winPercent",
        ["goals"] = "goals", ["kills"] = "kills", ["assists"] = "assists", ["assist"] = "assists", ["deaths"] = "deaths",
        ["k/d"] = "kdRatio", ["kd"] = "kdRatio", ["kd ratio"] = "kdRatio", ["k/d ratio"] = "kdRatio", ["kda"] = "kdRatio",
        ["spread"] = "kdSpread", ["k/d spread"] = "kdSpread", ["kd spread"] = "kdSpread", ["k-d"] = "kdSpread",
        ["carrier kills"] = "carrierKills", ["carrier kill"] = "carrierKills", ["carrier killers"] = "carrierKills", ["stops"] = "carrierKills",
        ["bomb stops"] = "carrierKills", ["bombstops"] = "carrierKills", ["plate stops"] = "carrierKills",
        ["avg life"] = "averageLifeSeconds", ["averagelife"] = "averageLifeSeconds", ["average life"] = "averageLifeSeconds",
        ["avg life duration"] = "averageLifeSeconds", ["average life duration"] = "averageLifeSeconds",
        ["time"] = "totalSecondsPlayed", ["minutes"] = "totalSecondsPlayed", ["minutes played"] = "totalSecondsPlayed", ["seconds played"] = "totalSecondsPlayed",
        ["game time"] = "totalSecondsPlayed", ["gametime"] = "totalSecondsPlayed", ["timealive"] = "totalSecondsPlayed", ["playingtime"] = "totalSecondsPlayed",
        ["sprees"] = "sprees", ["killing spree"] = "sprees", ["killing sprees"] = "sprees", ["killingsprees"] = "sprees",
        ["multikills"] = "multiKills", ["multikill"] = "multiKills",
        ["exterminations"] = "exterminations", ["extermination"] = "exterminations", ["exterm"] = "exterminations", ["exterms"] = "exterminations",
        ["betrayals"] = "betrayals",
        ["hammer kills"] = "hammerKills", ["hammerkills"] = "hammerKills", ["sword kills"] = "swordKills", ["swordkills"] = "swordKills",
        ["ball kills"] = "ballKills", ["ballkills"] = "ballKills", ["punches"] = "punches", ["ball punches"] = "punches",
        ["score"] = "score", ["damage done"] = "damageDealt", ["damage taken"] = "damageTaken",
    };

    public static List<ArchiveSeason> Parse(string sourceId, HistoricalSourceConfig config, SheetWorkbook workbook, List<string> warnings)
    {
        var seasons = new List<ArchiveSeason>();
        var rosters = Rosters(sourceId, config, workbook, warnings);
        foreach (var tab in config.Tabs)
        {
            if (ParseTab(sourceId, config, workbook, tab, rosters, warnings) is { } season)
                seasons.Add(season);
        }
        return seasons;
    }

    /// <summary>The teams' rosters, for players whose row names no team (or only the side of a game).</summary>
    private static Dictionary<string, string> Rosters(string sourceId, HistoricalSourceConfig config, SheetWorkbook workbook, List<string> warnings)
    {
        var rosters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in config.RosterSheets)
        {
            if (workbook.Sheet(name) is { } rosterSheet)
                foreach (var (player, team) in RosterParser.Parse(rosterSheet))
                    rosters.TryAdd(player, team);
            else
                warnings.Add($"{sourceId}: no sheet '{name}'");
        }
        return rosters;
    }

    /// <summary>A stats tab's season; null (with a warning) for a tab that isn't there or is empty.</summary>
    private static ArchiveSeason? ParseTab(string sourceId, HistoricalSourceConfig config, SheetWorkbook workbook, StatsTab tab,
        Dictionary<string, string> rosters, List<string> warnings)
    {
        var sheet = workbook.Sheet(tab.Name);
        if (sheet is null || sheet.Rows.Count <= tab.HeaderRow + 1)
        {
            warnings.Add($"{sourceId}: no sheet '{tab.Name}', or it is empty");
            return null;
        }
        var columns = ColumnsOf(sheet.Rows[tab.HeaderRow]);
        var dataRows = sheet.Rows.Skip(tab.HeaderRow + 1).ToList();
        // A Team column of only 0s and 1s is the side of the player's last game (the stats tool's sheets), not a team
        var teamColumns = columns.Where(c => c.Value.Key == "teamName").Select(c => c.Key).ToList();
        var sides = teamColumns.Count > 0 && dataRows.Select(r => teamColumns.Select(c => c < r.Length ? r[c].Trim() : "").LastOrDefault(v => v.Length > 0))
            .OfType<string>().Where(v => v.Length > 0).DefaultIfEmpty("x").All(v => v is "0" or "1");
        var played = PlayedDates(sourceId, workbook, tab, sheet, dataRows, warnings);
        var players = TabPlayers(sheet, tab, columns, sides, rosters);

        var defaults = config.Season;
        return new ArchiveSeason
        {
            SeasonName = tab.CustomSeason ?? defaults?.Name ?? sourceId,
            GameVersion = tab.Game ?? defaults?.Game ?? "",
            League = defaults?.League ?? "AGLA",
            Division = defaults?.Division ?? "Open",
            Format = defaults?.Format,
            SourceWorkbook = sourceId,
            SourceSheet = tab.Name,
            IsPlayoffs = tab.IsPlayoffs,
            IsCombined = tab.IsCombined,
            FirstPlayedAt = played.Count > 0 ? played.Min() : null,
            LastPlayedAt = played.Count > 0 ? played.Max() : null,
            SourceYear = tab.Year,
            Titles = tab.CustomSeason is null ? defaults?.Titles : null,
            Players = players,
        };
    }

    /// <summary>When the tab's games were played: the stats tool's TimeStamp column (each player's last game), or another
    /// tab's (a match history headed Timestamp), for a stats tab without one.</summary>
    private static List<DateTime> PlayedDates(string sourceId, SheetWorkbook workbook, StatsTab tab, SheetTable sheet, List<string[]> dataRows, List<string> warnings)
    {
        var stampColumn = StampColumn(sheet.Rows[tab.HeaderRow]);
        var played = stampColumn < 0 ? [] : Stamps(dataRows, stampColumn);
        if (tab.DatesSheet is { } datesName)
        {
            if (workbook.Sheet(datesName) is { Rows.Count: > 0 } dates)
            {
                var column = StampColumn(dates.Rows[0]);
                if (column >= 0)
                    played.AddRange(Stamps(dates.Rows.Skip(1), column));
            }
            else
                warnings.Add($"{sourceId}: no sheet '{datesName}'");
        }
        return played;
    }

    private static int StampColumn(string[] header) => Array.FindIndex(header, h => h.Trim().ToLowerInvariant() is "timestamp" or "time stamp");

    private static List<DateTime> Stamps(IEnumerable<string[]> rows, int column)
        => rows.Select(r => PlayedAt(column < r.Length ? r[column] : null)).OfType<DateTime>().ToList();

    /// <summary>The tab's player lines: a row with a gamertag (not a totals row, or the header again), a player's second
    /// row their second stint, the team their row's (unless it is a side) or else their roster's.</summary>
    private static List<ArchivePlayer> TabPlayers(SheetTable sheet, StatsTab tab, Dictionary<int, (string Key, string Header)> columns, bool sides,
        Dictionary<string, string> rosters)
    {
        var players = new List<ArchivePlayer>();
        var stints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? currentTeam = null;
        var sideBySideStart = -1;
        for (var r = tab.HeaderRow + 1; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var firstCell = row.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();
            if (tab.SideBySideGamesStart is { } sbs && firstCell?.StartsWith(sbs, StringComparison.OrdinalIgnoreCase) == true)
                sideBySideStart = r;
            if (tab.StopAt is { } stopAt && firstCell?.StartsWith(stopAt, StringComparison.OrdinalIgnoreCase) == true)
                break;

            if (TryParseTabRow(sheet, r, tab, columns, sides, rosters, stints, ref currentTeam) is { } player)
                players.Add(player);
        }

        if (tab.PerGame && sideBySideStart >= 0)
        {
            // A finals player keeps the team the tab or a roster has them on; the other side's header names a team by
            // whatever short name it was written with
            var teams = new Dictionary<string, string>(rosters, StringComparer.OrdinalIgnoreCase);
            foreach (var p in players.Where(p => p.TeamName is not null))
                teams[HistoricalKeys.Normalize(p.Gamertag)] = p.TeamName!;
            players.AddRange(SideBySideGamePlayers(sheet, sideBySideStart, teams));
        }

        return tab.PerGame ? AggregatePerGame(players) : players;
    }

    private static ArchivePlayer? TryParseTabRow(SheetTable sheet, int r, StatsTab tab, Dictionary<int, (string Key, string Header)> columns,
        bool sides, Dictionary<string, string> rosters, Dictionary<string, int> stints, ref string? currentTeam)
    {
        var row = sheet.Rows[r];
        if (row.All(string.IsNullOrWhiteSpace))
        {
            currentTeam = null;
            return null;
        }
        var (rawGamertag, teamName) = NameCells(sheet, r, tab, columns);
        if (string.IsNullOrWhiteSpace(rawGamertag) || TotalsRow().IsMatch(rawGamertag) || IsRepeatingHeader(rawGamertag, teamName))
        {
            currentTeam = null;
            return null;
        }
        var (gamertag, tradeNote, roleNote) = ArchiveCells.Gamertag(rawGamertag);
        if (gamertag.Length == 0)
        {
            currentTeam = null;
            return null;
        }
        if (tab.PerGame && !HasAnyGameStat(row, columns))
            return null;

        var team = ResolveRowTeam(teamName, sides, tab.CarryTeam, gamertag, rosters, ref currentTeam);
        var stint = stints.GetValueOrDefault(gamertag) + 1;
        stints[gamertag] = stint;
        return Player(row, columns, gamertag, team, tradeNote, roleNote, stint);
    }

    private static string? ResolveRowTeam(string? teamName, bool sides, bool carryTeam, string gamertag,
        Dictionary<string, string> rosters, ref string? currentTeam)
    {
        var explicitTeam = sides || HistoricalKeys.IsExcludedTeamName(teamName) ? null : teamName?.Trim();
        if (carryTeam)
        {
            if (!string.IsNullOrWhiteSpace(teamName) && HistoricalKeys.IsExcludedTeamName(teamName))
                currentTeam = null;
            else if (!string.IsNullOrWhiteSpace(explicitTeam))
                currentTeam = explicitTeam;
            explicitTeam ??= currentTeam;
        }
        return explicitTeam ?? rosters.GetValueOrDefault(HistoricalKeys.Normalize(gamertag));
    }

    private static bool HasAnyGameStat(string[] row, Dictionary<int, (string Key, string Header)> columns)
    {
        foreach (var (c, column) in columns)
        {
            if (c < row.Length && column.Key is not ("gamertag" or "teamName" or "gamesPlayed") && ArchiveCells.Number(row[c]) is not null)
                return true;
        }
        return false;
    }

    private sealed record SideBySideBlock(int NameCol, bool Won, string Opponent, Dictionary<int, (string Key, string Header)> Cols);

    private static List<ArchivePlayer> SideBySideGamePlayers(SheetTable sheet, int startRow, Dictionary<string, string> teams)
    {
        var r = startRow + 1;
        while (r < sheet.Rows.Count && ParseSideBySideHeader(sheet.Rows[r]).Count == 0)
            r++;

        var teamSections = ReadSideBySideSections(sheet, ref r);
        var result = new List<ArchivePlayer>();
        for (var s = 0; s < teamSections.Count; s++)
        {
            var inferredTeam = InferOpposingTeamName(teamSections, s);
            var (headers, rows) = teamSections[s];
            foreach (var hdr in headers)
                AddSideBySideBlockPlayers(hdr, rows, inferredTeam, teams, result);
        }
        return result;
    }

    private static List<(List<SideBySideBlock> Headers, List<string[]> Rows)> ReadSideBySideSections(SheetTable sheet, ref int r)
    {
        var sections = new List<(List<SideBySideBlock> Headers, List<string[]> Rows)>();
        while (r < sheet.Rows.Count)
        {
            var hdrs = ParseSideBySideHeader(sheet.Rows[r]);
            if (hdrs.Count == 0)
                break;
            r++;
            sections.Add((hdrs, ReadSideBySideSectionRows(sheet, hdrs, ref r)));
        }
        return sections;
    }

    private static List<string[]> ReadSideBySideSectionRows(SheetTable sheet, List<SideBySideBlock> hdrs, ref int r)
    {
        var rows = new List<string[]>();
        while (r < sheet.Rows.Count)
        {
            var row = sheet.Rows[r];
            if (ParseSideBySideHeader(row).Count > 0)
                break;
            if (row.All(string.IsNullOrWhiteSpace))
            {
                r++;
                continue;
            }
            if (hdrs.All(h => h.NameCol >= row.Length || string.IsNullOrWhiteSpace(row[h.NameCol])))
                break;
            rows.Add(row);
            r++;
        }
        return rows;
    }

    private static string? InferOpposingTeamName(List<(List<SideBySideBlock> Headers, List<string[]> Rows)> sections, int index)
    {
        if (sections.Count != 2)
            return null;
        return sections[1 - index].Headers
            .Select(h => h.Opponent)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .OrderByDescending(o => o.Length)
            .FirstOrDefault();
    }

    private static void AddSideBySideBlockPlayers(SideBySideBlock hdr, List<string[]> rows, string? inferredTeam,
        Dictionary<string, string> teams, List<ArchivePlayer> result)
    {
        foreach (var row in rows)
        {
            if (hdr.NameCol >= row.Length || string.IsNullOrWhiteSpace(row[hdr.NameCol]) || !HasAnyGameStat(row, hdr.Cols))
                continue;
            var (gamertag, tradeNote, roleNote) = ArchiveCells.Gamertag(row[hdr.NameCol]);
            if (gamertag.Length == 0)
                continue;
            var team = teams.GetValueOrDefault(HistoricalKeys.Normalize(gamertag)) ?? inferredTeam;
            var player = Player(row, hdr.Cols, gamertag, team, tradeNote, roleNote, 1);
            result.Add(WithGameOutcome(player, hdr.Won));
        }
    }

    private static ArchivePlayer WithGameOutcome(ArchivePlayer player, bool won) => new()
    {
        Gamertag = player.Gamertag,
        TeamName = player.TeamName,
        TradeNote = player.TradeNote,
        RoleNote = player.RoleNote,
        StintIndex = 1,
        GamesPlayed = 1,
        Wins = won ? 1 : 0,
        Losses = won ? 0 : 1,
        Goals = player.Goals,
        Kills = player.Kills,
        Deaths = player.Deaths,
        Assists = player.Assists,
        CarrierKills = player.CarrierKills,
        AverageLifeSeconds = player.AverageLifeSeconds,
        TotalSecondsPlayed = player.TotalSecondsPlayed,
        Sprees = player.Sprees,
        MultiKills = player.MultiKills,
        Exterminations = player.Exterminations,
        Betrayals = player.Betrayals,
        HammerKills = player.HammerKills,
        SwordKills = player.SwordKills,
        BallKills = player.BallKills,
        Punches = player.Punches,
        Score = player.Score,
        DamageDealt = player.DamageDealt,
        DamageTaken = player.DamageTaken,
    };

    private static List<SideBySideBlock> ParseSideBySideHeader(string[] row)
    {
        var starts = new List<(int Col, bool Won, string Opponent)>();
        for (var c = 0; c < row.Length; c++)
        {
            var match = SideBySideTeamHeader().Match(row[c].Trim());
            if (match.Success)
                starts.Add((c, match.Groups[1].Value.Equals("W", StringComparison.OrdinalIgnoreCase), match.Groups[2].Value.Trim()));
        }
        if (starts.Count == 0)
            return [];

        var result = new List<SideBySideBlock>();
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1].Col : row.Length;
            var cols = ColumnsOfRange(row, starts[i].Col + 1, end);
            if (cols.Count > 0)
                result.Add(new SideBySideBlock(starts[i].Col, starts[i].Won, starts[i].Opponent, cols));
        }
        return result;
    }

    /// <summary>The stats the columns from <paramref name="startCol"/> up to <paramref name="endCol"/> of a header hold, by
    /// column index.</summary>
    private static Dictionary<int, (string Key, string Header)> ColumnsOfRange(IReadOnlyList<string> row, int startCol, int endCol)
    {
        var cols = new Dictionary<int, (string Key, string Header)>();
        for (var c = startCol; c < endCol; c++)
        {
            var text = row[c].Trim();
            // A pivot table's column is its stat's ("SUM of Games Played"), and a database's header has underscores
            var lower = text.ToLowerInvariant().Replace('_', ' ');
            if (Columns.TryGetValue(lower.StartsWith("sum of ") ? lower[7..].Trim() : lower, out var key))
                cols[c] = (key, text);
        }
        return cols;
    }

    private static List<ArchivePlayer> AggregatePerGame(List<ArchivePlayer> gameRows)
    {
        static decimal? SumOpt(IEnumerable<decimal?> values)
        {
            var list = values.OfType<decimal>().ToList();
            return list.Count == 0 ? null : list.Sum();
        }

        var stints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var aggregated = new List<ArchivePlayer>();
        foreach (var group in gameRows.GroupBy(p => (Key: HistoricalKeys.Normalize(p.Gamertag), Team: (p.TeamName ?? "").ToLowerInvariant())))
        {
            var first = group.First();
            var stint = stints.GetValueOrDefault(group.Key.Key) + 1;
            stints[group.Key.Key] = stint;
            var games = group.Count();
            var wins = group.Sum(p => p.Wins ?? 0);
            var losses = group.Sum(p => p.Losses ?? 0);
            var kills = group.Sum(p => p.Kills ?? 0);
            var deaths = group.Sum(p => p.Deaths ?? 0);
            var kdRatio = deaths > 0 ? Math.Round(kills / deaths, 3, MidpointRounding.AwayFromZero) : (decimal?)null;
            aggregated.Add(new ArchivePlayer
            {
                Gamertag = first.Gamertag,
                TeamName = first.TeamName,
                TradeNote = first.TradeNote,
                RoleNote = first.RoleNote,
                StintIndex = stint,
                GamesPlayed = games,
                Wins = wins,
                Losses = losses,
                WinPercent = Math.Round(wins * 100m / games, 2, MidpointRounding.AwayFromZero),
                Goals = group.Sum(p => p.Goals ?? 0),
                Kills = kills,
                Deaths = deaths,
                Assists = group.Sum(p => p.Assists ?? 0),
                KdRatio = kdRatio,
                KdSpread = kills - deaths,
                CarrierKills = group.Sum(p => p.CarrierKills ?? 0),
                TotalSecondsPlayed = SumOpt(group.Select(p => p.TotalSecondsPlayed)),
                Sprees = group.Sum(p => p.Sprees ?? 0),
                MultiKills = group.Sum(p => p.MultiKills ?? 0),
                Exterminations = group.Sum(p => p.Exterminations ?? 0),
                Betrayals = group.Sum(p => p.Betrayals ?? 0),
                HammerKills = SumOpt(group.Select(p => p.HammerKills)),
                SwordKills = SumOpt(group.Select(p => p.SwordKills)),
                BallKills = SumOpt(group.Select(p => p.BallKills)),
                Punches = SumOpt(group.Select(p => p.Punches)),
                Score = SumOpt(group.Select(p => p.Score)),
                DamageDealt = SumOpt(group.Select(p => p.DamageDealt)),
                DamageTaken = SumOpt(group.Select(p => p.DamageTaken)),
            });
        }
        return aggregated;
    }

    [GeneratedRegex(@"^(W|L)\s+vs\s+(.+?)\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex SideBySideTeamHeader();

    /// <summary>A row's gamertag and team cells: the headed columns', else where the config says (a column without a header,
    /// its first cell blank).</summary>
    private static (string? Gamertag, string? TeamName) NameCells(SheetTable sheet, int r, StatsTab tab, Dictionary<int, (string Key, string Header)> columns)
    {
        var row = sheet.Rows[r];
        string? rawGamertag = null, teamName = null;
        foreach (var (c, column) in columns)
        {
            if (c >= row.Length)
                continue;
            if (column.Key == "gamertag")
                rawGamertag = row[c];
            else if (column.Key == "teamName")
                teamName = row[c];
        }
        if (string.IsNullOrEmpty(rawGamertag) && tab.PlayerColumn is int playerColumn)
            rawGamertag = sheet.Cell(r, playerColumn);
        if (string.IsNullOrWhiteSpace(teamName) && tab.TeamColumn is int teamColumn)
            teamName = sheet.Cell(r, teamColumn);
        return (rawGamertag, teamName);
    }

    /// <summary>The stats a header's columns hold, by column index (a later column of the same stat wins, as the archive's
    /// compiler read them).</summary>
    internal static Dictionary<int, (string Key, string Header)> ColumnsOf(IReadOnlyList<string> header) => ColumnsOfRange(header, 0, header.Count);

    /// <summary>A player's line from a row of the stat columns.</summary>
    internal static ArchivePlayer Player(string[] row, Dictionary<int, (string Key, string Header)> columns, string gamertag, string? teamName, string? tradeNote, string? roleNote, int stint, System.Text.Json.JsonElement? extendedMetrics = null)
    {
        var values = new Dictionary<string, decimal?>();
        foreach (var (c, column) in columns)
        {
            if (c >= row.Length || row[c].Length == 0 || column.Key is "gamertag" or "teamName")
                continue;
            if (column.Key == "averageLifeSeconds")
                values[column.Key] = ArchiveCells.AverageLifeSeconds(row[c]);
            else if (column.Key == "totalSecondsPlayed")
                values[column.Key] = ArchiveCells.DurationSeconds(row[c], column.Header);
            else if (ArchiveCells.Number(row[c]) is decimal number)
                values[column.Key] = number;
        }
        // Counts default to 0, the rest to unknown, as the archive's compiler wrote them
        decimal? Count(string key) => values.GetValueOrDefault(key) ?? 0;
        decimal? Value(string key) => values.GetValueOrDefault(key);
        var kills = Count("kills");
        var deaths = Count("deaths");
        var kdRatio = Value("kdRatio") ?? (deaths > 0 ? Math.Round(kills!.Value / deaths!.Value, 3, MidpointRounding.AwayFromZero) : (decimal?)null);
        return new ArchivePlayer
        {
            Gamertag = gamertag,
            TeamName = teamName,
            TradeNote = tradeNote,
            RoleNote = roleNote,
            StintIndex = stint,
            GamesPlayed = Count("gamesPlayed"),
            Wins = Count("wins"),
            Losses = Count("losses"),
            WinPercent = Value("winPercent"),
            Goals = Count("goals"),
            Kills = kills,
            Deaths = deaths,
            Assists = Count("assists"),
            KdRatio = kdRatio,
            KdSpread = Value("kdSpread") ?? kills - deaths,
            CarrierKills = Count("carrierKills"),
            AverageLifeSeconds = Value("averageLifeSeconds"),
            TotalSecondsPlayed = Value("totalSecondsPlayed"),
            Sprees = Count("sprees"),
            MultiKills = Count("multiKills"),
            Exterminations = Count("exterminations"),
            Betrayals = Count("betrayals"),
            HammerKills = Value("hammerKills"),
            SwordKills = Value("swordKills"),
            BallKills = Value("ballKills"),
            Punches = Value("punches"),
            Score = Value("score"),
            DamageDealt = Value("damageDealt"),
            DamageTaken = Value("damageTaken"),
            ExtendedMetrics = extendedMetrics,
        };
    }

    /// <summary>A TimeStamp cell: the stats tool's "Tue Oct 18 08:01:46 2022" (UTC), or a date the sheet shows; null for
    /// anything else or a year no Grifball season was played in.</summary>
    public static DateTime? PlayedAt(string? cell)
    {
        var text = string.Join(' ', (cell ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0)
            return null;
        var styles = System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal;
        DateTime at;
        if (!DateTime.TryParseExact(text, "ddd MMM d HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture, styles, out at)
            && !DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, styles, out at))
            return null;
        return at.Year is >= 2007 and <= 2100 ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null;
    }

    [GeneratedRegex("total|average|subtotal", RegexOptions.IgnoreCase)]
    private static partial Regex TotalsRow();

    private static bool IsRepeatingHeader(string rawGamertag, string? teamName)
    {
        var cleanTag = rawGamertag.Trim();
        var isTagHeader = cleanTag.Equals("Player", StringComparison.OrdinalIgnoreCase)
            || cleanTag.Equals("Gamertag", StringComparison.OrdinalIgnoreCase)
            || cleanTag.Equals("Players", StringComparison.OrdinalIgnoreCase)
            || cleanTag.Equals("Name", StringComparison.OrdinalIgnoreCase)
            || cleanTag.Equals("Player Name", StringComparison.OrdinalIgnoreCase);
        return isTagHeader && (string.IsNullOrWhiteSpace(teamName) || HistoricalKeys.IsExcludedTeamName(teamName));
    }
}
