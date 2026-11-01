using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.Historical.Sources;

/// <summary>
/// What the import reads from one source (stored as JSON on its row, HistoricalSource.Config): the seasons its stats tabs
/// hold (what seasons-config.json said), the Waypoint game lists of Halo Infinite seasons, the per-game (carnage) tabs, the
/// champions record and the players list.
/// </summary>
public class HistoricalSourceConfig
{
    /// <summary>The workbook's seasons: their name, league, game and division, unless a tab says otherwise.</summary>
    public SeasonDefaults? Season { get; init; }
    /// <summary>Season stats tabs: one archive entry each (a season's regular season, its playoffs).</summary>
    public List<StatsTab> Tabs { get; init; } = [];
    /// <summary>Database tables of stats with a season column (e.g. the grifball.info backup): an archive entry per season,
    /// division and phase.</summary>
    public List<StatsTable> Tables { get; init; } = [];
    /// <summary>Tabs listing a Halo Infinite season's Waypoint games.</summary>
    public List<WaypointTab> Waypoint { get; init; } = [];
    /// <summary>Tabs of per-game stats (carnage reports): the season's recorded games.</summary>
    public List<GamesTab> Games { get; init; } = [];
    /// <summary>The tab of the AGLA champions record.</summary>
    public string? ChampionsSheet { get; init; }
    /// <summary>The tab of a players list (gamertag, Discord, Discord ID, Location, System).</summary>
    public string? PlayersSheet { get; init; }
    /// <summary>Tabs of the teams' rosters (a "Team Name" and "Player Name" table, or a draft sheet: a "Team Name" row with
    /// a team per column and its players below), for stats tabs whose Team column is only a game side (0 or 1) or missing.</summary>
    public List<string> RosterSheets { get; init; } = [];
    /// <summary>Rosters (as <see cref="RosterSheets"/>) in this workbook of a season whose stats are in another source's, e.g. a
    /// draft sheet beside the schedule: they fill the team of that season's players whose stats name none.</summary>
    public List<RosterTab> Rosters { get; init; } = [];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The tabs the import reads from the source (trimmed, any case): the rest of its workbook isn't read.</summary>
    public HashSet<string> SheetNames()
    {
        IEnumerable<string?> names =
        [
            .. Tabs.Select(t => t.Name),
            .. Tabs.Select(t => t.DatesSheet),
            .. Tables.SelectMany(t => new[] { t.Sheet, t.Players?.Sheet, t.Teams?.Sheet }),
            .. Waypoint.Select(t => t.Sheet),
            .. Games.Select(t => t.Sheet),
            .. RosterSheets,
            .. Rosters.Select(t => t.Sheet),
            ChampionsSheet,
            PlayersSheet,
        ];
        return names.OfType<string>().Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static HistoricalSourceConfig? FromJson(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<HistoricalSourceConfig>(json, JsonOptions);
}

public class SeasonDefaults
{
    public required string Name { get; init; }
    /// <summary>The league's name or slug; AGLA when not given.</summary>
    public string? League { get; init; }
    /// <summary>A GameVersion slug, or "multi" when each tab names its own.</summary>
    public string? Game { get; init; }
    /// <summary>Open when not given.</summary>
    public string? Division { get; init; }
    /// <summary>A SeasonFormat name; otherwise it follows from the league (HistoricalKeys.Format).</summary>
    public string? Format { get; init; }
}

public class StatsTab
{
    public required string Name { get; init; }
    /// <summary>The header's row among the tab's non-empty rows (0-based).</summary>
    public int HeaderRow { get; init; }
    public bool IsPlayoffs { get; init; }
    public bool IsCombined { get; init; }
    /// <summary>A season of its own in the workbook (e.g. a Fall League tab in a Summer League workbook).</summary>
    public string? CustomSeason { get; init; }
    public string? Game { get; init; }
    /// <summary>Columns to read the player and team from when the header doesn't label them.</summary>
    public int? PlayerColumn { get; init; }
    public int? TeamColumn { get; init; }
    /// <summary>A tab whose Timestamp column dates this season, when this tab has none (e.g. a Late League match history).</summary>
    public string? DatesSheet { get; init; }
    /// <summary>The season's year when nothing in the sheets dates it, nor its name: the source's year otherwise.</summary>
    public int? Year { get; init; }
}

/// <summary>
/// A database table of player stats (a database export, e.g. the grifball.info backup): its rows are of many seasons, a
/// season column says which, and a row is a player's line (or one game of it, <see cref="PerGame"/>). An entry per season,
/// division and phase, keyed "{tab}#s{season}[_{division}]". The stat columns are read as a season stats tab's
/// (SeasonStatsParser).
/// </summary>
public class StatsTable
{
    public required string Sheet { get; init; }
    /// <summary>The GameVersion slug of every row.</summary>
    public required string Game { get; init; }
    public string SeasonColumn { get; init; } = "season";
    /// <summary>A column that is 1 on a playoff row; without it every row is the regular season's.</summary>
    public string? PlayoffsColumn { get; init; }
    /// <summary>Each row is one game of a player's: a player's rows of a season and team are added up (their games counted).</summary>
    public bool PerGame { get; init; }
    /// <summary>The gamertag from another tab, when the table has only the player's ID.</summary>
    public TableLookup? Players { get; init; }
    /// <summary>The team's name from another tab, by the team's ID.</summary>
    public TableLookup? Teams { get; init; }
    /// <summary>A column of the teams' tab whose value is the team's division in a season that lists divisions
    /// (<see cref="TableSeason.Divisions"/>), e.g. "ispro".</summary>
    public string? DivisionColumn { get; init; }
    /// <summary>Extra columns kept with a player's line (ExtendedMetrics): column to metric name, e.g. batavg to
    /// battingAverage. A metric whose name ends in "Id" is kept as text, the rest as numbers.</summary>
    public Dictionary<string, string> Extended { get; init; } = [];
    /// <summary>The seasons by their ID in the season column. A row of any other season is skipped, and reported.</summary>
    public Dictionary<string, TableSeason> Seasons { get; init; } = [];
}

/// <summary>Another tab's column by an ID: <see cref="Column"/> of the <see cref="Sheet"/> row whose <see cref="Key"/> is the
/// stats row's <see cref="RowKey"/> (the same column name when not given).</summary>
public class TableLookup
{
    public required string Sheet { get; init; }
    public required string Key { get; init; }
    public required string Column { get; init; }
    public string? RowKey { get; init; }
}

public class TableSeason
{
    public required string Name { get; init; }
    /// <summary>The season's divisions by the value of the table's DivisionColumn ("1": "Pro", "0": "Amateur"): a season
    /// each, named "{Name} ({division})". Without them the season is one, Open.</summary>
    public Dictionary<string, string>? Divisions { get; init; }
}

public class RosterTab
{
    public required string Sheet { get; init; }
    /// <summary>The source ID of the season's stats workbook.</summary>
    public required string Season { get; init; }
}

public class WaypointTab
{
    public required string Sheet { get; init; }
    /// <summary>The source ID of the season's stats workbook: its regular season's key is what the matches go under.</summary>
    public required string Season { get; init; }
}

public enum GamesLayout
{
    /// <summary>One row per player per game (Player, Team, ..., TimeStamp): a game is the rows sharing a timestamp (or,
    /// without one, a run of rows of one game time).</summary>
    Carnage = 1,
    /// <summary>A title row per game ("Week 1 - 1/28/19 - A over B (5-2)"), its players below, a side per team name.</summary>
    Blocks = 2,
}

public class GamesTab
{
    public required string Sheet { get; init; }
    /// <summary>The season's key ("{workbook}/{stats tab}") the games belong to.</summary>
    public required string Season { get; init; }
    /// <summary>The games' GameVersion slug, unless a row's Game column says.</summary>
    public required string Game { get; init; }
    public GamesLayout Layout { get; init; } = GamesLayout.Carnage;
    /// <summary>Late League games are scrims: in no series.</summary>
    public bool Scrims { get; init; }
}
