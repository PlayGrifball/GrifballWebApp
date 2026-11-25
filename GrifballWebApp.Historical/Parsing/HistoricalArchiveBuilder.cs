using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>The archive parsed from the sources' snapshots, and the seasons the changed sources touch.</summary>
public sealed class HistoricalArchiveBuild
{
    public required HistoricalArchiveInput Input { get; init; }
    /// <summary>What could not be read (a missing tab, a link without a match ID), for the report.</summary>
    public List<string> Warnings { get; init; } = [];
    /// <summary>The seasons (their entries' keys) the changed sources make up; null is every season (a full build, or the
    /// champions record changed: its titles are of every season).</summary>
    public HashSet<string>? Seasons { get; init; }
}

/// <summary>
/// Builds the <see cref="HistoricalArchiveInput"/> the importer reads from the sources' snapshots, as the Node
/// archive-tools (now removed) compiled it: season stats tabs, database tables of stats (StatsTableParser), the champions
/// record, Waypoint game lists, per-game tabs and the players list, each where the source's catalog entry
/// (<see cref="HistoricalSourceConfig"/>) says. Every source is parsed even when only some changed: a season is imported
/// whole (its stats, games and titles), so the import is told which seasons the changed sources touch instead.
/// <para>
/// One source at a time (<see cref="Add"/>, then <see cref="Finish"/>): a source's snapshot is read for the tabs its config
/// names only, and nothing of the snapshot or its workbook is kept once its parts are parsed, so a build holds one
/// workbook at most, not every source's.
/// </para>
/// </summary>
public sealed partial class HistoricalArchiveBuilder
{
    private readonly IReadOnlySet<string>? _changedSourceIds;
    private readonly List<string> _warnings = [];
    private readonly List<ArchiveSeason> _seasons = [];
    private readonly List<ArchiveChampionship> _champions = [];
    private string? _championsSourceId;
    private string? _championsSourceUrl;
    private readonly List<RecordedGameInput> _games = [];
    private readonly List<ArchiveForfeitInput> _forfeits = [];
    /// <summary>Schedules' matches of seasons whose results are lost (<see cref="ScheduleTab.Unplayed"/>).</summary>
    private readonly List<ArchiveScheduledMatchInput> _scheduled = [];
    /// <summary>The seasons a schedule covers (<see cref="HistoricalArchiveInput.ScheduledSeasons"/>).</summary>
    private readonly HashSet<string> _scheduledSeasons = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ArchivePlayerInput> _players = [];
    private readonly List<CatalogSource> _catalog = [];
    /// <summary>Each Waypoint tab's games, parsed as its source is read; put under their season's key once every season is known.</summary>
    private readonly List<(string SourceId, WaypointTab Tab, (List<string> Regular, List<string> Playoffs, List<ScheduledGame> Schedule, List<string> Unparsed)? Games)> _waypoint = [];
    /// <summary>Rosters of seasons whose stats are in another source, applied once every season is known.</summary>
    private readonly List<(string SourceId, RosterTab Tab, Dictionary<string, string>? Teams)> _rosters = [];
    /// <summary>Each standings tab's teams, put on their season's regular season once every season is known.</summary>
    private readonly List<(string SourceId, StandingsTab Tab, List<ArchiveTeam>? Teams)> _standings = [];
    /// <summary>Each ladder tab's players (with its workbook's season defaults, its year and its past finishes), put on their
    /// season once every season is known.</summary>
    private readonly List<LadderRead> _ladders = [];
    /// <summary>Each result list's games, put under their season once every season is known.</summary>
    private readonly List<(string SourceId, int? Year, SeasonDefaults? Defaults, ResultsTab Tab, List<RecordedGameInput>? Games)> _results = [];
    /// <summary>Each schedule tab's matches, which date their season's series once every result list's games are in.</summary>
    private readonly List<(string SourceId, ScheduleTab Tab, List<CalendarMatch>? Matches)> _schedules = [];
    /// <summary>Each bracket tab's series, put on their season's regular season once every season (and its standings) is known.</summary>
    private readonly List<(string SourceId, BracketTab Tab, ArchiveBracket? Bracket)> _brackets = [];
    /// <summary>Each normalized bracket table's series, keyed by season source ID.</summary>
    private readonly List<(string SourceId, string SeasonSourceId, ArchiveBracket Bracket)> _tableBrackets = [];
    /// <summary>Each draft tab's teams, put on their season's regular season once every season (and its standings) is known.</summary>
    private readonly List<(string SourceId, DraftTab Tab, List<ArchiveTeam>? Teams)> _drafts = [];
    /// <summary>Each awards tab's awards, put on their season's regular season once every season is known.</summary>
    private readonly List<(string SourceId, AwardsTab Tab, List<AwardsParser.ParsedAward>? Awards)> _awards = [];
    /// <summary>Per-game record counts from CSV-only GameRecordSheets, keyed by "{sourceId}/{sheet}".</summary>
    private readonly Dictionary<string, (SeasonStatsParser.GameRecordsResult? Records, bool Changed)> _gameRecords = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Stats tabs whose <see cref="StatsTab.GamesSheet"/> points to another source ("{sourceId}/{sheet}").</summary>
    private readonly List<(string SourceId, StatsTab Tab, ArchiveSeason Season, Dictionary<int, (string Key, string Header)> Columns)> _pendingGameCounts = [];
    private readonly HashSet<string> _affected = new(StringComparer.OrdinalIgnoreCase);
    private bool _everySeason;
    private bool _playersChanged;

