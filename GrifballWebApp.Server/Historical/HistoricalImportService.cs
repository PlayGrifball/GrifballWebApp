using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using System.Text.Json;

namespace GrifballWebApp.Server.Historical;

public interface IHistoricalImportService
{
    /// <summary>
    /// Imports the historical archive: its seasons, players' season stats and championships. Idempotent: a season is
    /// found again by its HistoricalSourceID ("{sourceWorkbook}/{sourceSheet}"), a stat row by (SeasonID, UserID,
    /// StintIndex, IsPlayoffs) and a title by (SeasonID, Tier, UserID), so a re-run updates rows in place and adds none.
    /// Each season is one transaction. <paramref name="progress"/> hears what it is doing.
    /// </summary>
    Task<HistoricalImportReport> Import(HistoricalArchiveInput input, Func<HistoricalImportProgress, Task>? progress = null, CancellationToken ct = default);
}

public record HistoricalImportProgress(string Stage, int SeasonsDone, int SeasonsTotal);

public class HistoricalImportOptions
{
    /// <summary>Attempts at pulling one match (a failed call, after the Halo client's own retries of 429 and 5xx).</summary>
    public int MatchPullAttempts { get; set; } = 3;
    /// <summary>The first wait between attempts, doubled after each.</summary>
    public TimeSpan MatchPullBackoff { get; set; } = TimeSpan.FromSeconds(5);
}

public class HistoricalImportReport
{
    public int SeasonsCreated { get; set; }
    public int SeasonsUpdated { get; set; }
    public int StatsInserted { get; set; }
    public int StatsUpdated { get; set; }
    public int WaypointStatsInserted { get; set; }
    public int WaypointStatsUpdated { get; set; }
    /// <summary>Manual rows not imported, or removed, because the player has Waypoint stats for that phase of the season.</summary>
    public int ManualReplacedByWaypoint { get; set; }
    public int ChampionshipsInserted { get; set; }
    public int ChampionshipsUpdated { get; set; }
    public int DummyUsersCreated { get; set; }
    public int WaypointMatchesPulled { get; set; }
    /// <summary>Roster matches suggested for a sysadmin to decide (GamertagClaims).</summary>
    public int SuggestionsCreated { get; set; }
    /// <summary>Waypoint matches that are not in the database (could not be pulled, or not found), by season and phase:
    /// that phase kept its Manual stats.</summary>
    public List<string> MissingMatches { get; set; } = [];
    /// <summary>Why something was left out, one line each.</summary>
    public List<string> Skipped { get; set; } = [];
    /// <summary>What was imported but deserves a look, e.g. a sheet row of a Waypoint season no account matched.</summary>
    public List<string> Warnings { get; set; } = [];
    /// <summary>Seasons whose transaction failed and was rolled back.</summary>
    public List<string> Errors { get; set; } = [];
}

/// <inheritdoc/>
public class HistoricalImportService : IHistoricalImportService
{
    /// <summary>The league the champions record is of.</summary>
    private const string ChampionsLeagueSlug = "agla";
    /// <summary>A season's date when its name has no year: the first AGLA season (the Original 8 tournament) was in 2008.</summary>
    private const int FallbackYear = 2008;
    /// <summary>The champions record's workbook (catalog ID), when the file doesn't name it.</summary>
    private const string DefaultChampionsSourceId = "history-agla-champions";

    private readonly GrifballContext _context;
    private readonly IDataPullService _dataPullService;
    private readonly HistoricalImportOptions _options;
    private readonly ILogger<HistoricalImportService> _logger;

