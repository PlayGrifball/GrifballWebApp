using GrifballWebApp.Server.Historical.Sources;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Historical.Parsing;

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
        ["goals"] = "goals", ["kills"] = "kills", ["assists"] = "assists", ["deaths"] = "deaths",
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
        // The teams' rosters, for players whose row names no team (or only the side of a game)
        var rosters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in config.RosterSheets)
        {
            if (workbook.Sheet(name) is { } rosterSheet)
                foreach (var (player, team) in RosterParser.Parse(rosterSheet))
                    rosters.TryAdd(player, team);
            else
                warnings.Add($"{sourceId}: no sheet '{name}'");
        }
        foreach (var tab in config.Tabs)
        {
            var sheet = workbook.Sheet(tab.Name);
            if (sheet is null || sheet.Rows.Count <= tab.HeaderRow + 1)
            {
                warnings.Add($"{sourceId}: no sheet '{tab.Name}', or it is empty");
                continue;
            }
            var columns = ColumnsOf(sheet.Rows[tab.HeaderRow]);
            var dataRows = sheet.Rows.Skip(tab.HeaderRow + 1).ToList();
            // A Team column of only 0s and 1s is the side of the player's last game (the stats tool's sheets), not a team
            var teamColumns = columns.Where(c => c.Value.Key == "teamName").Select(c => c.Key).ToList();
            var sides = teamColumns.Count > 0 && dataRows.Select(r => teamColumns.Select(c => c < r.Length ? r[c].Trim() : "").LastOrDefault(v => v.Length > 0))
                .OfType<string>().Where(v => v.Length > 0).DefaultIfEmpty("x").All(v => v is "0" or "1");
            // When its games were played: the stats tool's TimeStamp column (each player's last game)
            var stampColumn = Array.FindIndex(sheet.Rows[tab.HeaderRow], h => h.Trim().ToLowerInvariant() is "timestamp" or "time stamp");
            var played = stampColumn < 0 ? [] : dataRows.Select(r => PlayedAt(stampColumn < r.Length ? r[stampColumn] : null)).OfType<DateTime>().ToList();

            var players = new List<ArchivePlayer>();
            var stints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var r = tab.HeaderRow + 1; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                if (row.All(string.IsNullOrWhiteSpace))
                    continue;
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
                {
                    rawGamertag = sheet.Cell(r, playerColumn);
                    teamName = tab.TeamColumn is int teamColumn ? sheet.Cell(r, teamColumn) : teamName;
                }
                if (string.IsNullOrWhiteSpace(rawGamertag) || TotalsRow().IsMatch(rawGamertag))
                    continue;
                var (gamertag, tradeNote, roleNote) = ArchiveCells.Gamertag(rawGamertag);
                if (gamertag.Length == 0)
                    continue;
                var stint = stints.GetValueOrDefault(gamertag) + 1;
                stints[gamertag] = stint;
                var team = sides || string.IsNullOrWhiteSpace(teamName) ? null : teamName.Trim();
                team ??= rosters.GetValueOrDefault(HistoricalKeys.Normalize(gamertag));
                players.Add(Player(row, columns, gamertag, team, tradeNote, roleNote, stint));
            }

            var defaults = config.Season;
            seasons.Add(new ArchiveSeason
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
                Players = players,
            });
        }
        return seasons;
    }

    /// <summary>The stats a header's columns hold, by column index (a later column of the same stat wins, as the archive's
    /// compiler read them).</summary>
    internal static Dictionary<int, (string Key, string Header)> ColumnsOf(IReadOnlyList<string> header)
    {
        var columns = new Dictionary<int, (string Key, string Header)>();
        for (var c = 0; c < header.Count; c++)
        {
            var text = header[c].Trim();
            if (Columns.TryGetValue(text.ToLowerInvariant(), out var key))
                columns[c] = (key, text);
        }
        return columns;
    }

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
}
