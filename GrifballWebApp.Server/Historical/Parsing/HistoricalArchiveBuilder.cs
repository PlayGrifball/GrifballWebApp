using GrifballWebApp.Server.Historical.Sources;

namespace GrifballWebApp.Server.Historical.Parsing;

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
public sealed class HistoricalArchiveBuilder
{
    private readonly IReadOnlySet<string>? _changedSourceIds;
    private readonly List<string> _warnings = [];
    private readonly List<ArchiveSeason> _seasons = [];
    private ChampionsArchive? _champions;
    private readonly List<RecordedGameInput> _games = [];
    private readonly List<ArchivePlayerInput> _players = [];
    private readonly List<CatalogSource> _catalog = [];
    /// <summary>Each Waypoint tab's games, parsed as its source is read; put under their season's key once every season is known.</summary>
    private readonly List<(string SourceId, WaypointTab Tab, (List<string> Regular, List<string> Playoffs, List<string> Teams, List<string> Unparsed)? Games)> _waypoint = [];
    /// <summary>Rosters of seasons whose stats are in another source, applied once every season is known.</summary>
    private readonly List<(string SourceId, RosterTab Tab, Dictionary<string, string>? Teams)> _rosters = [];
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
        _catalog.Add(new CatalogSource { Id = source.HistoricalSourceID, Url = source.Url });
        var config = HistoricalSourceConfig.FromJson(source.Config);
        if (config is null)
            return;
        var id = source.HistoricalSourceID;
        var changed = _changedSourceIds?.Contains(id) != false;
        SheetWorkbook? workbook;
        try
        {
            var sheets = config.SheetNames();
            workbook = SheetWorkbook.Read(content.Content, source.CsvSheetName, name => sheets.Contains(name.Trim()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _warnings.Add($"{id}: could not be read ({ex.Message})");
            return;
        }
        if (workbook is null)
        {
            _warnings.Add($"{id}: its snapshot is not a workbook");
            return;
        }

        var entries = SeasonStatsParser.Parse(id, config, workbook, _warnings);
        entries.AddRange(StatsTableParser.Parse(id, config, workbook, _warnings));
        foreach (var entry in entries)
            entry.SourceYear ??= source.Year;
        _seasons.AddRange(entries);
        if (config.ChampionsSheet is not null)
        {
            if (workbook.Sheet(config.ChampionsSheet) is { } sheet)
            {
                _champions = ChampionsParser.Parse(id, source.Url, sheet);
                _everySeason |= changed;
            }
            else
                _warnings.Add($"{id}: no sheet '{config.ChampionsSheet}'");
        }
        foreach (var tab in config.Waypoint)
        {
            if (workbook.Sheet(tab.Sheet) is { } sheet)
            {
                var unparsed = new List<string>();
                var (regular, playoffs, teams) = WaypointMatchParser.Parse(sheet, unparsed);
                _waypoint.Add((id, tab, ([.. regular], [.. playoffs], [.. teams], unparsed)));
            }
            else
                _waypoint.Add((id, tab, null));
        }
        foreach (var tab in config.Rosters)
            _rosters.Add((id, tab, workbook.Sheet(tab.Sheet) is { } rosterSheet ? RosterParser.Parse(rosterSheet) : null));
        foreach (var tab in config.Games)
        {
            _games.AddRange(RecordedGameParser.Parse(id, tab, workbook, _warnings));
            if (changed)
                _affected.Add(tab.Season);
        }
        if (config.PlayersSheet is not null)
        {
            if (workbook.Sheet(config.PlayersSheet) is { } sheet)
                _players.AddRange(PlayersListParser.Parse(id, sheet));
            else
                _warnings.Add($"{id}: no sheet '{config.PlayersSheet}'");
            _playersChanged |= changed;
        }
        if (changed)
            foreach (var entry in entries)
                _affected.Add(HistoricalKeys.SeasonSourceId(entry.SourceWorkbook!, entry.SourceSheet!));
    }

    public HistoricalArchiveBuild Finish()
    {
        // Waypoint games go under their season's regular-season key; a game listed as both is a playoff game
        var waypoint = new Dictionary<string, (SortedSet<string> Regular, SortedSet<string> Playoffs)>(StringComparer.OrdinalIgnoreCase);
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
                waypoint[key] = lists = (new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal));
            lists.Regular.UnionWith(games.Value.Regular);
            lists.Playoffs.UnionWith(games.Value.Playoffs);
            if (games.Value.Teams.Count > 0)
            {
                foreach (var team in games.Value.Teams)
                {
                    if (!primary.Teams.Contains(team, StringComparer.OrdinalIgnoreCase))
                        primary.Teams.Add(team);
                }
            }
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.Add(key);
        }
        foreach (var lists in waypoint.Values)
            lists.Regular.ExceptWith(lists.Playoffs);

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

        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = _seasons },
            Championships = _champions,
            Catalog = new ArchiveCatalog { Sources = _catalog },
            WaypointMatches = waypoint.Where(w => w.Value.Regular.Count > 0 || w.Value.Playoffs.Count > 0)
                .ToDictionary(w => w.Key, w => new WaypointSeasonMatches { Regular = [.. w.Value.Regular], Playoffs = [.. w.Value.Playoffs] }),
            RecordedGames = _games.Count > 0 ? _games : null,
            // The players list goes along when it changed (it is matched against every season's placeholders)
            Players = _playersChanged && _players.Count > 0 ? _players : null,
        };
        return new HistoricalArchiveBuild { Input = input, Warnings = _warnings, Seasons = _everySeason ? null : _affected };
    }
}