    public HistoricalImportService(GrifballContext context, IDataPullService dataPullService, IOptions<HistoricalImportOptions> options, ILogger<HistoricalImportService> logger)
    {
        _context = context;
        _dataPullService = dataPullService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>A season to import: its archive entries (regular season and playoffs), titles and Waypoint matches.</summary>
    private sealed class SeasonGroup
    {
        public string SourceId { get; set; } = "";
        public string? SourceUrl { get; set; }
        public required string SeasonName { get; init; }
        public required League League { get; init; }
        public GameVersion? GameVersion { get; set; }
        public string? Division { get; set; }
        public List<ArchiveSeason> Entries { get; } = [];
        public List<ArchiveChampionship> Championships { get; } = [];
        public WaypointSeasonMatches? Waypoint { get; set; }
    }

    private enum UpsertResult { Inserted, Updated, KeptWaypoint }

    private sealed record SheetPlayer(int UserId, string Gamertag, string? TeamName);

    public async Task<HistoricalImportReport> Import(HistoricalArchiveInput input, Func<HistoricalImportProgress, Task>? progress = null, CancellationToken ct = default)
    {
        progress ??= _ => Task.CompletedTask;
        var report = new HistoricalImportReport();
        var leagues = await _context.Leagues.ToListAsync(ct);
        var gameVersions = await _context.GameVersions.ToListAsync(ct);
        var urls = (input.Catalog?.Sources ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Url!.Trim(), StringComparer.OrdinalIgnoreCase);

        // A season's entries (regular season, playoffs) share its league and name
        var groups = new Dictionary<string, SeasonGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in input.Seasons.Seasons)
        {
            var league = leagues.FirstOrDefault(l => string.Equals(l.LeagueName, entry.League, StringComparison.OrdinalIgnoreCase)
                || string.Equals(l.Slug, entry.League, StringComparison.OrdinalIgnoreCase));
            if (league is null)
            {
                Skip(report, $"Season {entry.SeasonName}: unknown league '{entry.League}'");
                continue;
            }
            var gameVersion = gameVersions.FirstOrDefault(g => string.Equals(g.Slug, entry.GameVersion, StringComparison.OrdinalIgnoreCase));
            if (gameVersion is null)
            {
                Skip(report, $"Season {entry.SeasonName}: unknown game version '{entry.GameVersion}'");
                continue;
            }
            var nameKey = HistoricalKeys.FallbackSeasonSourceId(league.Slug, entry.SeasonName);
            if (!groups.TryGetValue(nameKey, out var group))
            {
                group = new SeasonGroup { SeasonName = entry.SeasonName.Trim(), League = league, GameVersion = gameVersion, Division = entry.Division };
                groups.Add(nameKey, group);
            }
            group.Entries.Add(entry);
        }

        // A season is keyed by the workbook and sheet of its regular season (its first entry otherwise), so a name
        // corrected in the archive still finds it
        foreach (var (nameKey, group) in groups)
        {
            var primary = group.Entries.FirstOrDefault(e => !e.IsPlayoffs) ?? group.Entries[0];
            group.SourceId = EntrySourceId(primary) ?? nameKey;
            if (EntrySourceId(primary) is null)
                Warn(report, $"Season {group.SeasonName}: no source workbook and sheet, keyed {nameKey}");
            if (primary.SourceWorkbook is not null)
                group.SourceUrl = urls.GetValueOrDefault(primary.SourceWorkbook.Trim());
        }

        await GroupChampionships(input.Championships, groups, leagues, gameVersions, ct);

        // Waypoint matches can be keyed by any of a season's entries
        var bySourceId = new Dictionary<string, SeasonGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.Values)
        {
            bySourceId.TryAdd(group.SourceId, group);
            foreach (var id in group.Entries.Select(EntrySourceId).OfType<string>())
                bySourceId.TryAdd(id, group);
        }
        foreach (var (sourceId, matches) in input.WaypointMatches ?? [])
        {
            if (bySourceId.TryGetValue(sourceId, out var group) && group.GameVersion?.Slug == "infinite")
                group.Waypoint = matches;
            else
                Skip(report, $"Waypoint matches for {sourceId}: no Halo Infinite season with that key in the archive");
        }

        // Matches come from the Halo Infinite API before any season's transaction: no network call holds one open.
        await PullWaypointMatches(groups.Values, report, progress, groups.Count, ct);

        var done = 0;
        foreach (var group in groups.Values)
        {
            ct.ThrowIfCancellationRequested();
            await progress(new HistoricalImportProgress($"Importing {group.SeasonName}", done, groups.Count));
            await ImportSeason(group, gameVersions, report, ct);
            done++;
        }
        await progress(new HistoricalImportProgress("Done", done, groups.Count));