    /// <param name="changedSourceIds">The sources that changed (the seasons they make up are the build's Seasons); null is every source.</param>
    public HistoricalArchiveBuilder(IReadOnlySet<string>? changedSourceIds = null)
    {
        _changedSourceIds = changedSourceIds;
        _everySeason = changedSourceIds is null;
        _playersChanged = changedSourceIds is null;
    }

    public static HistoricalArchiveBuild Build(IReadOnlyList<HistoricalSourceContent> sources, IReadOnlySet<string>? changedSourceIds = null)
    {
        var builder = new HistoricalArchiveBuilder(changedSourceIds);
        foreach (var source in sources)
            builder.Add(source);
        return builder.Finish();
    }

    /// <summary>Parses one source's snapshot into the archive. Nothing of it is kept but what was parsed.</summary>
    public void Add(HistoricalSourceContent content)
    {
        var source = content.Source;
        _catalog.Add(new CatalogSource { Id = source.HistoricalSourceID, Url = source.Url, Title = source.Title });
        var config = HistoricalSourceConfig.FromJson(source.Config);
        if (config is null)
            return;
        var id = source.HistoricalSourceID;
        var changed = _changedSourceIds?.Contains(id) != false;
        if (ReadWorkbook(content, config) is not { } workbook)
            return;

        var pendingGames = new List<(StatsTab Tab, ArchiveSeason Season, Dictionary<int, (string Key, string Header)> Columns)>();
        var entries = SeasonStatsParser.Parse(id, config, workbook, _warnings, pendingGames);
        foreach (var (tab, season, columns) in pendingGames)
            _pendingGameCounts.Add((id, tab, season, columns));
        entries.AddRange(StatsTableParser.Parse(id, config, workbook, _warnings));
        foreach (var entry in entries)
            entry.SourceYear ??= source.Year;
        _seasons.AddRange(entries);
        foreach (var sheetName in config.GameRecordSheets)
            _gameRecords[$"{id}/{sheetName.Trim()}"] = (SeasonStatsParser.GameRecords(id, workbook, sheetName, _warnings), changed);
        if (config.ChampionsSheet is not null)
            AddChampions(id, source.Url, config, workbook, changed);
        if (config.ChampionsTable is { } championsTable)
            AddChampionsTable(id, source.Url, config, championsTable.Sheet, workbook, changed);
        AddWaypoint(id, config, workbook);
        AddSeasonTabs(id, config, workbook);
        if (config.BracketTable is { } bracketTable)
            AddBracketTable(id, config, bracketTable, workbook);
        AddResultLists(id, source.Year, config, workbook);
        AddGames(id, config, workbook, changed);
        AddGameTables(id, config, workbook, entries);
        AddLadders(source, config, workbook, changed);
        if (config.PlayersSheet is not null)
            AddPlayers(id, config.PlayersSheet, workbook, changed);
        if (changed)
            foreach (var entry in entries)
                _affected.Add(HistoricalKeys.SeasonSourceId(entry.SourceWorkbook!, entry.SourceSheet!));
    }

    /// <summary>The tabs that fill in seasons known only once every source is read (rosters, standings, drafts, awards,
    /// brackets): parsed now, put on their seasons in <see cref="Finish"/>. Result lists are <see cref="AddResultLists"/>.</summary>
    private void AddSeasonTabs(string id, HistoricalSourceConfig config, SheetWorkbook workbook)
    {
        foreach (var tab in config.Rosters)
            _rosters.Add((id, tab, workbook.Sheet(tab.Sheet) is { } rosterSheet ? RosterParser.Parse(rosterSheet, config, tab.Names) : null));
        foreach (var tab in config.Standings)
            _standings.Add((id, tab, workbook.Sheet(tab.Sheet) is { } standingsSheet ? Named(StandingsParser.Parse(standingsSheet, tab), config) : null));
        foreach (var tab in config.Drafts)
            _drafts.Add((id, tab, workbook.Sheet(tab.Sheet) is { } draftSheet ? DraftParser.Parse(draftSheet, tab, config) : null));
        foreach (var tab in config.Awards)
            _awards.Add((id, tab, workbook.Sheet(tab.Sheet) is { } awardsSheet ? AwardsParser.Parse(awardsSheet, tab) : null));
        foreach (var tab in config.Brackets)
            _brackets.Add((id, tab, BracketParser.Parse(id, tab, workbook, _warnings)));
    }

    /// <summary>The result lists and schedules of a source (<see cref="AddResults"/> puts their games under their seasons, <see cref="AddSchedules"/> dates them).</summary>
    private void AddResultLists(string id, int? year, HistoricalSourceConfig config, SheetWorkbook workbook)
    {
        foreach (var tab in config.Results)
            _results.Add((id, year, config.Season, tab, workbook.Sheet(tab.Sheet) is { } resultsSheet ? GameResultsParser.Parse(id, tab, resultsSheet, _warnings) : null));
        foreach (var tab in config.Schedules)
            _schedules.Add((id, tab, workbook.Sheet(tab.Sheet) is { } scheduleSheet ? ScheduleParser.Parse(id, tab, scheduleSheet, _warnings) : null));
    }

