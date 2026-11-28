using System.Text.Json;
using System.Text.Json.Serialization;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Excel;

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
    /// <summary>Season matches decided by a forfeit (no game was played), each naming its season by the season's
    /// <c>HistoricalSourceID</c>. Filled by the spreadsheet parser from the website databases' game tables.</summary>
    public List<ArchiveForfeitInput>? Forfeits { get; init; }
    /// <summary>Matches a season's schedule has whose results are lost (<see cref="Sources.ScheduleTab.Unplayed"/>).</summary>
    public List<ArchiveScheduledMatchInput>? ScheduledMatches { get; init; }
    /// <summary>The seasons (their keys) a schedule covers: their matches with no result are the archive's
    /// <see cref="ScheduledMatches"/>, none when it lists none, so ones it no longer lists go.</summary>
    public List<string>? ScheduledSeasons { get; init; }
    /// <summary>Players lists (the Late League master list and the like): a gamertag's Discord ID, region and system.
    /// Filled by the spreadsheet parser, or from late-league-master.json (<see cref="LateLeagueMasterFile"/>).</summary>
    public List<ArchivePlayerInput>? Players { get; init; }
    /// <summary>The snapshot each source was read from (by its catalog ID), when the archive was parsed from snapshots: where
    /// the import says each row came from (HistoricalProvenance).</summary>
    public Dictionary<string, int>? SnapshotIDs { get; init; }

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

/// <summary>
/// Where a record of the archive was read: its source (the catalog ID), tab, cells (an A1 reference or range) and their text.
/// The import keeps it for each row it makes from the record (HistoricalProvenance). Null on a record of an uploaded archive
/// that doesn't say.
/// </summary>
public sealed record ArchiveOrigin(string SourceId, string? Sheet = null, string? Cells = null, string? Value = null)
{
    /// <summary>A row of a tab: its cells from the first to the last with text.</summary>
    public static ArchiveOrigin Row(string sourceId, SheetTable sheet, int row) => new(sourceId, sheet.Name, sheet.RowRange(row), sheet.RowText(row));

    /// <summary>One cell of a tab.</summary>
    public static ArchiveOrigin Cell(string sourceId, SheetTable sheet, int row, int column)
        => new(sourceId, sheet.Name, sheet.Reference(row, column), sheet.Cell(row, column));

    /// <summary>Rows of a tab, the first to the last (a block of a game's rows): their cells, their texts one per line.</summary>
    public static ArchiveOrigin Rows(string sourceId, SheetTable sheet, int first, int last)
    {
        if (first == last)
            return Row(sourceId, sheet, first);
        var width = Enumerable.Range(first, last - first + 1).Max(r => r < sheet.Rows.Count ? sheet.Rows[r].Length : 0);
        return new(sourceId, sheet.Name, sheet.Range(first, 0, last, Math.Max(0, width - 1)),
            string.Join("\n", Enumerable.Range(first, last - first + 1).Select(sheet.RowText)));
    }

    /// <summary>Each row's origin by the row itself, for a parser that keeps a tab's rows rather than their indexes (indexed once).</summary>
    public static Func<string[], ArchiveOrigin> RowOf(string sourceId, SheetTable sheet)
    {
        var index = RowIndexes(sheet);
        return row => index.TryGetValue(row, out var r) ? Row(sourceId, sheet, r) : new ArchiveOrigin(sourceId, sheet.Name, Value: string.Join(" | ", row));
    }

    /// <summary>A block of a tab's rows by the rows themselves (a game's, which a parser keeps without their indexes): the range from
    /// the first to the last, the rows' texts one per line. Null for no row of the tab.</summary>
    public static Func<IEnumerable<string[]>, ArchiveOrigin?> RowsOf(string sourceId, SheetTable sheet)
    {
        var index = RowIndexes(sheet);
        return rows =>
        {
            var found = rows.Select(row => index.TryGetValue(row, out var r) ? r : -1).Where(r => r >= 0).ToList();
            if (found.Count == 0)
                return null;
            var width = found.Max(r => sheet.Rows[r].Length);
            return new ArchiveOrigin(sourceId, sheet.Name, sheet.Range(found.Min(), 0, found.Max(), Math.Max(0, width - 1)), string.Join("\n", found.Select(sheet.RowText)));
        };
    }

    /// <summary>Each of a tab's rows' index, by the row itself.</summary>
    private static Dictionary<string[], int> RowIndexes(SheetTable sheet)
    {
        var index = new Dictionary<string[], int>(ReferenceEqualityComparer.Instance);
        for (var r = 0; r < sheet.Rows.Count; r++)
            index.TryAdd(sheet.Rows[r], r);
        return index;
    }