        _logger.LogInformation("Historical import: {SeasonsCreated} seasons created, {SeasonsUpdated} updated, {StatsInserted} stats added, {StatsUpdated} updated, {DummyUsersCreated} placeholder users, {Skipped} skipped, {Errors} failed",
            report.SeasonsCreated, report.SeasonsUpdated, report.StatsInserted, report.StatsUpdated, report.DummyUsersCreated, report.Skipped.Count, report.Errors.Count);
        return report;
    }

    /// <summary>"{sourceWorkbook}/{sourceSheet}", or the archive's own key; null when the entry names neither.</summary>
    private static string? EntrySourceId(ArchiveSeason entry)
        => entry.HistoricalSourceId
            ?? (string.IsNullOrWhiteSpace(entry.SourceWorkbook) || string.IsNullOrWhiteSpace(entry.SourceSheet)
                ? null
                : HistoricalKeys.SeasonSourceId(entry.SourceWorkbook, entry.SourceSheet));

    /// <summary>
    /// Each title goes to its season: "{season} ({tier})" or "{season}" among the archive's seasons of the league (or
    /// historical seasons already imported). A title whose season the archive has no stats for gets a historical season
    /// of its own, keyed "{champions workbook}/{season}" and named as the record names it, so no title is lost.
    /// </summary>
    private async Task GroupChampionships(ChampionsArchive? championships, Dictionary<string, SeasonGroup> groups, List<League> leagues, List<GameVersion> gameVersions, CancellationToken ct)
    {
        if (championships is null || championships.Champions.Count == 0)
            return;
        var league = leagues.Single(l => l.Slug == ChampionsLeagueSlug);
        var workbook = string.IsNullOrWhiteSpace(championships.SourceId) ? DefaultChampionsSourceId : championships.SourceId.Trim();
        var imported = await _context.Seasons.Where(s => s.IsHistorical && s.LeagueID == league.LeagueID)
            .Select(s => new { s.HistoricalSourceID, s.SeasonName }).ToListAsync(ct);

        foreach (var championship in championships.Champions)
        {
            var names = new[] { $"{championship.Season} ({championship.Tier})", championship.Season };
            var nameKeys = names.Select(n => HistoricalKeys.FallbackSeasonSourceId(league.Slug, n)).ToArray();
            var group = nameKeys.Select(k => groups.GetValueOrDefault(k)).FirstOrDefault(g => g is not null);
            if (group is null)
            {
                var sourceId = HistoricalKeys.SeasonSourceId(workbook, championship.Season);
                var existing = imported.FirstOrDefault(s => string.Equals(s.HistoricalSourceID, sourceId, StringComparison.OrdinalIgnoreCase))
                    ?? names.Select(n => imported.FirstOrDefault(s => string.Equals(s.SeasonName, n.Trim(), StringComparison.OrdinalIgnoreCase))).FirstOrDefault(s => s is not null);
                var slug = HistoricalKeys.GameVersionSlug(championship.Game);
                group = new SeasonGroup
                {
                    SourceId = existing?.HistoricalSourceID ?? sourceId,
                    SourceUrl = championships.SourceUrl,
                    SeasonName = existing?.SeasonName ?? championship.Season.Trim(),
                    League = league,
                    GameVersion = gameVersions.FirstOrDefault(g => g.Slug == slug),
                };
                groups.Add(nameKeys[1], group);
            }
            group.Championships.Add(championship);
        }
    }

    /// <summary>
    /// Pulls the Waypoint matches not in the database yet through the data pull service. The Halo client retries 429s
    /// and 5xx itself (its Polly policy, honouring Retry-After); a call that still fails is tried again here with
    /// exponential backoff. A match the API doesn't return is missing, which its season's import reports.
    /// </summary>
    private async Task PullWaypointMatches(IEnumerable<SeasonGroup> groups, HistoricalImportReport report, Func<HistoricalImportProgress, Task> progress, int seasons, CancellationToken ct)
    {
        var ids = groups.Where(g => g.Waypoint is not null)
            .SelectMany(g => WaypointIds(g, report).Keys)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return;
        var have = await _context.Matches.Where(m => ids.Contains(m.MatchID)).Select(m => m.MatchID).ToListAsync(ct);
        var toPull = ids.Except(have).ToList();
        var retry = Policy.Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(Math.Max(_options.MatchPullAttempts - 1, 0),
                attempt => TimeSpan.FromTicks(_options.MatchPullBackoff.Ticks * (1L << (attempt - 1))),
                async (ex, delay, attempt, _) =>
                {
                    _logger.LogWarning(ex, "Pulling a Waypoint match failed (attempt {Attempt}); trying again in {Delay}", attempt, delay);
                    // A heartbeat before each wait too, so a slow pull doesn't look like a stopped job
                    await progress(new HistoricalImportProgress($"Pulling Waypoint matches: retrying in {delay}", 0, seasons));
                });
        var pulled = 0;
        foreach (var id in toPull)
        {
            ct.ThrowIfCancellationRequested();
            // Each pull reports (the job's heartbeat): the longest gap is one call with the Halo client's retries
            await progress(new HistoricalImportProgress($"Pulling Waypoint matches {pulled}/{toPull.Count}", 0, seasons));
            pulled++;
            try
            {
                await retry.ExecuteAsync(_ => _dataPullService.GetAndSaveMatch(id), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not pull Waypoint match {MatchID}", id);
                Skip(report, $"Waypoint match {id}: could not be pulled ({ex.Message})");
                _context.ChangeTracker.Clear();
                continue;
            }
            // GetAndSaveMatch saves nothing for a match the API doesn't return
            if (await _context.Matches.AnyAsync(m => m.MatchID == id, ct))
                report.WaypointMatchesPulled++;
            else
                Skip(report, $"Waypoint match {id}: not found");
        }
    }

    /// <summary>The season's Waypoint match IDs, each with whether it is a playoff match.</summary>
    private Dictionary<Guid, bool> WaypointIds(SeasonGroup group, HistoricalImportReport? report)
    {
        var ids = new Dictionary<Guid, bool>();
        foreach (var (values, playoffs) in new[] { (group.Waypoint!.Regular, false), (group.Waypoint.Playoffs, true) })
        {
            foreach (var value in values)
            {
                var id = HistoricalKeys.MatchId(value);
                if (id is null)
                {
                    if (report is not null)
                        Skip(report, $"{group.SourceId}: '{value}' is not a match ID or Waypoint match URL");
                    continue;
                }
                ids[id.Value] = playoffs;
            }
        }
        return ids;
    }

    private async Task ImportSeason(SeasonGroup group, List<GameVersion> gameVersions, HistoricalImportReport report, CancellationToken ct)
    {
        var counts = new HistoricalImportReport();
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var season = await UpsertSeason(group, counts, ct);
            if (season is null)
            {
                await transaction.RollbackAsync(ct);
                report.Skipped.AddRange(counts.Skipped);
                return;
            }

            var resolver = new PlayerResolver(_context, season.SeasonID, season.SeasonStart);
            var waypointUsers = group.Waypoint is null
                ? new Dictionary<bool, HashSet<int>> { [false] = [], [true] = [] }
                : await ImportWaypointStats(group, season, resolver, counts, ct);
            var sheetPlayers = await ImportManualStats(group, season, waypointUsers, resolver, counts, ct);
            if (group.Waypoint is not null)
                await SuggestRosterMatches(group, season, waypointUsers, sheetPlayers, counts, ct);
            await ImportChampionships(group, season, gameVersions, resolver, counts, ct);
            foreach (var gamertag in resolver.Ambiguous.Distinct(StringComparer.OrdinalIgnoreCase))
                Warn(counts, $"{group.SourceId}: {gamertag} is in more than one Xbox account's gamertag history for the season; left to its placeholder");

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            counts.DummyUsersCreated = resolver.DummiesCreated;
            Add(report, counts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Historical import of {Season} failed and was rolled back", group.SourceId);
            report.Errors.Add($"{group.SourceId}: {ex.GetBaseException().Message}");
        }
    }

    private async Task<Season?> UpsertSeason(SeasonGroup group, HistoricalImportReport counts, CancellationToken ct)
    {
        var season = await _context.Seasons.FirstOrDefaultAsync(s => s.HistoricalSourceID == group.SourceId, ct);
        if (season is null)
        {
            // A season of the app's own (or one imported under another key) with that name in the league
            var taken = await _context.Seasons.AnyAsync(s => s.LeagueID == group.League.LeagueID && s.SeasonName == group.SeasonName, ct);
            if (taken)
            {
                Skip(counts, $"Season {group.SeasonName} ({group.League.LeagueName}): the league already has a season with that name that is not {group.SourceId}");
                return null;
            }
            season = new Season { HistoricalSourceID = group.SourceId, IsHistorical = true, PublicAt = null };
            _context.Seasons.Add(season);
            counts.SeasonsCreated++;
        }
        else
        {
            counts.SeasonsUpdated++;
        }
        // Only the year is known: the season spans it, the window its gamertags are matched for
        var start = new DateTime(HistoricalKeys.Year(group.SeasonName) ?? FallbackYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        season.SignupsOpen = season.SignupsClose = season.DraftStart = season.SeasonStart = start;
        season.SeasonEnd = start.AddYears(1).AddSeconds(-1);
        season.SeasonName = group.SeasonName;
        season.LeagueID = group.League.LeagueID;
        season.GameVersionID = group.GameVersion?.GameVesionID;
        season.Division = group.Division;
        season.HistoricalSourceUrl = group.SourceUrl;
        await _context.SaveChangesAsync(ct);
        return season;
    }

    /// <summary>
    /// Stats derived from the season's Waypoint matches (pulled into Matches / MatchParticipants), one row per player
    /// and phase, Source Waypoint, for each phase (regular season, playoffs) whose listed matches are all in the
    /// database: they replace the player's Manual rows for that phase. A phase with a match missing keeps its Manual
    /// rows, and the missing matches are reported. Returns the users with Waypoint stats, by phase (true: playoffs).
    /// </summary>
    private async Task<Dictionary<bool, HashSet<int>>> ImportWaypointStats(SeasonGroup group, Season season, PlayerResolver resolver, HistoricalImportReport counts, CancellationToken ct)
    {
        var users = new Dictionary<bool, HashSet<int>> { [false] = [], [true] = [] };
        var matchPhases = WaypointIds(group, null);
        var ids = matchPhases.Keys.ToList();
        var present = (await _context.Matches.Where(m => ids.Contains(m.MatchID)).Select(m => m.MatchID).ToListAsync(ct)).ToHashSet();
        var completePhases = new HashSet<bool>();
        foreach (var phase in new[] { false, true })
        {
            var listed = matchPhases.Where(m => m.Value == phase).Select(m => m.Key).ToList();
            if (listed.Count == 0)
                continue;
            var missing = listed.Where(id => !present.Contains(id)).ToList();
            if (missing.Count == 0)
                completePhases.Add(phase);
            else
                counts.MissingMatches.Add($"{group.SourceId} {Phase(phase)}: {missing.Count} of {listed.Count} matches missing ({string.Join(", ", missing)}); kept the sheet's totals");
        }
        if (completePhases.Count == 0)
            return users;

        var completeIds = matchPhases.Where(m => completePhases.Contains(m.Value)).Select(m => m.Key).ToList();
        var participants = await _context.MatchParticipants
            .Where(p => completeIds.Contains(p.MatchID))
            .Select(p => new
            {
                p.MatchID,
                p.XboxUserID,
                p.XboxUser.Gamertag,
                p.MatchTeam.Outcome,
                p.Kills,
                p.Deaths,
                p.Assists,
                p.Betrayals,
                p.PersonalScore,
                p.DamageDealt,
                p.DamageTaken,
                p.TimePlayed,
                p.AverageLife,
            })
            .ToListAsync(ct);

        var rows = new List<PlayerSeasonStat>();
        foreach (var player in participants.GroupBy(p => (p.XboxUserID, Playoffs: matchPhases[p.MatchID])))
        {
            var gamertag = player.First().Gamertag;
            var userId = (await resolver.Resolve(gamertag, player.Key.XboxUserID, ct)).UserId;
            var manual = group.Entries.Where(e => e.IsPlayoffs == player.Key.Playoffs)
                .SelectMany(e => e.Players)
                .FirstOrDefault(p => HistoricalKeys.Normalize(p.Gamertag) == HistoricalKeys.Normalize(gamertag));
            var games = player.Count();
            var wins = player.Count(p => p.Outcome == Outcomes.Won);
            var kills = player.Sum(p => p.Kills);
            var deaths = player.Sum(p => p.Deaths);
            rows.Add(new PlayerSeasonStat
            {
                SeasonID = season.SeasonID,
                UserID = userId,
                GameVersionID = group.GameVersion!.GameVesionID,
                Gamertag = HistoricalKeys.CleanGamertag(gamertag),
                TeamName = manual?.TeamName,
                Division = group.Division,
                StintIndex = 1,
                IsPlayoffs = player.Key.Playoffs,
                Source = StatSource.Waypoint,
                GamesPlayed = games,
                Wins = wins,
                Losses = player.Count(p => p.Outcome == Outcomes.Lost),
                WinPercent = games == 0 ? null : Math.Round(100m * wins / games, 3),
                Kills = kills,
                Deaths = deaths,
                Assists = player.Sum(p => p.Assists),
                Betrayals = player.Sum(p => p.Betrayals),
                KdRatio = deaths == 0 ? null : Math.Round((decimal)kills / deaths, 3),
                KdSpread = kills - deaths,
                AverageLifeSeconds = Math.Round((decimal)player.Average(p => p.AverageLife.TotalSeconds), 2),
                TotalSecondsPlayed = (int)Math.Round(player.Sum(p => p.TimePlayed.TotalSeconds)),
                Score = player.Sum(p => p.PersonalScore),
                DamageDealt = player.Sum(p => p.DamageDealt),
                DamageTaken = player.Sum(p => p.DamageTaken),
            });
            users[player.Key.Playoffs].Add(userId);
        }

        // Waypoint replaces Manual for the phase: a player's phase of a season is never both
        foreach (var phase in completePhases)
        {
            var phaseUsers = users[phase];
            var replaced = await _context.PlayerSeasonStats
                .Where(s => s.SeasonID == season.SeasonID && s.IsPlayoffs == phase && s.Source == StatSource.Manual && phaseUsers.Contains(s.UserID))
                .ToListAsync(ct);
            // Counted below, once per sheet row (which then is skipped)
            _context.PlayerSeasonStats.RemoveRange(replaced);
        }
        await _context.SaveChangesAsync(ct);

        foreach (var row in rows)
        {
            if (await Upsert(row, ct) == UpsertResult.Inserted)
                counts.WaypointStatsInserted++;
            else
                counts.WaypointStatsUpdated++;
        }
        return users;
    }

    /// <summary>The sheet's rows. Returns, by phase, each row's player (for roster suggestions).</summary>
    private async Task<Dictionary<bool, List<SheetPlayer>>> ImportManualStats(SeasonGroup group, Season season, Dictionary<bool, HashSet<int>> waypointUsers, PlayerResolver resolver, HistoricalImportReport counts, CancellationToken ct)
    {
        var sheetPlayers = new Dictionary<bool, List<SheetPlayer>> { [false] = [], [true] = [] };
        var seen = new HashSet<(int UserID, int StintIndex, bool IsPlayoffs)>();
        foreach (var entry in group.Entries)
        {
            foreach (var player in entry.Players)
            {
                if (string.IsNullOrWhiteSpace(player.Gamertag))
                    continue;
                // Renamed players are found through their gamertag history, so their Waypoint row isn't joined by a
                // second, Manual one under the old gamertag
                var userId = (await resolver.Resolve(player.Gamertag, null, ct)).UserId;
                sheetPlayers[entry.IsPlayoffs].Add(new SheetPlayer(userId, HistoricalKeys.CleanGamertag(player.Gamertag), player.TeamName));
                if (waypointUsers[entry.IsPlayoffs].Contains(userId))
                {
                    counts.ManualReplacedByWaypoint++;
                    continue;
                }
                var stint = Math.Max(player.StintIndex, 1);
                if (!seen.Add((userId, stint, entry.IsPlayoffs)))
                {
                    Skip(counts, $"{group.SourceId}: {player.Gamertag} is a second row for the same player, stint {stint}{(entry.IsPlayoffs ? " (playoffs)" : "")}");
                    continue;
                }
                var row = new PlayerSeasonStat
                {
                    SeasonID = season.SeasonID,
                    UserID = userId,
                    GameVersionID = group.GameVersion!.GameVesionID,
                    Gamertag = HistoricalKeys.CleanGamertag(player.Gamertag),
                    TeamName = player.TeamName,
                    Division = entry.Division,
                    TradeNote = player.TradeNote,
                    RoleNote = player.RoleNote,
                    StintIndex = stint,
                    IsPlayoffs = entry.IsPlayoffs,
                    IsCombined = entry.IsCombined,
                    Source = StatSource.Manual,
                    GamesPlayed = Int(player.GamesPlayed),
                    Wins = Int(player.Wins),
                    Losses = Int(player.Losses),
                    WinPercent = Dec(player.WinPercent, 3),
                    Goals = Int(player.Goals),
                    Kills = Int(player.Kills),
                    Deaths = Int(player.Deaths),
                    Assists = Int(player.Assists),
                    KdRatio = Dec(player.KdRatio, 3),
                    KdSpread = IntOrNull(player.KdSpread),
                    CarrierKills = Int(player.CarrierKills),
                    AverageLifeSeconds = Dec(player.AverageLifeSeconds, 2),
                    TotalSecondsPlayed = IntOrNull(player.TotalSecondsPlayed),
                    Sprees = Int(player.Sprees),
                    MultiKills = Int(player.MultiKills),
                    Exterminations = Int(player.Exterminations),
                    Betrayals = Int(player.Betrayals),
                    HammerKills = IntOrNull(player.HammerKills),
                    SwordKills = IntOrNull(player.SwordKills),
                    BallKills = IntOrNull(player.BallKills),
                    Punches = IntOrNull(player.Punches),
                    Score = IntOrNull(player.Score),
                    DamageDealt = IntOrNull(player.DamageDealt),
                    DamageTaken = IntOrNull(player.DamageTaken),
                    ExtendedMetricsJson = player.ExtendedMetrics is { ValueKind: JsonValueKind.Object } metrics && metrics.EnumerateObject().Any()
                        ? metrics.GetRawText()
                        : null,
                };
                switch (await Upsert(row, ct))
                {
                    case UpsertResult.Inserted: counts.StatsInserted++; break;
                    case UpsertResult.Updated: counts.StatsUpdated++; break;
                    default: counts.ManualReplacedByWaypoint++; break;
                }
            }
        }
        return sheetPlayers;
    }

    /// <summary>
    /// For each phase with Waypoint stats: sheet rows no Xbox account matched (their user has no Waypoint row) and Xbox
    /// accounts no sheet row matched are probably the same players under old gamertags. Pairs on the same team (an
    /// account's team is its teammates' sheet team, by majority) become suggestions for a sysadmin (GamertagClaims,
    /// RosterMatch); nothing is merged automatically. The unmatched rows are warned about.
    /// </summary>
    private async Task SuggestRosterMatches(SeasonGroup group, Season season, Dictionary<bool, HashSet<int>> waypointUsers, Dictionary<bool, List<SheetPlayer>> sheetPlayers, HistoricalImportReport counts, CancellationToken ct)
    {
        var matchPhases = WaypointIds(group, null);
        foreach (var phase in new[] { false, true })
        {
            var phaseWaypointUsers = waypointUsers[phase];
            if (phaseWaypointUsers.Count == 0)
                continue;
            var sheet = sheetPlayers[phase];
            var unmatchedRows = sheet.Where(s => !phaseWaypointUsers.Contains(s.UserId)).ToList();
            foreach (var row in unmatchedRows)
                Warn(counts, $"{group.SourceId} {Phase(phase)}: {row.Gamertag} matches no Xbox account of the season's matches");
            if (unmatchedRows.Count == 0)
                continue;

            var sheetUserIds = sheet.Select(s => s.UserId).ToHashSet();
            var teamOfUser = sheet.Where(s => !string.IsNullOrWhiteSpace(s.TeamName))
                .GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.First().TeamName!);
            var phaseIds = matchPhases.Where(m => m.Value == phase).Select(m => m.Key).ToList();
            var players = await _context.MatchParticipants.Where(p => phaseIds.Contains(p.MatchID))
                .Select(p => new { p.MatchID, p.TeamID, p.XboxUserID, UserId = p.XboxUser.User != null ? (int?)p.XboxUser.User.Id : null })
                .ToListAsync(ct);
            var unmatchedAccounts = players.Where(p => p.UserId is null || !sheetUserIds.Contains(p.UserId.Value))
                .Select(p => p.XboxUserID).Distinct().ToList();
            foreach (var xuid in unmatchedAccounts)
            {
                var team = players.Where(p => p.XboxUserID == xuid)
                    .SelectMany(p => players.Where(o => o.MatchID == p.MatchID && o.TeamID == p.TeamID && o.XboxUserID != xuid))
                    .Where(o => o.UserId is not null && teamOfUser.ContainsKey(o.UserId.Value))
                    .Select(o => teamOfUser[o.UserId!.Value])
                    .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;
                if (team is null)
                    continue;
                foreach (var row in unmatchedRows.Where(r => string.Equals(r.TeamName, team, StringComparison.OrdinalIgnoreCase)))
                {
                    var isPlaceholder = await _context.Users.AnyAsync(u => u.Id == row.UserId && u.IsDummyUser, ct);
                    if (!isPlaceholder)
                        continue;
                    var normalized = HistoricalKeys.Normalize(row.Gamertag);
                    var exists = _context.GamertagClaims.Local.Any(c => c.Kind == GamertagClaimKind.RosterMatch && c.XboxUserID == xuid && c.NormalizedGamertag == normalized)
                        || await _context.GamertagClaims.AnyAsync(c => c.Kind == GamertagClaimKind.RosterMatch && c.XboxUserID == xuid && c.NormalizedGamertag == normalized, ct);
                    if (exists)
                        continue;
                    _context.GamertagClaims.Add(new GamertagClaim
                    {
                        Kind = GamertagClaimKind.RosterMatch,
                        XboxUserID = xuid,
                        Gamertag = row.Gamertag,
                        NormalizedGamertag = normalized,
                        PlaceholderUserID = row.UserId,
                        SeasonID = season.SeasonID,
                        TeamName = row.TeamName,
                    });
                    counts.SuggestionsCreated++;
                }
            }
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>Adds the row, or copies its values onto the one with its key. A sheet row never overwrites a Waypoint one.</summary>
    private async Task<UpsertResult> Upsert(PlayerSeasonStat row, CancellationToken ct)
    {
        var existing = await _context.PlayerSeasonStats.FirstOrDefaultAsync(s => s.SeasonID == row.SeasonID && s.UserID == row.UserID
            && s.StintIndex == row.StintIndex && s.IsPlayoffs == row.IsPlayoffs, ct);
        if (existing is null)
        {
            _context.PlayerSeasonStats.Add(row);
            await _context.SaveChangesAsync(ct);
            return UpsertResult.Inserted;
        }
        if (existing.Source == StatSource.Waypoint && row.Source == StatSource.Manual)
            return UpsertResult.KeptWaypoint;
        row.PlayerSeasonStatID = existing.PlayerSeasonStatID;
        row.CreatedAt = existing.CreatedAt;
        row.CreatedByID = existing.CreatedByID;
        row.ModifiedAt = existing.ModifiedAt;
        row.ModifiedByID = existing.ModifiedByID;
        // Only the values that changed are written (and get a history row): a re-run of the same archive changes nothing.
        _context.Entry(existing).CurrentValues.SetValues(row);
        await _context.SaveChangesAsync(ct);
        return UpsertResult.Updated;
    }

    private async Task ImportChampionships(SeasonGroup group, Season season, List<GameVersion> gameVersions, PlayerResolver resolver, HistoricalImportReport counts, CancellationToken ct)
    {
        var seen = new HashSet<(string Tier, int UserID)>();
        foreach (var championship in group.Championships)
        {
            var slug = HistoricalKeys.GameVersionSlug(championship.Game);
            var gameVersionId = gameVersions.FirstOrDefault(g => g.Slug == slug)?.GameVesionID;
            var tier = championship.Tier.Trim();
            foreach (var gamertag in championship.Roster.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                var userId = (await resolver.Resolve(gamertag, null, ct)).UserId;
                if (!seen.Add((tier.ToUpperInvariant(), userId)))
                {
                    Skip(counts, $"{group.SourceId} {tier} champions: {gamertag} is listed twice for one player");
                    continue;
                }
                var isCaptain = championship.Captain is not null && HistoricalKeys.Normalize(championship.Captain) == HistoricalKeys.Normalize(gamertag);
                var existing = await _context.PlayerChampionships.FirstOrDefaultAsync(c => c.SeasonID == season.SeasonID && c.Tier == tier && c.UserID == userId, ct);
                if (existing is null)
                {
                    existing = new PlayerChampionship { SeasonID = season.SeasonID, Tier = tier, UserID = userId };
                    _context.PlayerChampionships.Add(existing);
                    counts.ChampionshipsInserted++;
                }
                else
                {
                    counts.ChampionshipsUpdated++;
                }
                existing.LeagueID = group.League.LeagueID;
                existing.GameVersionID = gameVersionId;
                existing.TeamName = championship.Team.Trim();
                existing.IsCaptain = isCaptain;
                existing.GamertagUsed = HistoricalKeys.CleanGamertag(gamertag);
                await _context.SaveChangesAsync(ct);
            }
        }
    }

    private static string Phase(bool playoffs) => playoffs ? "playoffs" : "regular season";
    private static int Int(decimal? value) => value is null ? 0 : (int)Math.Round(value.Value);
    private static int? IntOrNull(decimal? value) => value is null ? null : (int)Math.Round(value.Value);
    private static decimal? Dec(decimal? value, int places) => value is null ? null : Math.Round(value.Value, places);

    private void Warn(HistoricalImportReport report, string warning)
    {
        _logger.LogWarning("Historical import: {Warning}", warning);
        report.Warnings.Add(warning);
    }

    private void Skip(HistoricalImportReport report, string reason)
    {
        _logger.LogInformation("Historical import skipped: {Reason}", reason);
        report.Skipped.Add(reason);
    }

    private static void Add(HistoricalImportReport report, HistoricalImportReport season)
    {
        report.SeasonsCreated += season.SeasonsCreated;
        report.SeasonsUpdated += season.SeasonsUpdated;
        report.StatsInserted += season.StatsInserted;
        report.StatsUpdated += season.StatsUpdated;
        report.WaypointStatsInserted += season.WaypointStatsInserted;
        report.WaypointStatsUpdated += season.WaypointStatsUpdated;
        report.ManualReplacedByWaypoint += season.ManualReplacedByWaypoint;
        report.ChampionshipsInserted += season.ChampionshipsInserted;
        report.ChampionshipsUpdated += season.ChampionshipsUpdated;
        report.DummyUsersCreated += season.DummyUsersCreated;
        report.SuggestionsCreated += season.SuggestionsCreated;
        report.MissingMatches.AddRange(season.MissingMatches);
        report.Skipped.AddRange(season.Skipped);
        report.Warnings.AddRange(season.Warnings);
    }
}