    /// <summary>The snapshot's workbook, the sheets its config reads; null (a warning said why) when it can't be read.</summary>
    private SheetWorkbook? ReadWorkbook(HistoricalSourceContent content, HistoricalSourceConfig config)
    {
        var source = content.Source;
        var id = source.HistoricalSourceID;
        SheetWorkbook? workbook;
        try
        {
            var sheets = config.SheetNames();
            workbook = SheetWorkbook.Read(content.Content, source.CsvSheetName, name => sheets.Contains(name.Trim()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _warnings.Add($"{id}: could not be read ({ex.Message})");
            return null;
        }
        if (workbook is null)
            _warnings.Add($"{id}: its snapshot is not a workbook");
        return workbook;
    }

    private void AddChampions(string id, string url, HistoricalSourceConfig config, SheetWorkbook workbook, bool changed)
    {
        var sheetName = config.ChampionsSheet!;
        if (workbook.Sheet(sheetName) is { } sheet)
        {
            // Every source's champions, each named as its config names its teams (#534)
            var parsed = ChampionsParser.Parse(id, url, sheet, config);
            _championsSourceId = id;
            _championsSourceUrl = url;
            _champions.AddRange(parsed.Champions);
            _everySeason |= changed;
        }
        else
            _warnings.Add($"{id}: no sheet '{sheetName}'");
    }

    private void AddChampionsTable(string id, string url, HistoricalSourceConfig config, string sheetName, SheetWorkbook workbook, bool changed)
    {
        if (workbook.Sheet(sheetName) is { } sheet)
        {
            var parsed = ChampionsParser.ParseTable(id, url, sheet, _warnings, config);
            _championsSourceId ??= id;
            _championsSourceUrl ??= url;
            _champions.AddRange(parsed.Champions);
            _everySeason |= changed;
        }
        else
            _warnings.Add($"{id}: no sheet '{sheetName}'");
    }

    private void AddBracketTable(string id, HistoricalSourceConfig config, BracketTableConfig table, SheetWorkbook workbook)
    {
        var stats = config.Tables.FirstOrDefault(t => string.Equals(t.Sheet, table.Stats, StringComparison.OrdinalIgnoreCase));
        if (stats is null)
        {
            _warnings.Add($"{id}: bracket table '{table.Sheet}' names no stats table '{table.Stats}'");
            return;
        }
        foreach (var (seasonSourceId, bracket) in BracketParser.ParseTable(id, table, stats, workbook, _warnings))
            _tableBrackets.Add((id, seasonSourceId, bracket));
    }

    private void AddWaypoint(string id, HistoricalSourceConfig config, SheetWorkbook workbook)
    {
    foreach (var tab in config.Waypoint)
    {
        if (workbook.Sheet(tab.Sheet) is { } sheet)
        {
            var unparsed = new List<string>();
            var (regular, playoffs, schedule) = WaypointMatchParser.Parse(sheet, unparsed, tab.BareIds);
            _waypoint.Add((id, tab, (regular, playoffs, schedule, unparsed)));
        }
        else
            _waypoint.Add((id, tab, null));
    }
    }

    private void AddGames(string id, HistoricalSourceConfig config, SheetWorkbook workbook, bool changed)
    {
    foreach (var tab in config.Games)
    {
        _games.AddRange(RecordedGameParser.Parse(id, tab, workbook, _warnings));
        if (changed)
            _affected.Add(tab.Season);
    }
    }

    /// <summary>The source's database tables of games (<see cref="GamesTableParser"/>): the games and forfeits of its own
    /// seasons. One of a season its stats make no entry for is left out and reported. Their seasons are the entries', which
    /// the source's change already marks.</summary>
    private void AddGameTables(string id, HistoricalSourceConfig config, SheetWorkbook workbook, List<ArchiveSeason> entries)
    {
        if (config.GameTables.Count == 0)
            return;
        var parsed = GamesTableParser.Parse(id, config, workbook, _warnings);
        var seasons = entries.Select(e => HistoricalKeys.SeasonSourceId(e.SourceWorkbook!, e.SourceSheet!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var games = parsed.Games.Where(g => seasons.Contains(g.SeasonSourceId)).ToList();
        var forfeits = parsed.Forfeits.Where(f => seasons.Contains(f.SeasonSourceId)).ToList();
        var left = parsed.Games.Count - games.Count + parsed.Forfeits.Count - forfeits.Count;
        if (left > 0)
            _warnings.Add($"{id}: {left} game(s) or forfeit(s) of a season with no stats, skipped");
        _games.AddRange(games);
        _forfeits.AddRange(forfeits);
    }

    private void AddPlayers(string id, string sheetName, SheetWorkbook workbook, bool changed)
    {
        if (workbook.Sheet(sheetName) is { } sheet)
            _players.AddRange(PlayersListParser.Parse(id, sheet));
        else
            _warnings.Add($"{id}: no sheet '{sheetName}'");
        _playersChanged |= changed;
    }

    /// <summary>A ladder tab read: its players (null: no such sheet, or no table), its past finishes (null: none), and
    /// what a season of its own is made of.</summary>
    private sealed record LadderRead(string SourceId, LadderTab Tab, List<LadderStanding>? Players, List<LadderStanding>? Finishes,
        SeasonDefaults? Defaults, int? SourceYear, bool Changed);

    private void AddLadders(HistoricalSource source, HistoricalSourceConfig config, SheetWorkbook workbook, bool changed)
    {
        if (config.Ladders.Count == 0)
            return;
        Dictionary<string, List<LadderStanding>>? finishes = null;
        if (config.FinishesSheet is not null)
        {
            finishes = workbook.Sheet(config.FinishesSheet) is { } finishesSheet ? LadderParser.ParseFinishes(finishesSheet) : null;
            if (finishes is null)
                _warnings.Add($"{source.HistoricalSourceID}: past finishes '{config.FinishesSheet}' not read (no such sheet, or no Pos column)");
        }
        foreach (var tab in config.Ladders)
        {
            var players = workbook.Sheet(tab.Sheet) is { } sheet ? LadderParser.Parse(sheet, tab) : null;
            var finished = tab.Finishes is { } heading && finishes is not null ? finishes.GetValueOrDefault(heading.Trim()) : null;
            if (tab.Finishes is not null && finishes is not null && finished is null)
                _warnings.Add($"{source.HistoricalSourceID}: no past finishes under '{tab.Finishes}' for ladder '{tab.Sheet}'");
            _ladders.Add(new LadderRead(source.HistoricalSourceID, tab, players, finished, config.Season, tab.Year ?? source.Year, changed));
        }
    }

    public HistoricalArchiveBuild Finish()
    {
        ApplyCrossSourceGameCounts();
        var waypoint = WaypointBySeason();
        FillTeamsFromRosters();
        AddStandings();
        PutLadders();
        AddDrafts();
        AddResults();
        NameGameSides();
        AddSchedules();
        AddBrackets();
        AddAwards();

        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = _seasons },
            Championships = _champions.Count > 0 || _championsSourceId is not null
                ? new ChampionsArchive { SourceId = _championsSourceId, SourceUrl = _championsSourceUrl, Champions = _champions }
                : null,
            Catalog = new ArchiveCatalog { Sources = _catalog },
            WaypointMatches = waypoint.Where(w => w.Value.Regular.Count > 0 || w.Value.Playoffs.Count > 0)
                .ToDictionary(w => w.Key, w => new WaypointSeasonMatches { Regular = [.. w.Value.Regular], Playoffs = [.. w.Value.Playoffs], Schedule = [.. w.Value.Schedule.Values.OrderBy(g => g.MatchId, StringComparer.Ordinal)] }),
            RecordedGames = _games.Count > 0 ? _games : null,
            Forfeits = _forfeits.Count > 0 ? _forfeits : null,
            ScheduledMatches = _scheduled.Count > 0 ? _scheduled : null,
            ScheduledSeasons = _scheduledSeasons.Count > 0 ? [.. _scheduledSeasons] : null,
            // The players list goes along when it changed (it is matched against every season's placeholders)
            Players = _playersChanged && _players.Count > 0 ? _players : null,
        };
        return new HistoricalArchiveBuild { Input = input, Warnings = _warnings, Seasons = _everySeason ? null : _affected };
    }

    private void ApplyCrossSourceGameCounts()
    {
        foreach (var (sourceId, tab, season, columns) in _pendingGameCounts)
        {
            var key = string.Join('/', tab.GamesSheet!.Split('/', 2).Select(p => p.Trim()));
            if (!_gameRecords.TryGetValue(key, out var found))
            {
                _warnings.Add($"{sourceId}: no game records source '{key}'");
                continue;
            }
            if (found.Records is null)
                continue;
            SeasonStatsParser.ApplyGameRecords(sourceId, key, found.Records, columns, season.Players, _warnings);
            season.FirstPlayedAt ??= found.Records.FirstPlayedAt;
            season.LastPlayedAt ??= found.Records.LastPlayedAt;
            if (found.Changed)
                _affected.Add(HistoricalKeys.SeasonSourceId(season.SourceWorkbook!, season.SourceSheet!));
        }
    }

    /// <summary>The Waypoint games, by the key of their season's regular season.</summary>
    private Dictionary<string, (SortedSet<string> Regular, SortedSet<string> Playoffs, Dictionary<string, ScheduledGame> Schedule)> WaypointBySeason()
    {
        // Waypoint games go under their season's regular-season key; a game listed as both is a playoff game
        // A game the schedule names the teams of more than once keeps its first row's
        var waypoint = new Dictionary<string, (SortedSet<string> Regular, SortedSet<string> Playoffs, Dictionary<string, ScheduledGame> Schedule)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sourceId, tab, games) in _waypoint)
        {
            var entries = _seasons.Where(s => string.Equals(s.SourceWorkbook, tab.Season, StringComparison.OrdinalIgnoreCase)).ToList();
            var primary = entries.FirstOrDefault(e => !e.IsPlayoffs) ?? entries.FirstOrDefault();
            if (primary is null || games is null)
            {
                _warnings.Add($"{sourceId}: Waypoint games of '{tab.Sheet}' not read ({(games is null ? "no such sheet" : $"no season from {tab.Season}")})");
                continue;
            }
            var key = HistoricalKeys.SeasonSourceId(primary.SourceWorkbook!, primary.SourceSheet!);
            _warnings.AddRange(games.Value.Unparsed.Select(u => $"{sourceId}: a link without a match ID, {u}"));
            if (!waypoint.TryGetValue(key, out var lists))
                waypoint[key] = lists = (new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal), new Dictionary<string, ScheduledGame>(StringComparer.OrdinalIgnoreCase));
            lists.Regular.UnionWith(games.Value.Regular);
            lists.Playoffs.UnionWith(games.Value.Playoffs);
            foreach (var game in games.Value.Schedule)
                lists.Schedule.TryAdd(game.MatchId, game);
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.Add(key);
        }
        foreach (var lists in waypoint.Values)
            lists.Regular.ExceptWith(lists.Playoffs);
        return waypoint;
    }

    private void FillTeamsFromRosters()
    {
        // Rosters from another source fill the team of the season's players whose stats name none
        foreach (var (sourceId, tab, teams) in _rosters)
        {
            var entries = _seasons.Where(s => string.Equals(s.SourceWorkbook, tab.Season, StringComparison.OrdinalIgnoreCase)).ToList();
            if (teams is null || entries.Count == 0)
            {
                _warnings.Add($"{sourceId}: roster '{tab.Sheet}' not read ({(teams is null ? "no such sheet" : $"no season from {tab.Season}")})");
                continue;
            }
            foreach (var player in entries.SelectMany(e => e.Players).Where(p => string.IsNullOrWhiteSpace(p.TeamName)))
                player.TeamName = teams.GetValueOrDefault(HistoricalKeys.Normalize(player.Gamertag));
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.UnionWith(entries.Select(e => HistoricalKeys.SeasonSourceId(e.SourceWorkbook!, e.SourceSheet!)));
        }
    }

    /// <summary>A standings tab's teams under the names the workbook's config gives them (<see cref="HistoricalSourceConfig.TeamNames"/>),
    /// without a placeholder it doesn't name ("Team Name"): no team, and never a real one a letter away.</summary>
    private static List<ArchiveTeam>? Named(List<ArchiveTeam>? teams, HistoricalSourceConfig config)
        => teams?.Select(t => t with { TeamName = config.TeamName(t.TeamName) }).Where(t => !HistoricalKeys.IsExcludedTeamName(t.TeamName)).ToList();

    /// <summary>Each standings tab's teams go on its season's regular-season entry (the first, or its <see cref="StandingsTab.Tab"/>),
    /// where the import reads a team's record and placing.</summary>
    private void AddStandings()
    {
        foreach (var (sourceId, tab, teams) in _standings)
        {
            var workbook = tab.Season ?? sourceId;
            var (regular, season) = SeasonOf(workbook, tab.Tab);
            if (teams is null || regular is null)
            {
                _warnings.Add($"{sourceId}: standings '{tab.Sheet}' not read ({(teams is null ? "no such sheet, or no table" : $"no season from {workbook}")})");
                continue;
            }
            // The season's names of its teams: its stats', and an earlier standings tab's (two tabs of a season spell a team
            // alike, the stats naming none)
            // A captain or roster player is one of the season's players (SeasonPlayer: as written, as the stats write it, or
            // without the nickname after it): a nickname ("Team (Snipez)") or a typo would be a placeholder player. A season
            // with no player lines (its league kept no stats) has only its standings' names: a roster's names are gamertags
            var (known, _) = Spellings(season, regular);
            var players = PlayersOf(season);
            string? PlayerOf(string name) => players.Count == 0
                ? StandingsParser.Gamertag(name)
                : SeasonPlayer(name, players) ?? SeasonPlayer(StandingsParser.Gamertag(name), players);
            var named = teams.Select(t => t with
            {
                TeamName = KnownName(t.TeamName, known),
                Captain = t.Captain is { } captain ? PlayerOf(captain) : null,
                Roster = [.. t.Roster.Select(PlayerOf).OfType<string>()],
            }).ToList();
            // The stats' placeholder of a captain's team ("OREOTHECAT's Team") is the team the standings name with that captain,
            // or name after them: it takes the team's name as the season has it
            foreach (var (team, given) in named.Zip(teams))
            {
                var placeholders = new[] { given.Captain, given.TeamName, team.TeamName }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => $"{c!.Trim()}'s Team").ToList();
                foreach (var player in season.SelectMany(s => s.Players).Where(p => placeholders.Contains(p.TeamName?.Trim() ?? "", StringComparer.OrdinalIgnoreCase)))
                    player.TeamName = team.TeamName;
            }
            regular.Teams.AddRange(named);
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.UnionWith(season.Select(e => HistoricalKeys.SeasonSourceId(e.SourceWorkbook!, e.SourceSheet!)));
        }
    }

    /// <summary>Each bracket on its season's regular-season entry (the first, or its <see cref="BracketTab.Tab"/>), its teams
    /// under the names the season has them by (its stats' and standings', as <see cref="KnownName"/> finds them).</summary>
    private void AddBrackets()
    {
        foreach (var (sourceId, tab, bracket) in _brackets)
        {
            if (bracket is null)
                continue;
            var workbook = tab.Season ?? sourceId;
            var regular = _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase)
                && (tab.Tab is null || string.Equals(s.SourceSheet, tab.Tab, StringComparison.OrdinalIgnoreCase)));
            if (regular is null)
            {
                _warnings.Add($"{sourceId}: bracket '{tab.Sheet ?? tab.Rounds[0].Sheet}' not read (no season from {workbook})");
                continue;
            }
            AttachBracket(regular, bracket, tab.Aliases, sourceId);
        }

        foreach (var (sourceId, seasonSourceId, bracket) in _tableBrackets)
        {
            var regular = _seasons.FirstOrDefault(s => !s.IsPlayoffs
                && string.Equals(HistoricalKeys.SeasonSourceId(s.SourceWorkbook!, s.SourceSheet!), seasonSourceId, StringComparison.OrdinalIgnoreCase));
            if (regular is null)
            {
                _warnings.Add($"{sourceId}: bracket for '{seasonSourceId}' not attached (no regular-season entry)");
                continue;
            }
            AttachBracket(regular, bracket, null, sourceId);
        }
    }

    private void AttachBracket(ArchiveSeason regular, ArchiveBracket bracket, Dictionary<string, string>? aliases, string sourceId)
    {
        var season = _seasons.Where(s => string.Equals(s.SourceWorkbook, regular.SourceWorkbook, StringComparison.OrdinalIgnoreCase) && s.SeasonName == regular.SeasonName).ToList();
        var known = season.SelectMany(s => s.Players).Select(p => p.TeamName).OfType<string>().Concat(season.SelectMany(s => s.Teams).Select(t => t.TeamName))
            .Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // A bracket's team is often "Team (captain)": the name without it, a few letters off, is the season's too
        string Named(string name)
        {
            name = aliases?.GetValueOrDefault(name) ?? name;
            return Closest(name, known) ?? Closest(StandingsParser.WithoutNote(name), known) ?? name;
        }
        regular.Bracket = new ArchiveBracket { SourceId = sourceId, Series = [.. bracket.Series.Select(s => s with { Home = Named(s.Home), Away = Named(s.Away) })] };
        if (_changedSourceIds?.Contains(sourceId) != false)
            _affected.UnionWith(season.Select(e => HistoricalKeys.SeasonSourceId(e.SourceWorkbook!, e.SourceSheet!)));
    }

    /// <summary>The regular-season entry of the workbook's season (the first, or its stats tab's), and all of that season's entries.</summary>
    private (ArchiveSeason? Regular, List<ArchiveSeason> Season) SeasonOf(string workbook, string? tab)
    {
        var regular = _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase)
            && (tab is null || string.Equals(s.SourceSheet, tab, StringComparison.OrdinalIgnoreCase)));
        var season = regular is null ? [] : _seasons.Where(s => string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase) && s.SeasonName == regular.SeasonName).ToList();
        return (regular, season);
    }

    /// <summary>
    /// Each draft tab's teams go on its season's regular-season entry, where the import reads a team's captain, its place
    /// in the draft order and its picks. A draft team is one of the season's teams (its stats' or standings', as
    /// <see cref="KnownName"/> finds it, else its captain's team in the stats): a board's team the season doesn't have (a
    /// column of notes) is none. Its captain and picks are the season's players (<see cref="SeasonPlayer"/>): a nickname
    /// ("Psyduck") is left out, as it would be a placeholder player.
    /// </summary>
    private void AddDrafts()
    {
        // The tabs with a draft order first, then by source: a team's draft is the first tab's that gives it (the import's),
        // whatever order the sources were read in
        foreach (var (sourceId, tab, teams) in _drafts.OrderBy(d => d.Tab.Order == DraftOrder.None).ThenBy(d => d.SourceId, StringComparer.OrdinalIgnoreCase))
        {
            var workbook = tab.Season ?? sourceId;
            var (regular, season) = SeasonOf(workbook, tab.Tab);
            if (teams is null || regular is null)
            {
                _warnings.Add($"{sourceId}: draft '{tab.Sheet}' not read ({(teams is null ? "no such sheet, or no board or table" : $"no season from {workbook}")})");
                continue;
            }
            var known = season.SelectMany(s => s.Players).Select(p => p.TeamName).Concat(regular.Teams.Select(t => t.TeamName))
                .Where(n => !HistoricalKeys.IsExcludedTeamName(n)).Select(n => n!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var players = PlayersOf(season);
            var teamOf = season.SelectMany(s => s.Players).Where(p => !HistoricalKeys.IsExcludedTeamName(p.TeamName))
                .GroupBy(p => HistoricalKeys.Normalize(p.Gamertag)).ToDictionary(g => g.Key, g => g.First().TeamName!.Trim());
            foreach (var team in teams)
            {
                var captain = SeasonPlayer(Nicknamed(tab, team.Captain), players);
                var name = (team.TeamName.Length > 0 ? Closest(team.TeamName, known) : null)
                    ?? (captain is not null ? teamOf.GetValueOrDefault(HistoricalKeys.Normalize(captain)) : null);
                if (name is null)
                    continue;
                var picks = team.Picks.Select(p => SeasonPlayer(Nicknamed(tab, p.Gamertag), players) is { } gamertag ? p with { Gamertag = gamertag } : null).OfType<ArchiveDraftPick>().ToList();
                regular.Teams.Add(team with { TeamName = name, Captain = captain, Picks = picks });
            }
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.UnionWith(season.Select(e => HistoricalKeys.SeasonSourceId(e.SourceWorkbook!, e.SourceSheet!)));
        }
    }

    /// <summary>A draft's name for a player: the gamertag its tab's <see cref="DraftTab.Names"/> gives the nickname, else the name.</summary>
    private static string? Nicknamed(DraftTab tab, string? name)
        => name is null ? null : tab.Names.FirstOrDefault(n => string.Equals(n.Key, name.Trim(), StringComparison.OrdinalIgnoreCase)).Value ?? name;

    /// <summary>Two names' digits don't tell them apart: the same, or only one has any ("NecessaryEvil" and NecessaryEvil10,
    /// never "Reaper3" and Reaper2).</summary>
    private static bool SameNumber(string a, string b)
    {
        var (x, y) = (Digits(a), Digits(b));
        return x == y || x.Length == 0 || y.Length == 0;
    }

    /// <summary>The shortest name <see cref="SeasonPlayer"/> takes a few letters off.</summary>
    private const int MinCloseKeyLength = 5;

    /// <summary>The season's players' gamertags (as its stats write them).</summary>
    private static List<string> PlayersOf(List<ArchiveSeason> season)
        => season.SelectMany(s => s.Players).Select(p => p.Gamertag).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// The season's player a standings or draft cell names, as the stats write them: the same gamertag, else the one player
    /// whose gamertag is the same but for spacing, symbols, framing or a "#1234" suffix (<see cref="HistoricalKeys.StripFraming"/>:
    /// "Mr_Epic_Beast", "OREOTHECAT#5618"), else the one a few letters off (one from 5 letters, two from 8, three from 15, its
    /// number no other:
    /// "NecessaryEvil" for NecessaryEvil10, "Solace VII"). Null for a name no player has (a nickname), or more than one is
    /// that close to.
    /// </summary>
    internal static string? SeasonPlayer(string? name, List<string> players)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var normalized = HistoricalKeys.Normalize(name);
        if (players.FirstOrDefault(p => HistoricalKeys.Normalize(p) == normalized) is { } same)
            return same;
        var key = HistoricalKeys.StripFraming(name);
        if (key.Length == 0)
            return null;
        var loose = players.Where(p => HistoricalKeys.StripFraming(p) == key).ToList();
        if (loose.Count > 0)
            return loose.Count == 1 ? loose[0] : null;
        // A short name a letter off is as likely another player's
        if (key.Length < MinCloseKeyLength)
            return null;
        var allowed = key.Length >= 15 ? 3 : key.Length >= 8 ? 2 : 1;
        var close = players.Where(p => DuplicateSuggestionService.Distance(key, HistoricalKeys.StripFraming(p), allowed) is not null && SameNumber(key, HistoricalKeys.StripFraming(p))).ToList();
        return close.Count == 1 ? close[0] : null;
    }

    /// <summary>
    /// Each ladder tab's players go on its season: the workbook's regular-season entry of that name, or (a season with no
    /// stats tab) an entry of its own, keyed by the ladder tab, with no stats. Its past finishes go with them: a player's
    /// place is the finishes' Pos (and their league and rank where the tab gives none), and a player only the finishes
    /// list is added with what they give.
    /// </summary>
    private void PutLadders()
    {
        foreach (var read in _ladders)
        {
            if (read.Players is null)
            {
                _warnings.Add($"{read.SourceId}: ladder '{read.Tab.Sheet}' not read (no such sheet, or no League header)");
                continue;
            }
            var entry = _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, read.SourceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.SeasonName.Trim(), read.Tab.Season.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                entry = new ArchiveSeason
                {
                    SeasonName = read.Tab.Season.Trim(),
                    GameVersion = read.Tab.Game,
                    League = read.Defaults?.League ?? HistoricalKeys.LateLeague,
                    Division = read.Defaults?.Division,
                    Format = read.Defaults?.Format,
                    SourceWorkbook = read.SourceId,
                    SourceSheet = read.Tab.Sheet.Trim(),
                    SourceYear = read.SourceYear,
                };
                _seasons.Add(entry);
            }
            entry.Ladder.AddRange(WithFinishes(read.Players, read.Finishes ?? []));
            if (read.Changed)
                _affected.Add(HistoricalKeys.SeasonSourceId(entry.SourceWorkbook!, entry.SourceSheet!));
        }
    }

    /// <summary>The ladder's players with their finishes (matched by gamertag, <see cref="HistoricalKeys.Normalize"/>), then
    /// the players only the finishes list. With finishes, a player's place is theirs alone.</summary>
    private static List<LadderStanding> WithFinishes(List<LadderStanding> players, List<LadderStanding> finishes)
    {
        var byGamertag = finishes.GroupBy(f => HistoricalKeys.Normalize(f.Gamertag)).ToDictionary(g => g.Key, g => g.First());
        var matched = new HashSet<string>();
        var ladder = players.Select(p =>
        {
            var key = HistoricalKeys.Normalize(p.Gamertag);
            // The finishes are the places: one they don't list has none (its order in the tab would repeat one of theirs)
            if (!byGamertag.TryGetValue(key, out var finish))
                return byGamertag.Count == 0 ? p : p with { Position = null };
            matched.Add(key);
            return p with { Position = finish.Position, League = p.League ?? finish.League, Rank = p.Rank ?? finish.Rank };
        }).ToList();
        ladder.AddRange(byGamertag.Values.Where(f => !matched.Contains(HistoricalKeys.Normalize(f.Gamertag))));
        return ladder;
    }

    /// <summary>
    /// Each awards tab's awards go on their season's regular-season entry (the first, or its <see cref="AwardsTab.Tab"/>),
    /// where the import records them: a winner as one of the season's players spells their gamertag, a team as the season
    /// names it.
    /// </summary>
    private void AddAwards()
    {
        foreach (var (sourceId, tab, awards) in _awards)
        {
            if (awards is null || awards.Count == 0)
            {
                _warnings.Add($"{sourceId}: awards '{tab.Sheet}' not read ({(awards is null ? "no such sheet" : "no awards in it")})");
                continue;
            }
            foreach (var block in awards.GroupBy(a => a.Season ?? tab.Season ?? sourceId, StringComparer.OrdinalIgnoreCase))
                AddAwards(sourceId, tab, block.Key, block.Select(a => a.Award));
        }
    }

    /// <summary>A tab's awards of the season of <paramref name="workbook"/> go on its regular-season entry.</summary>
    private void AddAwards(string sourceId, AwardsTab tab, string workbook, IEnumerable<ArchiveAward> awards)
    {
        var regular = _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase)
            && (tab.Tab is null || string.Equals(s.SourceSheet, tab.Tab, StringComparison.OrdinalIgnoreCase)));
        if (regular is null)
        {
            _warnings.Add($"{sourceId}: awards '{tab.Sheet}' not read (no season from {workbook})");
            return;
        }
        var season = _seasons.Where(s => string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase) && s.SeasonName == regular.SeasonName).ToList();
        var (teams, players) = Spellings(season, regular);
        regular.Awards.AddRange(awards.Select(a => a.IsTeam
            ? a with { Winner = KnownName(a.Winner, teams) }
            : a with
            {
                Winner = players.GetValueOrDefault(HistoricalKeys.Normalize(a.Winner)) ?? a.Winner,
                Team = a.Team is { } team ? KnownName(team, teams) : null,
            }));
        if (_changedSourceIds?.Contains(sourceId) != false)
            _affected.UnionWith(season.Select(e => HistoricalKeys.SeasonSourceId(e.SourceWorkbook!, e.SourceSheet!)));
    }

    /// <summary>The season's spelling of its teams (its stats' and its standings' so far) and of its players' gamertags (by
    /// normalised gamertag): what standings and awards put their names in.</summary>
    private static (List<string> Teams, Dictionary<string, string> Players) Spellings(List<ArchiveSeason> season, ArchiveSeason regular)
    {
        var teams = season.SelectMany(s => s.Players).Select(p => p.TeamName).OfType<string>().Concat(regular.Teams.Select(t => t.TeamName))
            .Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var players = season.SelectMany(s => s.Players).Select(p => p.Gamertag)
            .GroupBy(HistoricalKeys.Normalize).ToDictionary(g => g.Key, g => g.First());
        return (teams, players);
    }

    /// <summary>
    /// The season's name of a standings team the standings spell another way: the same letters and digits ("like
    /// clockwork!"), or a few letters off (one, two from 8 letters, three from 15) with the same digits when exactly one
    /// of the known teams is that close ("Chemical Inbalance", never "Team 10" for "Team 1"), or the same but for a note
    /// in brackets ("Puffy Grifbussy (WINNER)"). A team no one else names keeps its own name.
    /// </summary>
    private static string KnownName(string name, List<string> known) => Closest(name, known) ?? name;

    private static string? Closest(string name, List<string> known)
    {
        var key = TeamKey(name);
        if (known.FirstOrDefault(t => TeamKey(t) == key) is { } same)
            return same;
        // The same name but for a note in brackets on either ("Puffy Grifbussy" and "Puffy Grifbussy (WINNER)")
        var bare = TeamKey(StandingsParser.WithoutNote(name));
        if (known.FirstOrDefault(t => TeamKey(StandingsParser.WithoutNote(t)) == bare) is { } noted)
            return noted;
        var digits = Digits(key);
        var allowed = key.Length >= 15 ? 3 : key.Length >= 8 ? 2 : 1;
        var close = known.Where(t => Digits(TeamKey(t)) == digits && DuplicateSuggestionService.Distance(key, TeamKey(t), allowed) is not null).ToList();
        return close.Count == 1 ? close[0] : null;
    }

    /// <summary>A team name's letters and digits, lower case. Not HistoricalKeys.LooseKey: that drops a gamertag's
    /// "#1234" suffix, which in a team's name ("Team #2") is the name.</summary>
    internal static string TeamKey(string name) => new([.. name.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    private static string Digits(string key) => new([.. key.Where(char.IsDigit)]);
}