    /// <summary>A line summed from several rows (a player's games): the first row's cells, every row's text. Null for none.</summary>
    public static ArchiveOrigin? Of(IEnumerable<ArchiveOrigin?> origins)
    {
        var list = origins.OfType<ArchiveOrigin>().ToList();
        return list.Count switch
        {
            0 => null,
            1 => list[0],
            _ => list[0] with { Value = string.Join("\n", list.Select(o => o.Value)) },
        };
    }
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
    /// <summary>Keys ("{workbook}/{sheet}") of sheets that made a season of their own once and are this season now (a results
    /// sheet named like this stats tab's season): the imported seasons they keyed are leftovers to retire.</summary>
    public List<string> AlsoFromSheets { get; init; } = [];
    /// <summary>The tab the entry was read from (its header row, when the parser knows it).</summary>
    public ArchiveOrigin? Origin { get; set; }
    /// <summary>The champions record's name for the season's titles when it isn't <see cref="SeasonName"/>.</summary>
    public string? Titles { get; init; }
    /// <summary>When the entry's games were played, as far as its sheet says (a stats tab's TimeStamp column): the season's
    /// dates when its games don't give them.</summary>
    public DateTime? FirstPlayedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    /// <summary>The year its source is filed under (the catalog's), for a season whose name and sheet give no date.</summary>
    public int? SourceYear { get; set; }
    public List<ArchivePlayer> Players { get; init; } = [];
    /// <summary>The season's teams, their records, captains and rosters when a standings table lists them (e.g. the
    /// grifball.com archive), including teams with no captured player stat rows.</summary>
    public List<ArchiveTeam> Teams { get; init; } = [];
    /// <summary>A Late League season's ladder (its ranked standings), in its order: each player's place, league, rank, LP,
    /// streak and MMR, and their finish where the workbook's past finishes give one.</summary>
    public List<GrifballWebApp.Domain.LateLeague.LadderStanding> Ladder { get; init; } = [];
    /// <summary>Where each of <see cref="Ladder"/>'s rows was read, by its gamertag (as the row has it).</summary>
    public Dictionary<string, ArchiveOrigin> LadderOrigins { get; init; } = [];
    /// <summary>The season's awards (its MVP, named awards, records), in its sheets' order: a row per winner.</summary>
    public List<ArchiveAward> Awards { get; init; } = [];
    /// <summary>The season's playoff bracket, when a sheet draws one (BracketParser): the import takes it over rebuilding
    /// one from the order its playoff games were played in.</summary>
    public ArchiveBracket? Bracket { get; set; }
}

/// <summary>A season's playoff bracket from its sheet: its series in the order they were played.</summary>
public class ArchiveBracket
{
    /// <summary>The catalog id of the workbook the bracket was read from.</summary>
    public string? SourceId { get; init; }
    public List<ArchiveBracketSeries> Series { get; init; } = [];
}

