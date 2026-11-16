using System.Text.Json;
using System.Text.Json.Serialization;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Historical.Import;

/// <summary>
/// What the historical import reads: the compiled archive (not kept in the repository; the tests use a few of its
/// seasons, GrifballWebApp.Integration.Test/Fixtures/HistoricalArchive)
/// (unified-historical-seasons.json, agla-champions.json) and, optionally, the Waypoint matches of Halo Infinite
/// seasons. Property names are the archive's (camelCase).
/// </summary>
public class HistoricalArchiveInput
{
    public required UnifiedArchive Seasons { get; init; }
    public ChampionsArchive? Championships { get; init; }
    /// <summary>catalog.json: each workbook's Google Sheets link, stored on its seasons (HistoricalSourceUrl).</summary>
    public ArchiveCatalog? Catalog { get; init; }
    /// <summary>Halo Infinite seasons' Waypoint matches, by the season's <c>HistoricalSourceID</c> ("{sourceWorkbook}/{sourceSheet}").</summary>
    public Dictionary<string, WaypointSeasonMatches>? WaypointMatches { get; init; }
    /// <summary>Games recorded outside Waypoint (MCC carnage reports, older games), each naming its season by the season's
    /// <c>HistoricalSourceID</c>. Filled by the spreadsheet parser.</summary>
    public List<RecordedGameInput>? RecordedGames { get; init; }
    /// <summary>Players lists (the Late League master list and the like): a gamertag's Discord ID, region and system.
    /// Filled by the spreadsheet parser, or from late-league-master.json (<see cref="LateLeagueMasterFile"/>).</summary>
    public List<ArchivePlayerInput>? Players { get; init; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static async Task<T> ReadAsync<T>(Stream json, CancellationToken ct = default)
        => await JsonSerializer.DeserializeAsync<T>(json, JsonOptions, ct)
            ?? throw new InvalidDataException($"The {typeof(T).Name} file is empty");
}

/// <summary>unified-historical-seasons.json: one entry per season and phase (a season's regular season and its playoffs
/// are two entries with the same league and name).</summary>
public class UnifiedArchive
{
    public List<ArchiveSeason> Seasons { get; init; } = [];
}

public class ArchiveSeason
{
    public required string SeasonName { get; init; }
    /// <summary>A GameVersion slug: halo3, reach, reach-dash, halo4, halo5, infinite.</summary>
    public required string GameVersion { get; init; }
    /// <summary>The league's name or slug, e.g. AGLA, Late League.</summary>
    public required string League { get; init; }
    public string? Division { get; init; }
    public string? SourceWorkbook { get; init; }
    public string? SourceSheet { get; init; }
    public bool IsPlayoffs { get; init; }
    public bool IsCombined { get; init; }
    /// <summary>The season's format (LeagueSeason, Tournament) when the archive says; otherwise it follows from the league and
    /// workbook (<see cref="HistoricalKeys.Format"/>). LateLeague makes it a matchmaking season (<see cref="HistoricalKeys.IsLateLeague"/>).</summary>
    public string? Format { get; init; }
    /// <summary>Overrides the derived key ("{sourceWorkbook}/{sourceSheet}") when the archive sets one.</summary>
    public string? HistoricalSourceId { get; init; }
    /// <summary>The champions record's name for the season's titles when it isn't <see cref="SeasonName"/>.</summary>
    public string? Titles { get; init; }
    /// <summary>When the entry's games were played, as far as its sheet says (a stats tab's TimeStamp column): the season's
    /// dates when its games don't give them.</summary>
    public DateTime? FirstPlayedAt { get; init; }
    public DateTime? LastPlayedAt { get; init; }
    /// <summary>The year its source is filed under (the catalog's), for a season whose name and sheet give no date.</summary>
    public int? SourceYear { get; set; }
    public List<ArchivePlayer> Players { get; init; } = [];
    /// <summary>The season's teams, their records, captains and rosters when a standings table lists them (e.g. the
    /// grifball.com archive), including teams with no captured player stat rows.</summary>
    public List<ArchiveTeam> Teams { get; init; } = [];
}

/// <summary>A team of a season's standings. A record: a copy under another name (`with`) keeps every other value.</summary>
public record ArchiveTeam
{
    public required string TeamName { get; init; }
    public string? Division { get; init; }
    /// <summary>Its place in the regular season's standings (its seed).</summary>
    public int? Rank { get; init; }
    /// <summary>Its final place: 1 for the champion, the round it went out in for the others.</summary>
    public int? Finish { get; init; }
    public int? Wins { get; init; }
    public int? Losses { get; init; }
    public int? GoalsFor { get; init; }
    public int? GoalsAgainst { get; init; }
    public string? Captain { get; init; }
    public List<string> Roster { get; init; } = [];
}

public class ArchivePlayer
{
    public required string Gamertag { get; init; }
    public string? TeamName { get; set; }
    public string? TradeNote { get; init; }
    public string? RoleNote { get; init; }
    public int StintIndex { get; init; } = 1;
    public decimal? GamesPlayed { get; init; }
    public decimal? Wins { get; init; }
    public decimal? Losses { get; init; }
    public decimal? WinPercent { get; init; }
    public decimal? Goals { get; init; }
    public decimal? Kills { get; init; }
    public decimal? Deaths { get; init; }
    public decimal? Assists { get; init; }
    public decimal? KdRatio { get; init; }
    public decimal? KdSpread { get; init; }
    public decimal? CarrierKills { get; init; }
    public decimal? AverageLifeSeconds { get; init; }
    public decimal? TotalSecondsPlayed { get; init; }
    public decimal? Sprees { get; init; }
    public decimal? MultiKills { get; init; }
    public decimal? Exterminations { get; init; }
    public decimal? Betrayals { get; init; }
    public decimal? HammerKills { get; init; }
    public decimal? SwordKills { get; init; }
    public decimal? BallKills { get; init; }
    public decimal? Punches { get; init; }
    public decimal? Score { get; init; }
    public decimal? DamageDealt { get; init; }
    public decimal? DamageTaken { get; init; }
    public JsonElement? ExtendedMetrics { get; init; }
}

/// <summary>agla-champions.json: every AGLA title and its roster.</summary>
public class ChampionsArchive
{
    /// <summary>The workbook's ID, e.g. history-agla-champions: the key prefix of a season only a title names.</summary>
    public string? SourceId { get; init; }
    public string? SourceUrl { get; init; }
    public List<ArchiveChampionship> Champions { get; init; } = [];
}

public class ArchiveChampionship
{
    /// <summary>The season's name, e.g. "Summer League 2021".</summary>
    public required string Season { get; init; }
    /// <summary>The game as the record writes it, e.g. "MCC Halo 3", "Infinite".</summary>
    public string? Game { get; init; }
    /// <summary>The tier (division) the title was won in: Pro, Amateur, Open, ...</summary>
    [JsonPropertyName("league")]
    public required string Tier { get; init; }
    public required string Team { get; init; }
    public string? Captain { get; init; }
    public List<string> Roster { get; init; } = [];
}

/// <summary>catalog.json: every source workbook of the archive.</summary>
public class ArchiveCatalog
{
    public List<CatalogSource> Sources { get; init; } = [];
}

public class CatalogSource
{
    public required string Id { get; init; }
    public string? Url { get; init; }
}

/// <summary>A Halo Infinite season's Waypoint matches: match IDs or Waypoint match URLs (the ID is the URL's last segment).</summary>
public class WaypointSeasonMatches
{
    public List<string> Regular { get; init; } = [];
    public List<string> Playoffs { get; init; } = [];
    /// <summary>The games whose schedule row names both sides (its Home and Away columns, which hold team or captain
    /// names): the import puts each player on the name that appears in their games.</summary>
    public List<ScheduledGame> Schedule { get; init; } = [];
}

/// <summary>A schedule row's game: its match ID and the names in the row's Home and Away cells.</summary>
public class ScheduledGame
{
    public required string MatchId { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
}

/// <summary>
/// A game recorded outside Waypoint, e.g. one MCC carnage report. Sides are as the source numbers them (1, 2); the import
/// maps each to a season team by its players' rosters.
/// </summary>
public class RecordedGameInput
{
    /// <summary>The season's <c>HistoricalSourceID</c> ("{sourceWorkbook}/{sourceSheet}"), as <see cref="HistoricalArchiveInput.WaypointMatches"/> is keyed.</summary>
    public required string SeasonSourceId { get; set; }
    /// <summary>Unique per game (e.g. "{workbook}/{sheet}#{row}"): what a re-import finds the game by.</summary>
    public required string SourceKey { get; init; }
    /// <summary>A GameVersion slug: halo3, reach, reach-dash, halo4, halo5, infinite.</summary>
    public required string GameVersion { get; set; }
    public DateTime? PlayedAt { get; init; }
    public string? Map { get; init; }
    public string? Gametype { get; init; }
    public bool IsPlayoffs { get; init; }
    /// <summary>Not part of a series: it still counts in the season's stats. A Late League game is in none either way (it is a
    /// matchmaking game).</summary>
    public bool IsScrim { get; init; }
    /// <summary>The game's Waypoint match ID or URL, when it was a Halo Infinite match: the Waypoint match wins.</summary>
    public string? WaypointMatchId { get; init; }
    /// <summary>The series the game is in, when its source says (a result list's rows of one series): the games with the same
    /// key are one series, whenever they were played. Without one, a series is the same two teams' games close in time.</summary>
    public string? Series { get; init; }
    /// <summary>A link to a video of the game, as its source gives it.</summary>
    public string? Vod { get; init; }
    /// <summary>Who hosted the game.</summary>
    public string? Host { get; init; }
    /// <summary>Forfeited, not played: no players, and its sides' outcomes say who forfeited (Lost), or both did.</summary>
    public bool IsForfeit { get; init; }
    public List<RecordedTeamInput> Teams { get; init; } = [];
    public List<RecordedPlayerInput> Players { get; init; } = [];
}

public class RecordedTeamInput
{
    public int Side { get; init; }
    public string? Name { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<GrifballWebApp.Domain.Halo.Outcomes>))]
    public GrifballWebApp.Domain.Halo.Outcomes Outcome { get; init; }
    public int? Score { get; init; }
}

