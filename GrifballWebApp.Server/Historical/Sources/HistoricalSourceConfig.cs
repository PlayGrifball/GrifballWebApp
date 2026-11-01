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
    /// <summary>Tabs listing a Halo Infinite season's Waypoint games.</summary>
    public List<WaypointTab> Waypoint { get; init; } = [];
    /// <summary>Tabs of per-game stats (carnage reports): the season's recorded games.</summary>
    public List<GamesTab> Games { get; init; } = [];
    /// <summary>The tab of the AGLA champions record.</summary>
    public string? ChampionsSheet { get; init; }
    /// <summary>The tab of a players list (gamertag, Discord, Discord ID, Location, System).</summary>
    public string? PlayersSheet { get; init; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter() },
    };

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
    /// <summary>A title row per game ("Week 1 - 1/28/19 - A over B (5-2)"), its players below, sides split by "Totals:" rows.</summary>
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