/// <summary>
/// A series of a bracket sheet: its slot (bracket, round, its number in its bracket as the app numbers them: the
/// winners' bracket and grand final on from 1, the losers' bracket from 1 again), its teams as the season names them,
/// the seeds the sheet gives them, the wins it gives each and its winner, each null where the sheet says nothing.
/// </summary>
public record ArchiveBracketSeries
{
    public Bracket Bracket { get; init; }
    public int Round { get; init; }
    public int Number { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
    public int? HomeSeed { get; init; }
    public int? AwaySeed { get; init; }
    public int? HomeWins { get; init; }
    public int? AwayWins { get; init; }
    public SeasonMatchSide? Winner { get; init; }
    public int? BestOf { get; init; }
    /// <summary>The series' cells (its teams, seeds and score).</summary>
    public ArchiveOrigin? Origin { get; init; }
}

/// <summary>A winner of a season's award (AwardsParser). A record: a copy for a season's player (`with`) keeps every other value.</summary>
public record ArchiveAward
{
    public required string Name { get; init; }
    /// <summary>The winner's gamertag, or a team's name when <see cref="IsTeam"/>.</summary>
    public required string Winner { get; init; }
    /// <summary>A team's award: <see cref="Winner"/> is the team.</summary>
    public bool IsTeam { get; init; }
    /// <summary>The player's team, when the sheet names it.</summary>
    public string? Team { get; init; }
    /// <summary>What it is worth: a record's value, a ranked list's place.</summary>
    public string? Value { get; init; }
    public ArchiveOrigin? Origin { get; init; }
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
    /// <summary>Its captain's place in the season's draft order (1 picks first), from a draft board.</summary>
    public int? DraftOrder { get; init; }
    /// <summary>Its draft picks, from a draft board: each player it picked and in which round.</summary>
    public List<ArchiveDraftPick> Picks { get; init; } = [];
    /// <summary>The row or column the team was read from (its standings row, its draft board column).</summary>
    public ArchiveOrigin? Origin { get; init; }
}

/// <summary>A pick of a season's draft: the player (a gamertag of the season's), its round and, when the board gives it,
/// its number in the whole draft.</summary>
public record ArchiveDraftPick(string Gamertag, int Round, int? Pick = null, ArchiveOrigin? Origin = null);

public class ArchivePlayer
{
    public required string Gamertag { get; init; }
    public string? TeamName { get; set; }
    public string? TradeNote { get; init; }
    public string? RoleNote { get; init; }
    public int StintIndex { get; init; } = 1;
    public decimal? GamesPlayed { get; set; }
    public decimal? Wins { get; set; }
    public decimal? Losses { get; set; }
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
    /// <summary>The stat row.</summary>
    public ArchiveOrigin? Origin { get; set; }
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
    /// <summary>The league's name or slug (e.g. "AGLA", "GGL", "EUGBL"); null defaults to AGLA.</summary>
    [JsonPropertyName("leagueSlug")]
    public string? League { get; init; }
    /// <summary>The source workbook ID that supplied this title, when merged from multiple sources.</summary>
    public string? SourceId { get; init; }
    /// <summary>The source URL that supplied this title, when merged from multiple sources.</summary>
    public string? SourceUrl { get; init; }
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
    /// <summary>The title's row.</summary>
    public ArchiveOrigin? Origin { get; init; }
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
    /// <summary>The source's name in the catalog, what a season's link to it is labelled.</summary>
    public string? Title { get; init; }
}

/// <summary>A Halo Infinite season's Waypoint matches: match IDs or Waypoint match URLs (the ID is the URL's last segment).</summary>
public class WaypointSeasonMatches
{
    public List<string> Regular { get; init; } = [];
    public List<string> Playoffs { get; init; } = [];
    /// <summary>The games whose schedule row names both sides (its Home and Away columns, which hold team or captain
    /// names): the import puts each player on the name that appears in their games.</summary>
    public List<ScheduledGame> Schedule { get; init; } = [];
    /// <summary>The cell each match's link was read from, by the link as <see cref="Regular"/> and <see cref="Playoffs"/> have it.</summary>
    public Dictionary<string, ArchiveOrigin> Origins { get; init; } = [];
}

/// <summary>A schedule row's game: its match ID and the names in the row's Home and Away cells.</summary>
public class ScheduledGame
{
    public required string MatchId { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
    /// <summary>The schedule row.</summary>
    public ArchiveOrigin? Origin { get; init; }
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
    /// <summary>When it was played; a result list's game has the day its season's schedule gives it, if any.</summary>
    public DateTime? PlayedAt { get; set; }
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
    /// <summary>The game's rows (a carnage report's block, a result list's row, a database row).</summary>
    public ArchiveOrigin? Origin { get; set; }
    /// <summary>The schedule's cell its day was taken from, when its own rows give none (a result list's game).</summary>
    public ArchiveOrigin? DatedBy { get; set; }
}

public class RecordedTeamInput
{
    public int Side { get; init; }
    public string? Name { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<GrifballWebApp.Domain.Halo.Outcomes>))]
    public GrifballWebApp.Domain.Halo.Outcomes Outcome { get; init; }
    public int? Score { get; init; }
    /// <summary>The name is the side's team as its season has it (a website database's): the side is that team, or none
    /// when the season hasn't it (a deleted team), never the team its players are on. Otherwise the side is the team most
    /// of its players are on, by its name when they are on none.</summary>
    public bool NamesItsTeam { get; init; }
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
    /// <summary>Times the player picked up the ball (grifball.com's grabs).</summary>
    public int? Grabs { get; init; }
    /// <summary>The player's row of the game.</summary>
    public ArchiveOrigin? Origin { get; init; }
}

/// <summary>
/// A season match decided by a forfeit: no game was played. The import makes it a forfeited season match between the
/// season teams of these names (the winner's and the forfeiting side's, or both forfeiting).
/// </summary>
public class ArchiveForfeitInput
{
    /// <summary>The season's <c>HistoricalSourceID</c>, as <see cref="RecordedGameInput.SeasonSourceId"/>.</summary>
    public required string SeasonSourceId { get; init; }
    /// <summary>Where it comes from (e.g. "{workbook}/{sheet}#{row}"), for the report.</summary>
    public required string SourceKey { get; init; }
    /// <summary>The team that won by the forfeit; with <see cref="BothForfeited"/>, one of the two.</summary>
    public required string Winner { get; init; }
    /// <summary>The team that forfeited.</summary>
    public required string Loser { get; init; }
    /// <summary>A double forfeit: neither side won.</summary>
    public bool BothForfeited { get; init; }
    /// <summary>A playoff series' forfeit: the import leaves it out (its bracket is rebuilt from games), and reports it.</summary>
    public bool IsPlayoffs { get; init; }
    public DateTime? PlayedAt { get; init; }
    public ArchiveOrigin? Origin { get; init; }
}

/// <summary>A match of a season's schedule with no result (its season's results are lost): its two teams, on its day.</summary>
public class ArchiveScheduledMatchInput
{
    /// <summary>The season's <c>HistoricalSourceID</c>, as <see cref="RecordedGameInput.SeasonSourceId"/>.</summary>
    public required string SeasonSourceId { get; init; }
    /// <summary>Where it comes from (e.g. "{workbook}/{sheet}@{day}"), for the report.</summary>
    public required string SourceKey { get; init; }
    public required string Home { get; init; }
    public required string Away { get; init; }
    public required DateTime PlayedAt { get; init; }
    /// <summary>The calendar's cell.</summary>
    public ArchiveOrigin? Origin { get; init; }
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
    public ArchiveOrigin? Origin { get; init; }

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