public class RecordedPlayerInput
{
    public required string Gamertag { get; init; }
    public int Side { get; init; }
    public int Kills { get; init; }
    public int Deaths { get; init; }
    public int Assists { get; init; }
    public int? Goals { get; init; }
    public int? Score { get; init; }
    public int? CarrierKills { get; init; }
    public int? Betrayals { get; init; }
    public int? Suicides { get; init; }
    public int? HammerKills { get; init; }
    public int? SwordKills { get; init; }
    public int? BallKills { get; init; }
    public int? Punches { get; init; }
}

/// <summary>A player of a players list: who a gamertag is on Discord, where they play from and on what.</summary>
public class ArchivePlayerInput
{
    public required string Gamertag { get; init; }
    /// <summary>The Discord user ID (a snowflake), as text: JSON numbers that big lose digits.</summary>
    public string? DiscordId { get; init; }
    public string? DiscordUsername { get; init; }
    /// <summary>The sheet's location, e.g. "East", "Central", "Europe" (<see cref="GrifballWebApp.Application.Profile.Regions.Normalize"/>).</summary>
    public string? Region { get; init; }
    /// <summary>PC, XBOX, ...</summary>
    public string? System { get; init; }
    /// <summary>Where the row comes from, e.g. "late-league-aspence-main/Player Masterlist", for the report.</summary>
    public string? SourceKey { get; init; }

