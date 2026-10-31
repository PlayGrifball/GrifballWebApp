using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// What the historical import reads: the compiled archive in data/historical-archive/compiled
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

    internal static readonly JsonSerializerOptions JsonOptions = new()
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
    /// <summary>Overrides the derived key ("{sourceWorkbook}/{sourceSheet}") when the archive sets one.</summary>
    public string? HistoricalSourceId { get; init; }
    public List<ArchivePlayer> Players { get; init; } = [];
}

public class ArchivePlayer
{
    public required string Gamertag { get; init; }
    public string? TeamName { get; init; }
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
}

/// <summary>Gamertag and key helpers shared by the import.</summary>
public static partial class HistoricalKeys
{
    /// <summary>The prefix of a placeholder (dummy) user's UserName. Identity's allowed user name characters have no
    /// colon or space, so no account made by signing up can ever collide with one.</summary>
    public const string DummyUserNamePrefix = "historical:";

    /// <summary>Trimmed, inner whitespace collapsed to one space: how gamertags are stored on historical rows.</summary>
    public static string CleanGamertag(string gamertag) => Whitespace().Replace(gamertag.Trim(), " ");

    /// <summary><see cref="CleanGamertag"/>, upper-cased: what gamertags are compared by.</summary>
    public static string Normalize(string gamertag) => CleanGamertag(gamertag).ToUpperInvariant();

    public static string DummyUserName(string gamertag) => DummyUserNamePrefix + CleanGamertag(gamertag);

    /// <summary>Lower case, runs of anything but letters and digits made one hyphen: "Summer League 2021 (Pro)" is
    /// summer-league-2021-pro.</summary>
    public static string Slug(string text) => NonAlphanumeric().Replace(text.Trim().ToLowerInvariant(), "-").Trim('-');

    /// <summary>A historical season's key: the workbook and sheet its stats come from, "2018-sml18-fantasy-rey/Total Stats".
    /// A season name corrected in the archive keeps it, so a re-import finds the same season.</summary>
    public static string SeasonSourceId(string sourceWorkbook, string sourceSheet) => $"{sourceWorkbook.Trim()}/{sourceSheet.Trim()}";

    /// <summary>The key of a season whose entry has no workbook or sheet: "{league slug}/{season name slug}".</summary>
    public static string FallbackSeasonSourceId(string leagueSlug, string seasonName) => $"{leagueSlug}/{Slug(seasonName)}";

    /// <summary>The first year (1990-2099) in a season's name.</summary>
    public static int? Year(string seasonName)
    {
        var match = YearPattern().Match(seasonName);
        return match.Success ? int.Parse(match.Value) : null;
    }

    /// <summary>A match ID, or a Waypoint match URL ending in one.</summary>
    public static Guid? MatchId(string idOrUrl)
    {
        var last = idOrUrl.Trim().TrimEnd('/').Split('/', '?', '#').Reverse()
            .FirstOrDefault(part => Guid.TryParse(part, out _));
        return last is null ? null : Guid.Parse(last);
    }

    /// <summary>The GameVersion slug for a game as the champions record writes it ("MCC Halo Reach: Dash", "Infinite").</summary>
    public static string? GameVersionSlug(string? game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return null;
        var name = game.Trim();
        if (name.StartsWith("MCC ", StringComparison.OrdinalIgnoreCase))
            name = name[4..];
        return Slug(name) switch
        {
            "halo-3" => "halo3",
            "halo-reach" => "reach",
            "halo-reach-dash" => "reach-dash",
            "halo-4" => "halo4",
            "halo-5" => "halo5",
            "infinite" or "halo-infinite" => "infinite",
            _ => null,
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"(19|20)\d{2}")]
    private static partial Regex YearPattern();
}
