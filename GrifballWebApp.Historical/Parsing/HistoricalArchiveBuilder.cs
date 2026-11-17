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
    private ChampionsArchive? _champions;
    private readonly List<RecordedGameInput> _games = [];
    private readonly List<ArchivePlayerInput> _players = [];
    private readonly List<CatalogSource> _catalog = [];
    /// <summary>Each Waypoint tab's games, parsed as its source is read; put under their season's key once every season is known.</summary>
    private readonly List<(string SourceId, WaypointTab Tab, (List<string> Regular, List<string> Playoffs, List<ScheduledGame> Schedule, List<string> Unparsed)? Games)> _waypoint = [];
    /// <summary>Rosters of seasons whose stats are in another source, applied once every season is known.</summary>
    private readonly List<(string SourceId, RosterTab Tab, Dictionary<string, string>? Teams)> _rosters = [];
    /// <summary>Each standings tab's teams, put on their season's regular season once every season is known.</summary>
    private readonly List<(string SourceId, StandingsTab Tab, List<ArchiveTeam>? Teams)> _standings = [];
    /// <summary>Each result list's games, put under their season once every season is known.</summary>
    private readonly List<(string SourceId, int? Year, SeasonDefaults? Defaults, ResultsTab Tab, List<RecordedGameInput>? Games)> _results = [];
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
        if (ReadWorkbook(content, config) is not { } workbook)
            return;

        var entries = SeasonStatsParser.Parse(id, config, workbook, _warnings);
        entries.AddRange(StatsTableParser.Parse(id, config, workbook, _warnings));
        foreach (var entry in entries)
            entry.SourceYear ??= source.Year;
        _seasons.AddRange(entries);
        if (config.ChampionsSheet is not null)
            AddChampions(id, source.Url, config.ChampionsSheet, workbook, changed);
        AddWaypoint(id, config, workbook);
        AddSeasonTabs(id, source.Year, config, workbook);
        AddGames(id, config, workbook, changed);
        if (config.PlayersSheet is not null)
            AddPlayers(id, config.PlayersSheet, workbook, changed);
        if (changed)
            foreach (var entry in entries)
                _affected.Add(HistoricalKeys.SeasonSourceId(entry.SourceWorkbook!, entry.SourceSheet!));
    }

    /// <summary>The tabs of a season put on it once every season is known: rosters, standings and result lists.</summary>
    private void AddSeasonTabs(string id, int? year, HistoricalSourceConfig config, SheetWorkbook workbook)
    {
        foreach (var tab in config.Rosters)
            _rosters.Add((id, tab, workbook.Sheet(tab.Sheet) is { } rosterSheet ? RosterParser.Parse(rosterSheet) : null));
        foreach (var tab in config.Standings)
            _standings.Add((id, tab, workbook.Sheet(tab.Sheet) is { } standingsSheet ? StandingsParser.Parse(standingsSheet, tab) : null));
        foreach (var tab in config.Results)
            _results.Add((id, year, config.Season, tab, workbook.Sheet(tab.Sheet) is { } resultsSheet ? GameResultsParser.Parse(id, tab, resultsSheet, _warnings) : null));
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

    private void AddChampions(string id, string url, string sheetName, SheetWorkbook workbook, bool changed)
    {
        if (workbook.Sheet(sheetName) is { } sheet)
        {
            _champions = ChampionsParser.Parse(id, url, sheet);
            _everySeason |= changed;
        }
        else
            _warnings.Add($"{id}: no sheet '{sheetName}'");
    }

    private void AddWaypoint(string id, HistoricalSourceConfig config, SheetWorkbook workbook)
    {
    foreach (var tab in config.Waypoint)
    {
        if (workbook.Sheet(tab.Sheet) is { } sheet)
        {
            var unparsed = new List<string>();
            var (regular, playoffs, schedule) = WaypointMatchParser.Parse(sheet, unparsed);
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

    private void AddPlayers(string id, string sheetName, SheetWorkbook workbook, bool changed)
    {
        if (workbook.Sheet(sheetName) is { } sheet)
            _players.AddRange(PlayersListParser.Parse(id, sheet));
        else
            _warnings.Add($"{id}: no sheet '{sheetName}'");
        _playersChanged |= changed;
    }

    public HistoricalArchiveBuild Finish()
    {
        var waypoint = WaypointBySeason();
        FillTeamsFromRosters();
        AddStandings();
        AddResults();
        NameGameSides();

        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = _seasons },
            Championships = _champions,
            Catalog = new ArchiveCatalog { Sources = _catalog },
            WaypointMatches = waypoint.Where(w => w.Value.Regular.Count > 0 || w.Value.Playoffs.Count > 0)
                .ToDictionary(w => w.Key, w => new WaypointSeasonMatches { Regular = [.. w.Value.Regular], Playoffs = [.. w.Value.Playoffs], Schedule = [.. w.Value.Schedule.Values.OrderBy(g => g.MatchId, StringComparer.Ordinal)] }),
            RecordedGames = _games.Count > 0 ? _games : null,
            // The players list goes along when it changed (it is matched against every season's placeholders)
            Players = _playersChanged && _players.Count > 0 ? _players : null,
        };
        return new HistoricalArchiveBuild { Input = input, Warnings = _warnings, Seasons = _everySeason ? null : _affected };
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

    /// <summary>Each standings tab's teams go on its season's regular-season entry (the first, or its <see cref="StandingsTab.Tab"/>),
    /// where the import reads a team's record and placing.</summary>
    private void AddStandings()
    {
        foreach (var (sourceId, tab, teams) in _standings)
        {
            var workbook = tab.Season ?? sourceId;
            var regular = _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase)
                && (tab.Tab is null || string.Equals(s.SourceSheet, tab.Tab, StringComparison.OrdinalIgnoreCase)));
            if (teams is null || regular is null)
            {
                _warnings.Add($"{sourceId}: standings '{tab.Sheet}' not read ({(teams is null ? "no such sheet, or no table" : $"no season from {workbook}")})");
                continue;
            }
            var season = _seasons.Where(s => string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase) && s.SeasonName == regular.SeasonName).ToList();
            // The season's names of its teams: its stats', and an earlier standings tab's (two tabs of a season spell a team
            // alike, the stats naming none)
            var known = season.SelectMany(s => s.Players).Select(p => p.TeamName).OfType<string>().Concat(regular.Teams.Select(t => t.TeamName))
                .Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // A captain is one of the season's players: a nickname ("Team (Snipez)") would be a placeholder player
            var players = season.SelectMany(s => s.Players).Select(p => p.Gamertag)
                .GroupBy(HistoricalKeys.Normalize).ToDictionary(g => g.Key, g => g.First());
            var named = teams.Select(t => t with
            {
                TeamName = KnownName(t.TeamName, known),
                Captain = t.Captain is { } captain ? players.GetValueOrDefault(HistoricalKeys.Normalize(captain)) : null,
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