    /// <summary>The Discord ID, when it is one (digits only).</summary>
    [JsonIgnore]
    public long? DiscordUserId => long.TryParse(DiscordId?.Trim(), global::System.Globalization.NumberStyles.None, global::System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}

/// <summary>compiled/late-league-master.json: the Late League master list of players, among other sheets.</summary>
public class LateLeagueMasterFile
{
    public string? SourceId { get; init; }
    public List<LateLeagueMasterPlayer> PlayerMasterlist { get; init; } = [];

    public List<ArchivePlayerInput> ToPlayers() => PlayerMasterlist
        .Where(p => !string.IsNullOrWhiteSpace(p.Gamertag))
        .Select(p => new ArchivePlayerInput
        {
            Gamertag = p.Gamertag!,
            DiscordId = p.DiscordId,
            DiscordUsername = p.Discord,
            Region = p.Location,
            System = p.System,
            SourceKey = $"{SourceId ?? "late-league-master"}/Player Masterlist",
        })
        .ToList();
}

public class LateLeagueMasterPlayer
{
    public string? Gamertag { get; init; }
    /// <summary>The Discord username.</summary>
    public string? Discord { get; init; }
    [JsonPropertyName("Discord ID")]
    public string? DiscordId { get; init; }
    public string? Location { get; init; }
    public string? System { get; init; }
}
