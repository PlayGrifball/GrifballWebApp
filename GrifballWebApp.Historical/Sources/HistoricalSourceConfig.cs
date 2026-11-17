using System.Text.Json;
using System.Text.Json.Serialization;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Sources;

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
    /// <summary>Tabs of a season's team standings: each team's record, its rank (seed) and its final place.</summary>
    public List<StandingsTab> Standings { get; init; } = [];
    /// <summary>Tabs of a Late League season's ladder (its ranked standings): each player's division, league, rank, LP,
    /// streak and MMR (LadderParser), which go on the season's ladder.</summary>
    public List<LadderTab> Ladders { get; init; } = [];
    /// <summary>The tab of each player's finishes in past seasons (Late League's "Past Seasons", LadderParser.ParseFinishes): League,
    /// Rank and Pos columns under each season's heading, each season's going on the ladder whose <see cref="LadderTab.Finishes"/>
    /// is that heading.</summary>
    public string? FinishesSheet { get; init; }
    /// <summary>Tabs of a season's awards (MVPs, named season awards, records): AwardsParser.</summary>
    public List<AwardsTab> Awards { get; init; } = [];

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
            .. Tabs.Select(t => t.GamesSheet),
            .. Tables.SelectMany(t => new[] { t.Sheet, t.Players?.Sheet, t.Teams?.Sheet, t.StandingsSheet }),
            .. Waypoint.Select(t => t.Sheet),
            .. Games.Select(t => t.Sheet),
            .. RosterSheets,
            .. Rosters.Select(t => t.Sheet),
            .. Standings.Select(t => t.Sheet),
            .. Ladders.Select(t => t.Sheet),
            FinishesSheet,
            .. Awards.Select(t => t.Sheet),
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
    /// <summary>The champions record's name for the season's titles, when it isn't the season's own ("{season}" or
    /// "{season} ({tier})", e.g. Summer League 2025 (Pro) for Summer League 2025 (Classic Pro)): the titles go to this season.</summary>
    public string? Titles { get; init; }
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
    /// <summary>A tab of the season's games, a row per player per game (a Waypoint match export: PlayerId and Outcome; or a
    /// Name, Wins and Losses per game): each player's games played, and their wins and losses, for a stats tab without those
    /// columns. The Infinite stats tool's tabs (2025 on) count rounds (Rounds Won, Rounds Lost), not games.</summary>
    public string? GamesSheet { get; init; }
    /// <summary>The season's year when nothing in the sheets dates it, nor its name: the source's year otherwise.</summary>
    public int? Year { get; init; }
    /// <summary>Carry a non-empty Team cell down to subsequent player rows in the same block (resetting on totals,
    /// repeating headers, or non-player separator rows).</summary>
    public bool CarryTeam { get; init; }
    /// <summary>Each row with stats is one game of a player's: rows for the same player and team are summed into one
    /// season line (counting games played).</summary>
    public bool PerGame { get; init; }
    /// <summary>Stop reading the main table at a row whose first non-empty cell starts with this prefix (case-insensitive),
    /// e.g. "GAME 1" before a trailing finals/awards section in a different layout.</summary>
    public string? StopAt { get; init; }
    /// <summary>Optional marker (first non-empty cell prefix, case-insensitive) starting a side-by-side per-game box score
    /// section (e.g. Finals "GAME 1" / "Game 2" blocks), parsed alongside <see cref="PerGame"/>.</summary>
    public string? SideBySideGamesStart { get; init; }
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
    /// <summary>A tab of team standings, captains and rosters by <see cref="SeasonColumn"/> (e.g. Standings in the
    /// grifball.com archive): fills <see cref="ArchiveSeason.Teams"/> for each season in <see cref="Seasons"/>.</summary>
    public string? StandingsSheet { get; init; }
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
    /// <summary>The season's division (Pro, Amateur, Open, UK, Aussie, OGBL) when all rows of the season share one.</summary>
    public string? Division { get; init; }
    /// <summary>The champions record's name for the season's titles, when it isn't the season's own.</summary>
    public string? Titles { get; init; }
    /// <summary>The season's year when its name gives none.</summary>
    public int? Year { get; init; }
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

/// <summary>
/// A tab of a season's team standings (StandingsParser): a table of teams with any of a record, a rank or seed, a final
/// place and a captain, its columns found by their headers. Its teams go on the season's regular-season entry, which the
/// import takes their record and placing from.
/// </summary>
public class StandingsTab
{
    public required string Sheet { get; init; }
    /// <summary>The source ID of the season's stats workbook; null: this workbook's own.</summary>
    public string? Season { get; init; }
    /// <summary>The stats tab (its <see cref="StatsTab.Name"/>) of the season, when the workbook has several seasons.</summary>
    public string? Tab { get; init; }
    public StandingsLayout Layout { get; init; }
    /// <summary>The text of the cell the table starts at (its team column's header in <see cref="StandingsLayout.Rows"/>,
    /// the label of its row of team names in <see cref="StandingsLayout.Columns"/>), e.g. "Team Standings" in a table
    /// beside the schedule; null: the first cell that reads Team, Teams or Team Name.</summary>
    public string? Anchor { get; init; }
    /// <summary>Which of the anchor's cells the table starts at, in reading order (two tables side by side, e.g. Pro and
    /// Legacy); the first by default.</summary>
    public int AnchorNumber { get; init; } = 1;
    /// <summary>The teams are in the standings' order: a team's place among them is its rank, unless a rank or seed column
    /// gives it.</summary>
    public bool Ranked { get; init; }
    /// <summary>A team is named "Team (captain)": the name in brackets is its captain's.</summary>
    public bool CaptainInName { get; init; }
}

