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
/// archive-tools (now removed) compiled it: season stats tabs, the champions record, Waypoint game lists, per-game tabs and the
/// players list, each where the source's catalog entry (<see cref="HistoricalSourceConfig"/>) says. Every source is
/// parsed even when only some changed: a season is imported whole (its stats, games and titles), so the import is told
/// which seasons the changed sources touch instead.
/// </summary>
public static class HistoricalArchiveBuilder
{
    public static HistoricalArchiveBuild Build(IReadOnlyList<HistoricalSourceContent> sources, IReadOnlySet<string>? changedSourceIds = null)
    {
        var warnings = new List<string>();
        var seasons = new List<ArchiveSeason>();
        ChampionsArchive? champions = null;
        var games = new List<RecordedGameInput>();
        var players = new List<ArchivePlayerInput>();
        var waypointTabs = new List<(string SourceId, WaypointTab Tab, SheetWorkbook Workbook)>();
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var everySeason = changedSourceIds is null;
        var playersChanged = changedSourceIds is null;

        foreach (var (source, _, content) in sources)
        {
            var config = HistoricalSourceConfig.FromJson(source.Config);
            if (config is null)
                continue;
            var id = source.HistoricalSourceID;
            var changed = changedSourceIds?.Contains(id) != false;
            SheetWorkbook? workbook;
            try
            {
                workbook = SheetWorkbook.Read(content, source.CsvSheetName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add($"{id}: could not be read ({ex.Message})");
                continue;
            }
            if (workbook is null)
            {
                warnings.Add($"{id}: its snapshot is not a workbook");
                continue;
            }

            var entries = SeasonStatsParser.Parse(id, config, workbook, warnings);
            seasons.AddRange(entries);
            if (config.ChampionsSheet is not null)
            {
                if (workbook.Sheet(config.ChampionsSheet) is { } sheet)
                {
                    champions = ChampionsParser.Parse(id, source.Url, sheet);
                    everySeason |= changed;
                }
                else
                    warnings.Add($"{id}: no sheet '{config.ChampionsSheet}'");
            }
            foreach (var tab in config.Waypoint)
                waypointTabs.Add((id, tab, workbook));
            foreach (var tab in config.Games)
            {
                games.AddRange(RecordedGameParser.Parse(id, tab, workbook, warnings));
                if (changed)
                    affected.Add(tab.Season);
            }
            if (config.PlayersSheet is not null)
            {
                if (workbook.Sheet(config.PlayersSheet) is { } sheet)
                    players.AddRange(PlayersListParser.Parse(id, sheet));
                else
                    warnings.Add($"{id}: no sheet '{config.PlayersSheet}'");
                playersChanged |= changed;
            }
            if (changed)
                foreach (var entry in entries)
                    affected.Add(HistoricalKeys.SeasonSourceId(entry.SourceWorkbook!, entry.SourceSheet!));
        }

        // Waypoint games go under their season's regular-season key; a game listed as both is a playoff game
        var waypoint = new Dictionary<string, (SortedSet<string> Regular, SortedSet<string> Playoffs)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sourceId, tab, workbook) in waypointTabs)
        {
            var entries = seasons.Where(s => string.Equals(s.SourceWorkbook, tab.Season, StringComparison.OrdinalIgnoreCase)).ToList();
            var primary = entries.FirstOrDefault(e => !e.IsPlayoffs) ?? entries.FirstOrDefault();
            var sheet = workbook.Sheet(tab.Sheet);
            if (primary is null || sheet is null)
            {
                warnings.Add($"{sourceId}: Waypoint games of '{tab.Sheet}' not read ({(sheet is null ? "no such sheet" : $"no season from {tab.Season}")})");
                continue;
            }
            var key = HistoricalKeys.SeasonSourceId(primary.SourceWorkbook!, primary.SourceSheet!);
            var unparsed = new List<string>();
            var (regular, playoffs) = WaypointMatchParser.Parse(sheet, unparsed);
            warnings.AddRange(unparsed.Select(u => $"{sourceId}: a link without a match ID, {u}"));
            if (!waypoint.TryGetValue(key, out var lists))
                waypoint[key] = lists = (new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal));
            lists.Regular.UnionWith(regular);
            lists.Playoffs.UnionWith(playoffs);
            if (changedSourceIds?.Contains(sourceId) != false)
                affected.Add(key);
        }
        foreach (var lists in waypoint.Values)
            lists.Regular.ExceptWith(lists.Playoffs);

        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = seasons },
            Championships = champions,
            Catalog = new ArchiveCatalog { Sources = sources.Select(s => new CatalogSource { Id = s.Source.HistoricalSourceID, Url = s.Source.Url }).ToList() },
            WaypointMatches = waypoint.Where(w => w.Value.Regular.Count > 0 || w.Value.Playoffs.Count > 0)
                .ToDictionary(w => w.Key, w => new WaypointSeasonMatches { Regular = [.. w.Value.Regular], Playoffs = [.. w.Value.Playoffs] }),
            RecordedGames = games.Count > 0 ? games : null,
            // The players list goes along when it changed (it is matched against every season's placeholders)
            Players = playersChanged && players.Count > 0 ? players : null,
        };
        return new HistoricalArchiveBuild { Input = input, Warnings = warnings, Seasons = everySeason ? null : affected };
    }
}
