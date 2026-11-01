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
        ["game time"] = "totalSecondsPlayed", ["gametime"] = "totalSecondsPlayed", ["timealive"] = "totalSecondsPlayed",
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
        foreach (var tab in config.Tabs)
        {
            var sheet = workbook.Sheet(tab.Name);
            if (sheet is null || sheet.Rows.Count <= tab.HeaderRow + 1)
            {
                warnings.Add($"{sourceId}: no sheet '{tab.Name}', or it is empty");
                continue;
            }
            var headers = sheet.Rows[tab.HeaderRow].Select(h => h.Trim()).ToArray();
            // A later column of the same stat wins, as the archive's compiler read them
            var columns = new Dictionary<int, (string Key, string Header)>();
            for (var c = 0; c < headers.Length; c++)
                if (Columns.TryGetValue(headers[c].ToLowerInvariant(), out var key))
                    columns[c] = (key, headers[c]);

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
                players.Add(Player(row, columns, gamertag, string.IsNullOrWhiteSpace(teamName) ? null : teamName.Trim(), tradeNote, roleNote, stint));
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
                Players = players,
            });
        }
        return seasons;
    }

    private static ArchivePlayer Player(string[] row, Dictionary<int, (string Key, string Header)> columns, string gamertag, string? teamName, string? tradeNote, string? roleNote, int stint)
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
        };
    }

    [GeneratedRegex("total|average|subtotal", RegexOptions.IgnoreCase)]
    private static partial Regex TotalsRow();
}