/// <summary>
/// A tab of a Late League season's ladder (LadderParser): a row per player under a row of headers (Division, League, Rank,
/// LP, Streak, MMR, P/ Win, P/ Loss), the player in <see cref="PlayerColumn"/>. Its rows go on the season named
/// <see cref="Season"/> of the workbook, or on a season of its own (one with no stats tab, keyed by this tab) when the
/// workbook has none of that name.
/// </summary>
public class LadderTab
{
    public required string Sheet { get; init; }
    /// <summary>The season's name, as its stats tab's <see cref="StatsTab.CustomSeason"/> gives it.</summary>
    public required string Season { get; init; }
    /// <summary>The season's GameVersion slug, for a season of its own.</summary>
    public required string Game { get; init; }
    /// <summary>The season's year, for a season of its own: nothing in the tab dates it.</summary>
    public int? Year { get; init; }
    /// <summary>The column of the gamertags (the first by default: its header varies, or is empty).</summary>
    public int PlayerColumn { get; init; }
    /// <summary>The column of the divisions, when its header doesn't say Division (an empty header).</summary>
    public int? DivisionColumn { get; init; }
    /// <summary>The season's heading in the workbook's <see cref="HistoricalSourceConfig.FinishesSheet"/>: its finishes
    /// (league, rank, place) go on this ladder.</summary>
    public string? Finishes { get; init; }
}

public enum StandingsLayout
{
    /// <summary>A team per row, under a row of headers.</summary>
    Rows,
    /// <summary>A team per column, beside a column of row labels (Team Name:, Captain:, Season Standings, Playoff Ending, Record).</summary>
    Columns,
    /// <summary>A cell per team down the anchor's column, its record in brackets: "ZMD (Zeg) (17-3) (95-46)".</summary>
    List,
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

/// <summary>
/// A tab of season awards (AwardsParser): who won what, laid out as <see cref="Layout"/> says. Its awards go on the season's
/// regular-season entry, which the import records them from (Season.RecordArchivedAwards).
/// </summary>
public class AwardsTab
{
    public required string Sheet { get; init; }
    /// <summary>The source ID of the season's stats workbook; null: this workbook's own. A
    /// <see cref="AwardsLayout.Seasons"/> tab names each block's season in <see cref="Seasons"/> instead.</summary>
    public string? Season { get; init; }
    /// <summary>The stats tab (its <see cref="StatsTab.Name"/>, or a stats table's "{table}#s{season}" key) of the season,
    /// when the workbook has several seasons.</summary>
    public string? Tab { get; init; }
    public AwardsLayout Layout { get; init; }
    /// <summary>Award labels in the sheet, and the name each is recorded under (empty: the label). A
    /// <see cref="AwardsLayout.Labels"/> or <see cref="AwardsLayout.Seasons"/> tab reads only these; a
    /// <see cref="AwardsLayout.Columns"/> tab reads every column, renaming the ones given here.</summary>
    public Dictionary<string, string> Awards { get; init; } = [];
    /// <summary>A <see cref="AwardsLayout.Seasons"/> tab's seasons: the text of a block's title that names it ("Season 6
    /// Pro"), to the season's source ID. The longest that the title holds wins.</summary>
    public Dictionary<string, string> Seasons { get; init; } = [];
    /// <summary>Winners the sheet names by a nickname, to their gamertag ("Luxe": "yLuxe"); an empty gamertag is no
    /// player (a joke entry), left out.</summary>
    public Dictionary<string, string> Names { get; init; } = [];
    /// <summary>A <see cref="AwardsLayout.Labels"/> winner's cell may name several players: "Mavbrid And MysticalNovice, Jussshhhyyy
    /// And Scullen cx" is four (split at "and" and commas). Off: a cell is one winner, as a gamertag may have either.</summary>
    public bool SplitWinners { get; init; }
    /// <summary>A <see cref="AwardsLayout.Columns"/> list is ranked: each winner's place in it is the award's value ("#2").</summary>
    public bool Ranked { get; init; }
    /// <summary>The text of the cell the <see cref="AwardsLayout.Columns"/> table's header row starts at (cells left of it
    /// are another table's); null: the sheet's first row.</summary>
    public string? Anchor { get; init; }
}

public enum AwardsLayout
{
    /// <summary>A header row of award names, each award's winners down its column below it to the first gap.</summary>
    Columns,
    /// <summary>Award labels anywhere in the sheet (<see cref="AwardsTab.Awards"/>), each winner the first text below its
    /// label (in the next four rows with text), within the label's column and the next two, in the sheet's order.</summary>
    Labels,
    /// <summary>A block per season, its title in the first column ("SL24 Season 2 Champions - ..."), and "MVP: name"
    /// cells in its row and the row below (<see cref="AwardsTab.Awards"/>: "MVP", "FMVP"). A season key is matched as whole words.</summary>
    Seasons,
    /// <summary>A records table, a record per row under the header: category, description, record_value, holder and
    /// team_or_captain columns. A record of a category that starts with "Team" is a team's: its holder is the team.</summary>
    Records,
}
