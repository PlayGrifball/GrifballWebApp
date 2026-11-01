using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using System.Text.Json;

namespace GrifballWebApp.Server.Historical;

public interface IHistoricalImportService
{
    /// <summary>
    /// Imports the historical archive into the app's own tables: its seasons, their teams and rosters, players' season
    /// stats and championships. Idempotent: a season is found again by its HistoricalSourceID
    /// ("{sourceWorkbook}/{sourceSheet}"), a team by its name in the season, a stat row by (SeasonID, UserID, StintIndex,
    /// IsPlayoffs) and a title by (SeasonID, Tier, UserID), so a re-run updates rows in place and adds none.
    /// Each season is one transaction. <paramref name="progress"/> hears what it is doing.
    /// </summary>
    Task<HistoricalImportReport> Import(HistoricalArchiveInput input, Func<HistoricalImportProgress, Task>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// <see cref="Import"/>, as a dry run (each season's transaction rolled back: the report says what it would do), with a
    /// guardrail (a season that would remove too much is rolled back and listed for review instead), or for some seasons
    /// only. Each season's removals are in <see cref="HistoricalImportReport.SeasonRemovals"/>.
    /// </summary>
    Task<HistoricalImportReport> Run(HistoricalArchiveInput input, HistoricalImportRunOptions options, Func<HistoricalImportProgress, Task>? progress = null, CancellationToken ct = default);
}

public class HistoricalImportRunOptions
{
    /// <summary>Roll every season back: nothing changes, the report says what would. The players lists are left out.</summary>
    public bool DryRun { get; init; }
    /// <summary>Stop (roll back) a season that would remove more than this allows; null imports whatever it removes.</summary>
    public HistoricalGuardrailOptions? Guardrail { get; init; }
    /// <summary>Only the seasons with one of these keys (a season's HistoricalSourceID or any of its entries'); null is all.</summary>
    public IReadOnlySet<string>? Seasons { get; init; }
}

/// <summary>When an automatic import stops a season for review (HistoricalImport:Guardrail).</summary>
public class HistoricalGuardrailOptions
{
    /// <summary>More than this share of the season's stat rows or teams removed.</summary>
    public decimal MaxRemovedFraction { get; set; } = 0.2m;
    /// <summary>Any championship (a title or a title's player) removed.</summary>
    public bool StopOnAnyChampionshipRemoved { get; set; } = true;

    public bool Exceeded(HistoricalSeasonRemoval removal)
        => TooMany(removal.StatsRemoved, removal.StatsBefore) || TooMany(removal.TeamsRemoved, removal.TeamsBefore)
           || (StopOnAnyChampionshipRemoved ? removal.TitlesRemoved > 0 : TooMany(removal.TitlesRemoved, removal.TitlesBefore));

    private bool TooMany(int removed, int before) => removed > 0 && before > 0 && (decimal)removed / before > MaxRemovedFraction;
}

/// <summary>What importing a season removes of what it had: stat rows, teams, titles (championship rows).</summary>
public class HistoricalSeasonRemoval
{
    public required string SeasonKey { get; set; }
    public string? SeasonName { get; set; }
    public int StatsBefore { get; set; }
    public int StatsRemoved { get; set; }
    public int TeamsBefore { get; set; }
    public int TeamsRemoved { get; set; }
    public int TitlesBefore { get; set; }
    public int TitlesRemoved { get; set; }
    /// <summary>Rolled back by the guardrail: left as it was until a sysadmin approves it.</summary>
    public bool Stopped { get; set; }

    public bool RemovesAnything => StatsRemoved > 0 || TeamsRemoved > 0 || TitlesRemoved > 0;

    public string Summary => $"{StatsRemoved} of {StatsBefore} stat rows, {TeamsRemoved} of {TeamsBefore} teams, {TitlesRemoved} of {TitlesBefore} titles";
}

public record HistoricalImportProgress(string Stage, int SeasonsDone, int SeasonsTotal);

public class HistoricalImportOptions
{
    /// <summary>Attempts at pulling one match (a failed call, after the Halo client's own retries of 429 and 5xx).</summary>
    public int MatchPullAttempts { get; set; } = 3;
    /// <summary>The first wait between attempts, doubled after each.</summary>
    public TimeSpan MatchPullBackoff { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>A recorded game of the same two teams as a Waypoint game no further from it than this is that game (the
    /// Waypoint one is kept).</summary>
    public TimeSpan RecordedGameWaypointWindow { get; set; } = TimeSpan.FromMinutes(30);
}

public class HistoricalImportReport
{
    public int SeasonsCreated { get; set; }
    public int SeasonsUpdated { get; set; }
    /// <summary>Titles-only seasons the champions record made whose titles are another season's now, removed.</summary>
    public int SeasonsRetired { get; set; }
    public int TeamsCreated { get; set; }
    /// <summary>Rows of earlier imports this one didn't bring back, removed: teams (renamed or gone), stored totals, titles.</summary>
    public int TeamsRemoved { get; set; }
    public int StatsRemoved { get; set; }
    public int ChampionshipsRemoved { get; set; }
    public int StatsInserted { get; set; }
    public int StatsUpdated { get; set; }
    /// <summary>Season matches made from Waypoint games, and the games linked to them.</summary>
    public int SeasonMatchesCreated { get; set; }
    public int GamesLinked { get; set; }
    public int SeasonMatchesRemoved { get; set; }
    /// <summary>Recorded (non-Waypoint) games added or updated; skipped as a Waypoint game's duplicate (by match ID, or the
    /// same two teams within <see cref="HistoricalImportOptions.RecordedGameWaypointWindow"/>, details in the warnings); and
    /// removed (no longer in the archive, or now a duplicate).</summary>
    public int RecordedGamesImported { get; set; }
    public int RecordedGamesSkippedAsWaypoint { get; set; }
    public int RecordedGamesRemoved { get; set; }
    /// <summary>Manual rows not imported, or removed, because the player has Waypoint stats for that phase of the season.</summary>
    public int ManualReplacedByWaypoint { get; set; }
    public int ChampionshipsInserted { get; set; }
    public int ChampionshipsUpdated { get; set; }
    public int DummyUsersCreated { get; set; }
    public int WaypointMatchesPulled { get; set; }
    /// <summary>Roster matches suggested for a sysadmin to decide (GamertagClaims).</summary>
    public int SuggestionsCreated { get; set; }
    /// <summary>Players lists: Discord IDs linked to placeholders, placeholders merged into the user with that Discord ID,
    /// Discord matches left for a sysadmin (GamertagClaims), and regions filled in.</summary>
    public int DiscordLinked { get; set; }
    public int DiscordMerged { get; set; }
    public int DiscordClaimsCreated { get; set; }
    public int RegionsSet { get; set; }
    /// <summary>Waypoint matches that are not in the database (could not be pulled, or not found), by season and phase:
    /// that phase kept its Manual stats.</summary>
    public List<string> MissingMatches { get; set; } = [];
    /// <summary>Why something was left out, one line each.</summary>
    public List<string> Skipped { get; set; } = [];
    /// <summary>What was imported but deserves a look, e.g. a sheet row of a Waypoint season no account matched.</summary>
    public List<string> Warnings { get; set; } = [];
    /// <summary>Seasons whose transaction failed and was rolled back.</summary>
    public List<string> Errors { get; set; } = [];
    /// <summary>Seasons this run removes (or would remove, a dry run) rows from, and those the guardrail stopped.</summary>
    public List<HistoricalSeasonRemoval> SeasonRemovals { get; set; } = [];
    /// <summary>A refresh of the sources: those whose content changed, those that failed (and why), how many were unchanged,
    /// and the import it queued.</summary>
    public List<string> SourcesChanged { get; set; } = [];
    public List<string> SourcesFailed { get; set; } = [];
    public int SourcesUnchanged { get; set; }
    public int? ImportJobID { get; set; }
}

/// <inheritdoc/>
public partial class HistoricalImportService : IHistoricalImportService
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
    private readonly ChampionshipService _championships;
    private readonly TimeProvider _timeProvider;
    private readonly GamertagMatching _matching;
    private readonly GrifballWebApp.Server.Profile.IUserMergeService _merges;

    public HistoricalImportService(GrifballContext context, IDataPullService dataPullService, IOptions<HistoricalImportOptions> options, ILogger<HistoricalImportService> logger, ChampionshipService championships, GamertagMatching matching, TimeProvider timeProvider,
        GrifballWebApp.Server.Profile.IUserMergeService? merges = null)
    {
        _context = context;
        _dataPullService = dataPullService;
        _options = options.Value;
        _logger = logger;
        _championships = championships;
        _matching = matching;
        _timeProvider = timeProvider;
        _merges = merges ?? new GrifballWebApp.Server.Profile.UserMergeService(context, timeProvider);
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
        /// <summary>Each entry's game: a stat row's game is its entry's (the season's is its regular season's).</summary>
        public Dictionary<ArchiveSeason, GameVersion> EntryGames { get; } = new(ReferenceEqualityComparer.Instance);
        public List<ArchiveChampionship> Championships { get; } = [];
        public WaypointSeasonMatches? Waypoint { get; set; }
        /// <summary>The season's recorded games; null when the archive has none for it (its games are then left as they are).</summary>
        public List<RecordedGameInput>? RecordedGames { get; set; }
        public SeasonFormat Format { get; set; } = SeasonFormat.LeagueSeason;
    }

    private sealed record SheetPlayer(int UserId, string Gamertag, string? TeamName);

    /// <summary>A season's teams (by name, unique in a season) and who played for each, gathered while importing.</summary>
    private sealed class SeasonTeams
    {
        public Dictionary<string, Team> ByName { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Each team's players, from the sheets and the titles: its roster.</summary>
        public HashSet<(int TeamID, int UserID)> Players { get; } = [];
        /// <summary>The captain (user) of each team a title names one for.</summary>
        public Dictionary<int, int> Captains { get; } = [];

        public Team? Find(string? name) => string.IsNullOrWhiteSpace(name) ? null : ByName.GetValueOrDefault(CleanTeamName(name));

        /// <summary>Puts the player on the named team's roster; returns the team's ID, null when no team is named.</summary>
        public int? Join(string? teamName, int userId)
        {
            var team = Find(teamName);
            if (team is null)
                return null;
            Players.Add((team.TeamID, userId));
            return team.TeamID;
        }
    }

    /// <summary>A team name as the import stores it: whitespace cleaned like a gamertag, cut to the column's length.</summary>
    private static string CleanTeamName(string name)
    {
        var clean = HistoricalKeys.CleanGamertag(name);
        return clean.Length > Team.TeamNameColumnLength ? clean[..Team.TeamNameColumnLength] : clean;
    }

    public Task<HistoricalImportReport> Import(HistoricalArchiveInput input, Func<HistoricalImportProgress, Task>? progress = null, CancellationToken ct = default)
        => Run(input, new HistoricalImportRunOptions(), progress, ct);

    public async Task<HistoricalImportReport> Run(HistoricalArchiveInput input, HistoricalImportRunOptions options, Func<HistoricalImportProgress, Task>? progress = null, CancellationToken ct = default)
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
            group.EntryGames[entry] = gameVersion;
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
            // The season's game and division are its regular season's; entries that say otherwise are reported
            group.GameVersion = group.EntryGames[primary];
            group.Division = primary.Division;
            group.Format = HistoricalKeys.Format(group.Entries.Select(e => e.Format).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f)), group.League.Slug, primary.SourceWorkbook, group.SeasonName);
            if (group.EntryGames.Values.Select(g => g.GameVesionID).Distinct().Count() > 1)
                Warn(report, $"Season {group.SeasonName}: its entries are of more than one game ({string.Join(", ", group.EntryGames.Values.Select(g => g.Slug).Distinct())}); the season is {group.GameVersion.Slug}, each stat row its entry's");
            if (group.Entries.Select(e => e.Division?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                Warn(report, $"Season {group.SeasonName}: its entries are of more than one division; the season is {group.Division ?? "undivided"}");
        }

        await GroupChampionships(input.Championships, groups, leagues, gameVersions, ct);
        // Every season the archive has now, before a partial run narrows it: a titles-only season not among them is left over
        var archiveSeasons = groups.Values.Select(g => g.SourceId).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        foreach (var game in input.RecordedGames ?? [])
        {
            if (bySourceId.TryGetValue(game.SeasonSourceId.Trim(), out var group))
                (group.RecordedGames ??= []).Add(game);
            else
                Skip(report, $"Recorded game {game.SourceKey}: no season with the key {game.SeasonSourceId} in the archive");
        }
        // Only the seasons asked for (the ones whose sources changed), with all of their parts
        if (options.Seasons is { } only)
        {
            foreach (var key in groups.Where(g => !only.Contains(g.Value.SourceId) && !g.Value.Entries.Select(EntrySourceId).Any(id => id is not null && only.Contains(id)))
                .Select(g => g.Key).ToList())
                groups.Remove(key);
        }

        // Matches come from the Halo Infinite API before any season's transaction: no network call holds one open.
        await PullWaypointMatches(groups.Values, report, progress, groups.Count, ct);

        var done = 0;
        foreach (var group in groups.Values)
        {
            ct.ThrowIfCancellationRequested();
            await progress(new HistoricalImportProgress($"Importing {group.SeasonName}", done, groups.Count));
            await ImportSeason(group, gameVersions, report, options, ct);
            done++;
        }
        if (input.Championships is { Champions.Count: > 0 } champions)
            await RetireLeftoverTitleSeasons(champions, archiveSeasons, leagues, report, options, ct);
        // After the seasons: their placeholders are who the players lists' Discord IDs and regions go to
        if (input.Players is { Count: > 0 } && !options.DryRun)
        {
            await progress(new HistoricalImportProgress("Importing players lists", done, groups.Count));
            await ImportPlayers(input.Players, report, ct);
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
        var workbook = ChampionsWorkbook(championships);
        var imported = await _context.Seasons.Where(s => s.HistoricalSourceID != null && s.LeagueID == league.LeagueID)
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

    private static string ChampionsWorkbook(ChampionsArchive? championships)
        => string.IsNullOrWhiteSpace(championships?.SourceId) ? DefaultChampionsSourceId : championships.SourceId.Trim();

    /// <summary>
    /// A titles-only season the champions record made ("{champions workbook}/{season}") that no archive season is now:
    /// its titles went to a season with stats (a tiered one, or one a later source brought). It goes with its teams,
    /// rosters and titles, under the guardrail like any season's removals, unless anything else points at it (stats,
    /// season matches, signups, VODs, recorded games, claims): then it stays and the report says why. A partial run only
    /// retires the seasons it was asked for, since the rest of the archive isn't in front of it.
    /// </summary>
    private async Task RetireLeftoverTitleSeasons(ChampionsArchive championships, HashSet<string> archiveSeasons, List<League> leagues, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var league = leagues.Single(l => l.Slug == ChampionsLeagueSlug);
        var prefix = ChampionsWorkbook(championships) + "/";
        var leftover = (await _context.Seasons.AsNoTracking()
                .Where(s => s.HistoricalSourceID != null && s.LeagueID == league.LeagueID && s.HistoricalSourceID.StartsWith(prefix))
                .Select(s => new { s.SeasonID, s.HistoricalSourceID, s.SeasonName })
                .ToListAsync(ct))
            .Where(s => !archiveSeasons.Contains(s.HistoricalSourceID))
            .Where(s => options.Seasons is not { } only || only.Contains(s.HistoricalSourceID))
            .ToList();

        foreach (var season in leftover)
        {
            var used = new List<string>();
            if (await _context.PlayerSeasonStats.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("stats");
            if (await _context.SeasonMatches.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("season matches");
            if (await _context.SeasonSignups.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("signups");
            if (await _context.SeasonAvailability.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("availability");
            if (await _context.Vods.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("VODs");
            if (await _context.RecordedGames.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("recorded games");
            if (await _context.GamertagClaims.AnyAsync(x => x.SeasonID == season.SeasonID, ct)) used.Add("claims");
            if (used.Count > 0)
            {
                Warn(report, $"{season.HistoricalSourceID}: its titles are another season's now, but it has {string.Join(", ", used)}; left for a sysadmin");
                continue;
            }

            var removal = new HistoricalSeasonRemoval { SeasonKey = season.HistoricalSourceID, SeasonName = season.SeasonName };
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var teams = await _context.Teams.Where(t => t.SeasonID == season.SeasonID).ToListAsync(ct);
                var titles = await _context.PlayerChampionships.Where(c => c.SeasonID == season.SeasonID).ToListAsync(ct);
                (removal.TeamsBefore, removal.TeamsRemoved) = (teams.Count, teams.Count);
                (removal.TitlesBefore, removal.TitlesRemoved) = (titles.Count, titles.Count);
                // A team's captain is one of its players: cleared before the rosters go
                foreach (var team in teams)
                    team.CaptainID = null;
                await _context.SaveChangesAsync(ct);
                var teamIds = teams.Select(t => t.TeamID).ToList();
                _context.PlayerChampionships.RemoveRange(titles);
                _context.TeamPlayers.RemoveRange(await _context.TeamPlayers.Where(tp => teamIds.Contains(tp.TeamID)).ToListAsync(ct));
                _context.Teams.RemoveRange(teams);
                _context.Seasons.Remove(await _context.Seasons.SingleAsync(s => s.SeasonID == season.SeasonID, ct));
                await _context.SaveChangesAsync(ct);

                removal.Stopped = !options.DryRun && options.Guardrail?.Exceeded(removal) == true;
                report.SeasonRemovals.Add(removal);
                if (options.DryRun || removal.Stopped)
                {
                    await transaction.RollbackAsync(ct);
                    _context.ChangeTracker.Clear();
                    if (removal.Stopped)
                        Warn(report, $"{season.HistoricalSourceID}: stopped for review, retiring it would remove {removal.Summary}");
                    continue;
                }
                await transaction.CommitAsync(ct);
                report.TeamsRemoved += removal.TeamsRemoved;
                report.ChampionshipsRemoved += removal.TitlesRemoved;
                report.SeasonsRetired++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "Retiring the titles-only season {Season} failed and was rolled back", season.HistoricalSourceID);
                report.Errors.Add($"{season.HistoricalSourceID}: {ex.GetBaseException().Message}");
            }
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

    private async Task ImportSeason(SeasonGroup group, List<GameVersion> gameVersions, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var counts = new HistoricalImportReport();
        var removal = await SeasonBefore(group, ct);
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
            // The season is reconciled with the archive: what this run doesn't bring back (a team renamed, a player taken
            // off the sheet or under a corrected gamertag, a tier's title gone, Waypoint games no longer listed) goes
            var teams = await UpsertTeams(group, season, counts, ct);
            await RemoveTeamsNoLongerThere(season, teams, counts, ct);
            if (group.Waypoint is not null)
                await RemoveSeriesNoLongerListed(group, season, counts, ct);
            var waypoint = await ImportWaypointPlayers(group, season, resolver, teams, counts, ct);
            var kept = new HashSet<PlayerSeasonStat>(ReferenceEqualityComparer.Instance);
            var sheetPlayers = await ImportManualStats(group, season, waypoint.Users, resolver, teams, kept, counts, ct);
            await RemoveRowsNoLongerThere(season, kept, counts, ct);
            var games = new List<ImportedGame>();
            if (group.Waypoint is not null)
            {
                await SuggestRosterMatches(group, season, teams, waypoint.Users, sheetPlayers, counts, ct);
                games.AddRange(await WaypointGames(group, teams, waypoint, counts, ct));
            }
            if (group.RecordedGames is not null)
                games.AddRange(await ImportRecordedGames(group, season, gameVersions, resolver, teams, games, counts, ct));
            await LinkSeries(season, games, counts, ct);
            var tiers = await ImportChampionships(group, season, gameVersions, resolver, teams, counts, ct);
            await RemoveTitlesNoLongerThere(season, tiers, counts, ct);
            await SyncRosters(teams, ct);
            foreach (var gamertag in resolver.Ambiguous.Distinct(StringComparer.OrdinalIgnoreCase))
                Warn(counts, $"{group.SourceId}: {gamertag} is in more than one Xbox account's gamertag history for the season; left to its placeholder");

            await _context.SaveChangesAsync(ct);
            counts.DummyUsersCreated = resolver.DummiesCreated;
            (removal.StatsRemoved, removal.TeamsRemoved, removal.TitlesRemoved) = (counts.StatsRemoved, counts.TeamsRemoved, counts.ChampionshipsRemoved);
            removal.Stopped = !options.DryRun && options.Guardrail?.Exceeded(removal) == true;
            if (options.DryRun || removal.Stopped)
            {
                // Rolled back: nothing of it stays, and the tracked rows are ones that no longer exist
                await transaction.RollbackAsync(ct);
                _context.ChangeTracker.Clear();
                report.SeasonRemovals.Add(removal);
                if (removal.Stopped)
                {
                    Warn(report, $"{group.SourceId}: stopped for review, it would remove {removal.Summary}");
                    return;
                }
                Add(report, counts);
                return;
            }
            await transaction.CommitAsync(ct);
            // The season is saved: nothing of it is needed again, so nothing of it stays tracked. Otherwise every season's rows
            // pile up for the rest of the run: an import of every archive season grew past 512Mi (OOM-killed), and each
            // SaveChanges scanned all of them, slower season after season
            _context.ChangeTracker.Clear();
            if (removal.RemovesAnything)
                report.SeasonRemovals.Add(removal);
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

    /// <summary>What the season has before this run: what its removals are measured against.</summary>
    private async Task<HistoricalSeasonRemoval> SeasonBefore(SeasonGroup group, CancellationToken ct)
    {
        var removal = new HistoricalSeasonRemoval { SeasonKey = group.SourceId, SeasonName = group.SeasonName };
        var seasonId = await _context.Seasons.Where(s => s.HistoricalSourceID == group.SourceId).Select(s => (int?)s.SeasonID).FirstOrDefaultAsync(ct);
        if (seasonId is null)
            return removal;
        removal.StatsBefore = await _context.PlayerSeasonStats.CountAsync(s => s.SeasonID == seasonId, ct);
        removal.TeamsBefore = await _context.Teams.CountAsync(t => t.SeasonID == seasonId, ct);
        removal.TitlesBefore = await _context.PlayerChampionships.CountAsync(c => c.SeasonID == seasonId, ct);
        return removal;
    }

    private async Task<Season?> UpsertSeason(SeasonGroup group, HistoricalImportReport counts, CancellationToken ct)
    {
        var season = await _context.Seasons.FirstOrDefaultAsync(s => s.HistoricalSourceID == group.SourceId, ct);
        if (season is null)
        {
            // If there is an existing title-only placeholder from champions import with the same name, adopt it
            var existingPlaceholder = await _context.Seasons.FirstOrDefaultAsync(s =>
                s.LeagueID == group.League.LeagueID
                && s.SeasonName == group.SeasonName
                && s.HistoricalSourceID != null
                && s.HistoricalSourceID.StartsWith(ChampionsWorkbook(null) + "/"), ct);

            if (existingPlaceholder is not null)
            {
                season = existingPlaceholder;
                season.HistoricalSourceID = group.SourceId;
                counts.SeasonsUpdated++;
            }
            else
            {
                // A season of the app's own (or one imported under another key) with that name in the league
                var taken = await _context.Seasons.AnyAsync(s => s.LeagueID == group.League.LeagueID && s.SeasonName == group.SeasonName, ct);
                if (taken)
                {
                    Skip(counts, $"Season {group.SeasonName} ({group.League.LeagueName}): the league already has a season with that name that is not {group.SourceId}");
                    return null;
                }
                season = new Season { HistoricalSourceID = group.SourceId, SeasonName = group.SeasonName };
                _context.Seasons.Add(season);
                counts.SeasonsCreated++;
            }
        }
        else
        {
            counts.SeasonsUpdated++;
        }

        // Only the year is known: the season spans it, the window its gamertags are matched for, but ends by now at the
        // latest, so that this year's seasons are past ones too (the app tells current seasons by their dates)
        var start = new DateTime(HistoricalKeys.Year(group.SeasonName) ?? FallbackYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        season.SignupsOpen = season.SignupsClose = season.DraftStart = season.SeasonStart = start;
        var end = start.AddYears(1).AddSeconds(-1);
        season.SeasonEnd = season.SeasonEnd > start && season.SeasonEnd <= end ? season.SeasonEnd : end;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (season.SeasonEnd > now)
            season.SeasonEnd = now;
        // Public since it began: it is a past season everyone can see
        season.PublicAt = season.SeasonStart;
        season.SeasonName = group.SeasonName;
        season.LeagueID = group.League.LeagueID;
        season.GameVersionID = group.GameVersion?.GameVesionID;
        season.Division = group.Division;
        season.HistoricalSourceUrl = group.SourceUrl;
        season.Format = group.Format;
        await _context.SaveChangesAsync(ct);
        return season;
    }

    /// <summary>
    /// The season's teams this run doesn't name (renamed or gone from the archive) go, with their rosters, titles and
    /// season matches; the stat rows and claims that pointed at them are left without a team until this run links them.
    /// </summary>
    private async Task RemoveTeamsNoLongerThere(Season season, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var keep = teams.ByName.Values.Select(t => t.TeamID).ToList();
        var stale = await _context.Teams.Where(t => t.SeasonID == season.SeasonID && !keep.Contains(t.TeamID)).ToListAsync(ct);
        if (stale.Count == 0)
            return;
        var ids = stale.Select(t => t.TeamID).ToList();
        foreach (var row in await _context.PlayerSeasonStats.Where(s => s.TeamID != null && ids.Contains(s.TeamID.Value)).ToListAsync(ct))
            row.TeamID = null;
        foreach (var claim in await _context.GamertagClaims.Where(c => c.TeamID != null && ids.Contains(c.TeamID.Value)).ToListAsync(ct))
            claim.TeamID = null;
        foreach (var side in await _context.RecordedGameTeams.Where(t => t.TeamID != null && ids.Contains(t.TeamID.Value)).ToListAsync(ct))
            side.TeamID = null;
        _context.PlayerChampionships.RemoveRange(await _context.PlayerChampionships.Where(c => ids.Contains(c.TeamID)).ToListAsync(ct));
        _context.SeasonMatches.RemoveRange(await _context.SeasonMatches
            .Where(m => (m.HomeTeamID != null && ids.Contains(m.HomeTeamID.Value)) || (m.AwayTeamID != null && ids.Contains(m.AwayTeamID.Value)))
            .Include(m => m.SeriesGames).Include(m => m.BracketMatch).ToListAsync(ct));
        foreach (var team in stale)
            team.CaptainID = null;
        await _context.SaveChangesAsync(ct);
        _context.TeamPlayers.RemoveRange(await _context.TeamPlayers.Where(tp => ids.Contains(tp.TeamID)).ToListAsync(ct));
        _context.Teams.RemoveRange(stale);
        await _context.SaveChangesAsync(ct);
        counts.TeamsRemoved += stale.Count;
    }

    /// <summary>The season's stored totals this run didn't write (a player taken off the sheet, a gamertag corrected to
    /// another player, a player whose games are the stats now) go.</summary>
    private async Task RemoveRowsNoLongerThere(Season season, HashSet<PlayerSeasonStat> kept, HistoricalImportReport counts, CancellationToken ct)
    {
        var stale = await _context.PlayerSeasonStats.Where(s => s.SeasonID == season.SeasonID).ToListAsync(ct);
        stale.RemoveAll(kept.Contains);
        _context.PlayerSeasonStats.RemoveRange(stale);
        await _context.SaveChangesAsync(ct);
        counts.StatsRemoved += stale.Count;
    }

    /// <summary>The titles of tiers the champions record no longer gives this season go (each tier's own roster is
    /// reconciled by ChampionshipService).</summary>
    private async Task RemoveTitlesNoLongerThere(Season season, HashSet<string> tiers, HistoricalImportReport counts, CancellationToken ct)
    {
        var stale = (await _context.PlayerChampionships.Where(c => c.SeasonID == season.SeasonID).ToListAsync(ct))
            .Where(c => !tiers.Contains(c.Tier)).ToList();
        _context.PlayerChampionships.RemoveRange(stale);
        await _context.SaveChangesAsync(ct);
        counts.ChampionshipsRemoved += stale.Count;
    }

    /// <summary>
    /// The season's teams: every team its sheets or titles name, found again by name. A team's record is the sheet's
    /// (its matches are not in the app): the regular-season record of its player who played the most games, the one with
    /// the most wins among those; a team only a title names has none.
    /// </summary>
    private async Task<SeasonTeams> UpsertTeams(SeasonGroup group, Season season, HistoricalImportReport counts, CancellationToken ct)
    {
        var teams = new SeasonTeams();
        var names = group.Entries.SelectMany(e => e.Players).Select(p => p.TeamName)
            .Concat(group.Championships.Select(c => c.Team))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => CleanTeamName(n!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var existing = await _context.Teams.Where(t => t.SeasonID == season.SeasonID).ToListAsync(ct);
        foreach (var name in names)
        {
            var team = existing.FirstOrDefault(t => string.Equals(t.TeamName, name, StringComparison.OrdinalIgnoreCase));
            if (team is null)
            {
                team = new Team { SeasonID = season.SeasonID };
                _context.Teams.Add(team);
                counts.TeamsCreated++;
            }
            var record = group.Entries.Where(e => !e.IsPlayoffs && !e.IsCombined)
                .SelectMany(e => e.Players)
                .Where(p => p.TeamName is not null && string.Equals(CleanTeamName(p.TeamName), name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var longest = record.OrderByDescending(p => Int(p.Wins) + Int(p.Losses)).ThenByDescending(p => Int(p.Wins)).FirstOrDefault();
            team.TeamName = name;
            team.RecordedWins = longest is null ? null : Int(longest.Wins);
            team.RecordedLosses = longest is null ? null : Int(longest.Losses);
            teams.ByName[name] = team;
        }
        await _context.SaveChangesAsync(ct);
        return teams;
    }

    /// <summary>Each team's roster becomes its players on the sheets and titles (rows added, and removed for players no
    /// longer there), and the captain its title names is the team's captain.</summary>
    private async Task SyncRosters(SeasonTeams teams, CancellationToken ct)
    {
        var teamIds = teams.ByName.Values.Select(t => t.TeamID).ToList();
        var rows = await _context.TeamPlayers.Where(tp => teamIds.Contains(tp.TeamID)).ToListAsync(ct);
        foreach (var (teamId, userId) in teams.Players.Where(p => !rows.Any(r => r.TeamID == p.TeamID && r.UserID == p.UserID)).ToList())
        {
            var row = new TeamPlayer { TeamID = teamId, UserID = userId };
            _context.TeamPlayers.Add(row);
            rows.Add(row);
        }
        await _context.SaveChangesAsync(ct);

        foreach (var team in teams.ByName.Values)
        {
            var captain = teams.Captains.TryGetValue(team.TeamID, out var userId)
                ? rows.FirstOrDefault(r => r.TeamID == team.TeamID && r.UserID == userId)
                : null;
            team.CaptainID = captain?.TeamPlayerID;
        }
        _context.TeamPlayers.RemoveRange(rows.Where(r => !teams.Players.Contains((r.TeamID, r.UserID))));
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>The players of a season's Waypoint matches: by phase, the accounts with games (their stats for the
    /// phase come from the games, so their sheet rows are not kept), the phases whose games are all in, and each Xbox
    /// user's account.</summary>
    private sealed record WaypointPlayers(Dictionary<bool, HashSet<int>> Users, HashSet<bool> CompletePhases, Dictionary<long, int> Accounts);

    /// <summary>A game between two of the season's teams, to be put in a series: a Waypoint match or a recorded game.</summary>
    private sealed record ImportedGame(Guid? MatchID, RecordedGame? Recorded, DateTime? Start, bool Playoffs, int? TeamA, Outcomes OutcomeA, int? TeamB, Outcomes OutcomeB);

    private static int? Min(int? a, int? b) => a is null || b is null ? a ?? b : Math.Min(a.Value, b.Value);
    private static int? Max(int? a, int? b) => a is null || b is null ? null : Math.Max(a.Value, b.Value);

    /// <summary>Games of the same two teams no further apart than this are one series (a season match).</summary>
    private static readonly TimeSpan SeriesGap = TimeSpan.FromHours(3);

    /// <summary>
    /// The players of the season's Waypoint matches (pulled into Matches / MatchParticipants), for each phase (regular
    /// season, playoffs) whose listed matches are all in the database: each is resolved to their Xbox user's account (a
    /// placeholder linked to it when it has none) and put on their sheet team. Their stats for the phase then come from
    /// the games, once <see cref="LinkSeries"/> links them to the season (SeasonStatsService reads them), so
    /// their stored totals for it go. A phase with a match missing keeps the sheet's totals, and the missing matches are
    /// reported.
    /// </summary>
    private async Task<WaypointPlayers> ImportWaypointPlayers(SeasonGroup group, Season season, PlayerResolver resolver, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var players = new WaypointPlayers(new() { [false] = [], [true] = [] }, [], []);
        // Games linked to the season already (an earlier import's) are their players' stats too
        var linkedPlayers = await _context.MatchParticipants
            .Where(p => p.MatchTeam.Match.SeriesGame!.SeasonMatch.SeasonID == season.SeasonID && p.XboxUser.User != null)
            .Select(p => new { UserID = p.XboxUser.User!.Id, Playoffs = p.MatchTeam.Match.SeriesGame!.SeasonMatch.BracketMatch != null })
            .Distinct()
            .ToListAsync(ct);
        foreach (var linked in linkedPlayers)
            players.Users[linked.Playoffs].Add(linked.UserID);
        var matchPhases = group.Waypoint is null ? new Dictionary<Guid, bool>() : WaypointIds(group, null);
        var ids = matchPhases.Keys.ToList();
        var present = (await _context.Matches.Where(m => ids.Contains(m.MatchID)).Select(m => m.MatchID).ToListAsync(ct)).ToHashSet();
        foreach (var phase in new[] { false, true })
        {
            var listed = matchPhases.Where(m => m.Value == phase).Select(m => m.Key).ToList();
            if (listed.Count == 0)
                continue;
            var missing = listed.Where(id => !present.Contains(id)).ToList();
            if (missing.Count == 0)
                players.CompletePhases.Add(phase);
            else
                counts.MissingMatches.Add($"{group.SourceId} {Phase(phase)}: {missing.Count} of {listed.Count} matches missing ({string.Join(", ", missing)}); kept the sheet's totals");
        }

        var completeIds = matchPhases.Where(m => players.CompletePhases.Contains(m.Value)).Select(m => m.Key).ToList();
        var participants = await _context.MatchParticipants
            .Where(p => completeIds.Contains(p.MatchID))
            .Select(p => new { p.MatchID, p.XboxUserID, p.XboxUser.Gamertag })
            .ToListAsync(ct);
        foreach (var player in participants.GroupBy(p => (p.XboxUserID, Playoffs: matchPhases[p.MatchID])))
        {
            var gamertag = player.First().Gamertag;
            var userId = (await resolver.Resolve(gamertag, player.Key.XboxUserID, ct)).UserId;
            players.Accounts[player.Key.XboxUserID] = userId;
            var sheetRow = group.Entries.Where(e => e.IsPlayoffs == player.Key.Playoffs)
                .SelectMany(e => e.Players)
                .FirstOrDefault(p => HistoricalKeys.Normalize(p.Gamertag) == HistoricalKeys.Normalize(gamertag));
            teams.Join(sheetRow?.TeamName, userId);
            players.Users[player.Key.Playoffs].Add(userId);
        }

        // The games replace the sheet's totals for the phase: a player's phase of a season is never both
        foreach (var phase in new[] { false, true })
        {
            var phaseUsers = players.Users[phase];
            // Counted in ImportManualStats, once per sheet row (which it then skips)
            _context.PlayerSeasonStats.RemoveRange(await _context.PlayerSeasonStats
                .Where(s => s.SeasonID == season.SeasonID && s.IsPlayoffs == phase && phaseUsers.Contains(s.UserID))
                .ToListAsync(ct));
        }
        await _context.SaveChangesAsync(ct);
        return players;
    }

    /// <summary>The season's series with a game its Waypoint list no longer has go: their games stop being its stats.</summary>
    private async Task RemoveSeriesNoLongerListed(SeasonGroup group, Season season, HistoricalImportReport counts, CancellationToken ct)
    {
        var listed = WaypointIds(group, null).Keys.ToList();
        var gone = await _context.SeasonMatches
            .Where(m => m.SeasonID == season.SeasonID && m.SeriesGames.Any(l => l.MatchID != null && !listed.Contains(l.MatchID.Value)))
            .Include(m => m.SeriesGames).Include(m => m.BracketMatch)
            .ToListAsync(ct);
        _context.SeasonMatches.RemoveRange(gone);
        await _context.SaveChangesAsync(ct);
        counts.SeasonMatchesRemoved += gone.Count;
    }

    /// <summary>
    /// The season's Waypoint games (of its complete phases) not in a series yet, for <see cref="LinkSeries"/>: each game's
    /// sides are the season's teams most of their players are on (from the sheets). A side that is none of the season's
    /// teams (or the same team as the other) is left without one and reported: its game still counts in its players' stats.
    /// </summary>
    private async Task<List<ImportedGame>> WaypointGames(SeasonGroup group, SeasonTeams teams, WaypointPlayers players, HistoricalImportReport counts, CancellationToken ct)
    {
        if (players.CompletePhases.Count == 0)
            return [];
        var matchPhases = WaypointIds(group, null);
        var ids = matchPhases.Where(m => players.CompletePhases.Contains(m.Value)).Select(m => m.Key).ToList();
        var linked = (await _context.SeriesGames.Where(l => l.MatchID != null && ids.Contains(l.MatchID.Value)).Select(l => l.MatchID!.Value).ToListAsync(ct)).ToHashSet();
        var sides = await _context.MatchTeams
            .Where(t => ids.Contains(t.MatchID))
            .Select(t => new { t.MatchID, t.Match.StartTime, t.TeamID, t.Outcome, Players = t.MatchParticipants.Select(p => p.XboxUserID).ToList() })
            .ToListAsync(ct);
        int? TeamOf(IEnumerable<long> xuids) => MajorityTeam(xuids.Where(players.Accounts.ContainsKey).Select(x => players.Accounts[x]), teams);

        var games = new List<ImportedGame>();
        foreach (var match in sides.Where(s => !linked.Contains(s.MatchID)).GroupBy(s => s.MatchID))
        {
            var both = match.OrderBy(s => s.TeamID).ToList();
            if (both.Count > 2)
            {
                Warn(counts, $"{group.SourceId}: Waypoint match {match.Key} has more than two teams; not linked to the season");
                continue;
            }
            var (a, b) = (TeamOf(both[0].Players), both.Count == 2 ? TeamOf(both[1].Players) : null);
            if (a == b)
                (a, b) = (null, null);
            if ((a is null || b is null) && teams.ByName.Count > 0)
                Warn(counts, $"{group.SourceId}: Waypoint match {match.Key}: a side is none of the season's teams (by its players' sheet teams); linked without it");
            games.Add(new ImportedGame(match.Key, null, both[0].StartTime, matchPhases[match.Key], a, both[0].Outcome, b, both.Count == 2 ? both[1].Outcome : Outcomes.DidNotFinish));
        }
        return games;
    }

    /// <summary>The season's team most of these players are on (its roster from the sheets and titles); null for none.</summary>
    private static int? MajorityTeam(IEnumerable<int> userIds, SeasonTeams teams)
    {
        var teamsOfUser = teams.Players.GroupBy(p => p.UserID).ToDictionary(g => g.Key, g => g.Select(p => p.TeamID).ToList());
        return userIds
            .SelectMany(u => teamsOfUser.GetValueOrDefault(u) ?? [])
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => (int?)g.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// The season's games not in a series yet (Waypoint and recorded alike) become its season matches, as reported games
    /// are in the app: the games of the same two teams within <see cref="SeriesGap"/> of each other are one series, its
    /// score their wins. Playoff series are bracket matches, in rounds by the order the teams played them (the archive has
    /// no bracket); a Late League season has no playoffs, so its series are all regular ones.
    /// </summary>
    private async Task LinkSeries(Season season, List<ImportedGame> games, HistoricalImportReport counts, CancellationToken ct)
    {
        if (games.Count == 0)
            return;
        var noPlayoffs = season.Format == SeasonFormat.LateLeague;

        // Series: the same two teams' games, a phase at a time, no more than SeriesGap apart
        var series = new List<List<ImportedGame>>();
        foreach (var pair in games.GroupBy(g => (Playoffs: g.Playoffs && !noPlayoffs, Low: Min(g.TeamA, g.TeamB), High: Max(g.TeamA, g.TeamB))))
        {
            List<ImportedGame>? current = null;
            foreach (var game in pair.OrderBy(g => g.Start))
            {
                if (current is null || game.Start is null || current[^1].Start is null || game.Start - current[^1].Start > SeriesGap)
                    series.Add(current = []);
                current.Add(game);
            }
        }

        var roundOf = new Dictionary<int, int>();
        // Each slot is the team's previous series, or a seed for its first (the bracket's rule: one or the other)
        var lastSeries = new Dictionary<int, MatchBracketInfo>();
        var seed = 0;
        var bracketNumber = await _context.SeasonMatches.CountAsync(m => m.SeasonID == season.SeasonID && m.BracketMatch != null, ct);
        foreach (var played in series.OrderBy(s => s[0].Start))
        {
            var first = played[0];
            var (home, away) = (first.TeamA, first.TeamB);
            // A game's sides in the series' order (its first game's): the other way round when the teams swapped sides
            bool Swapped(ImportedGame g) => (g.TeamA, g.TeamB) != (home, away) && (g.TeamB, g.TeamA) == (home, away);
            var homeWins = played.Count(g => (Swapped(g) ? g.OutcomeB : g.OutcomeA) == Outcomes.Won);
            var awayWins = played.Count(g => (Swapped(g) ? g.OutcomeA : g.OutcomeB) == Outcomes.Won);
            var match = new SeasonMatch
            {
                SeasonID = season.SeasonID,
                HomeTeamID = home,
                AwayTeamID = away,
                HomeTeamScore = homeWins,
                AwayTeamScore = awayWins,
                HomeTeamResult = homeWins > awayWins ? SeasonMatchResult.Won : homeWins < awayWins ? SeasonMatchResult.Loss : null,
                AwayTeamResult = awayWins > homeWins ? SeasonMatchResult.Won : awayWins < homeWins ? SeasonMatchResult.Loss : null,
                BestOf = Math.Max(played.Count, 2 * Math.Max(homeWins, awayWins) - 1),
                ScheduledTime = first.Start,
            };
            foreach (var (game, index) in played.Select((g, i) => (g, i)))
                match.SeriesGames.Add(new SeriesGame { MatchID = game.MatchID, RecordedGame = game.Recorded, GameNumber = index + 1 });
            if (first.Playoffs && !noPlayoffs)
            {
                var round = Math.Max(home is int h ? roundOf.GetValueOrDefault(h) : 0, away is int w ? roundOf.GetValueOrDefault(w) : 0) + 1;
                if (home is int homeTeam)
                    roundOf[homeTeam] = round;
                if (away is int awayTeam)
                    roundOf[awayTeam] = round;
                var info = new MatchBracketInfo { RoundNumber = round, MatchNumber = ++bracketNumber, Bracket = Bracket.Winner };
                if (home is int h1 && lastSeries.TryGetValue(h1, out var homePrevious))
                    info.HomeTeamPreviousMatchBracketInfo = homePrevious;
                else
                    info.HomeTeamSeedNumber = ++seed;
                if (away is int a1 && lastSeries.TryGetValue(a1, out var awayPrevious))
                    info.AwayTeamPreviousMatchBracketInfo = awayPrevious;
                else
                    info.AwayTeamSeedNumber = ++seed;
                if (home is int h2)
                    lastSeries[h2] = info;
                if (away is int a2)
                    lastSeries[a2] = info;
                match.BracketMatch = info;
            }
            _context.SeasonMatches.Add(match);
            counts.SeasonMatchesCreated++;
            counts.GamesLinked += played.Count;
        }
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The season's recorded games (MCC carnage reports and the like), upserted by their SourceKey: each player resolved
    /// as the sheets' are (<see cref="PlayerResolver"/>, a placeholder when nobody matches), each side mapped to the season
    /// team most of its players are on (its recorded name otherwise). A game that is a Waypoint game's duplicate (the same
    /// match ID, or the same two teams within <see cref="HistoricalImportOptions.RecordedGameWaypointWindow"/> of one) is
    /// skipped, and removed if an earlier import added it: the Waypoint game wins. The season's recorded games the archive
    /// no longer lists go. Returns the games for <see cref="LinkSeries"/>: those not in a series yet, scrims aside (a scrim
    /// is in none, and leaves one an earlier import put it in; it counts for the season through its SeasonID).
    /// </summary>
    private async Task<List<ImportedGame>> ImportRecordedGames(SeasonGroup group, Season season, List<GameVersion> gameVersions, PlayerResolver resolver, SeasonTeams teams, List<ImportedGame> waypointGames, HistoricalImportReport counts, CancellationToken ct)
    {
        var inputs = group.RecordedGames!;
        var keys = inputs.Select(i => i.SourceKey.Trim()).ToList();
        var existing = await _context.RecordedGames
            .Where(g => keys.Contains(g.SourceKey) || g.SeasonID == season.SeasonID)
            .Include(g => g.Teams).Include(g => g.Participants).Include(g => g.SeriesGame)
            .ToListAsync(ct);
        var bySourceKey = existing.ToDictionary(g => g.SourceKey, StringComparer.OrdinalIgnoreCase);

        // The Waypoint games to compare against: the season's in series already and the ones about to be
        var waypoint = await _context.SeriesGames
            .Where(l => l.MatchID != null && l.SeasonMatch.SeasonID == season.SeasonID)
            .Select(l => new { l.Match!.StartTime, l.SeasonMatch.HomeTeamID, l.SeasonMatch.AwayTeamID })
            .ToListAsync(ct);
        var waypointPairs = waypoint.Select(w => (Start: w.StartTime, Low: Min(w.HomeTeamID, w.AwayTeamID), High: Max(w.HomeTeamID, w.AwayTeamID)))
            .Concat(waypointGames.Where(g => g.MatchID is not null).Select(g => (g.Start, Low: Min(g.TeamA, g.TeamB), High: Max(g.TeamA, g.TeamB))))
            .ToList();
        var matchIds = inputs.Select(i => i.WaypointMatchId is null ? null : HistoricalKeys.MatchId(i.WaypointMatchId)).OfType<Guid>().ToList();
        var pulled = (await _context.Matches.Where(m => matchIds.Contains(m.MatchID)).Select(m => m.MatchID).ToListAsync(ct)).ToHashSet();

        var kept = new HashSet<RecordedGame>(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toLink = new List<ImportedGame>();
        foreach (var input in inputs)
        {
            var key = input.SourceKey.Trim();
            var gameVersion = gameVersions.FirstOrDefault(g => string.Equals(g.Slug, input.GameVersion, StringComparison.OrdinalIgnoreCase));
            if (gameVersion is null)
            {
                Skip(counts, $"Recorded game {key}: unknown game version '{input.GameVersion}'");
                continue;
            }
            if (!seen.Add(key))
            {
                Skip(counts, $"Recorded game {key}: listed twice");
                continue;
            }
            if (input.WaypointMatchId is not null && HistoricalKeys.MatchId(input.WaypointMatchId) is Guid matchId && pulled.Contains(matchId))
            {
                SkipAsWaypoint(key, $"it is Waypoint match {matchId}");
                continue;
            }

            // Players and sides: each side is the season team most of its players are on
            var players = new List<(RecordedPlayerInput Input, int UserId)>();
            foreach (var player in input.Players.Where(p => !string.IsNullOrWhiteSpace(p.Gamertag)))
            {
                var userId = (await resolver.Resolve(player.Gamertag, null, ct)).UserId;
                if (players.Any(p => p.UserId == userId))
                {
                    Skip(counts, $"Recorded game {key}: {player.Gamertag} is listed twice for one player");
                    continue;
                }
                players.Add((player, userId));
            }
            // A side listed twice is taken once
            var sides = input.Teams.GroupBy(t => t.Side).Select(g => g.First()).OrderBy(t => t.Side).ToList();
            var sideTeams = sides.ToDictionary(t => t.Side, t => MajorityTeam(players.Where(p => p.Input.Side == t.Side).Select(p => p.UserId), teams) ?? teams.Find(t.Name)?.TeamID);
            var (a, b) = (sides.Count > 0 ? sideTeams[sides[0].Side] : null, sides.Count > 1 ? sideTeams[sides[1].Side] : null);
            if (a is not null && a == b)
            {
                Warn(counts, $"Recorded game {key}: both sides are {teams.ByName.Values.First(t => t.TeamID == a).TeamName}; linked without teams");
                (a, b) = (null, null);
                foreach (var side in sides)
                    sideTeams[side.Side] = null;
            }
            else if ((a is null || b is null) && teams.ByName.Count > 0)
                Warn(counts, $"Recorded game {key}: a side is none of the season's teams (by its players' teams or its name)");

            // The Waypoint game of the same two teams around the same time is this game
            if (a is not null && b is not null && input.PlayedAt is DateTime playedAt
                && waypointPairs.Any(w => w.Low == Min(a, b) && w.High == Max(a, b) && w.Start is DateTime start && (start - playedAt).Duration() <= _options.RecordedGameWaypointWindow))
            {
                SkipAsWaypoint(key, $"the same two teams played a Waypoint game within {_options.RecordedGameWaypointWindow} of it");
                continue;
            }

            // Found again by its key (an earlier import's), or new
            if (!bySourceKey.TryGetValue(key, out var game))
                _context.RecordedGames.Add(game = new RecordedGame { SourceKey = key });
            kept.Add(game);
            game.GameVersionID = gameVersion.GameVesionID;
            game.SeasonID = season.SeasonID;
            game.Map = Truncate(input.Map, 100);
            game.Gametype = Truncate(input.Gametype, 100);
            game.PlayedAt = input.PlayedAt;
            game.IsPlayoffs = input.IsPlayoffs && season.Format != SeasonFormat.LateLeague;

            // Sides by number and players by user, updated in place: a re-run of the same archive changes nothing
            foreach (var stale in game.Teams.Where(t => !sides.Any(s => s.Side == t.Side)).ToList())
                game.Teams.Remove(stale);
            foreach (var side in sides)
            {
                var row = game.Teams.FirstOrDefault(t => t.Side == side.Side);
                if (row is null)
                    game.Teams.Add(row = new RecordedGameTeam { Side = side.Side });
                row.TeamID = sideTeams[side.Side];
                row.Name = side.Name is null ? null : Truncate(HistoricalKeys.CleanGamertag(side.Name), Team.TeamNameColumnLength);
                row.Outcome = side.Outcome;
                row.Score = side.Score;
            }
            foreach (var stale in game.Participants.Where(p => !players.Any(x => x.UserId == p.UserID)).ToList())
                game.Participants.Remove(stale);
            foreach (var (player, userId) in players)
            {
                var row = game.Participants.FirstOrDefault(p => p.UserID == userId);
                if (row is null)
                    game.Participants.Add(row = new RecordedGameParticipant { UserID = userId });
                row.RecordedGameTeam = game.Teams.FirstOrDefault(t => t.Side == player.Side);
                row.Gamertag = Truncate(HistoricalKeys.CleanGamertag(player.Gamertag), 50)!;
                row.Kills = player.Kills;
                row.Deaths = player.Deaths;
                row.Assists = player.Assists;
                row.Goals = player.Goals;
                row.Score = player.Score;
                row.CarrierKills = player.CarrierKills;
                row.Betrayals = player.Betrayals;
                row.Suicides = player.Suicides;
                row.HammerKills = player.HammerKills;
                row.SwordKills = player.SwordKills;
                row.BallKills = player.BallKills;
                row.Punches = player.Punches;
            }
            counts.RecordedGamesImported++;

            if (input.IsScrim)
            {
                // Not in a series: out of one an earlier import put it in
                if (game.SeriesGame is not null)
                    await Unlink(game, ct);
                continue;
            }
            if (game.SeriesGame is null)
                toLink.Add(new ImportedGame(null, game, input.PlayedAt, game.IsPlayoffs, a,
                    sides.Count > 0 ? sides[0].Outcome : Outcomes.DidNotFinish, b, sides.Count > 1 ? sides[1].Outcome : Outcomes.DidNotFinish));
        }

        // Games of earlier imports this one doesn't bring back (gone from the archive, or a Waypoint game's duplicate now) go
        foreach (var stale in existing.Where(g => !kept.Contains(g)).ToList())
        {
            if (stale.SeriesGame is not null)
                await Unlink(stale, ct);
            _context.RecordedGames.Remove(stale);
            counts.RecordedGamesRemoved++;
        }
        await _context.SaveChangesAsync(ct);
        return toLink;

        void SkipAsWaypoint(string key, string why)
        {
            counts.RecordedGamesSkippedAsWaypoint++;
            Warn(counts, $"Recorded game {key}: skipped, {why}");
        }
    }

    /// <summary>Takes a recorded game out of its series; a series left with no games goes.</summary>
    private async Task Unlink(RecordedGame game, CancellationToken ct)
    {
        var seriesGame = game.SeriesGame!;
        _context.SeriesGames.Remove(seriesGame);
        game.SeriesGame = null;
        await _context.SaveChangesAsync(ct);
        if (!await _context.SeriesGames.AnyAsync(l => l.SeasonMatchID == seriesGame.SeasonMatchID, ct))
        {
            var match = await _context.SeasonMatches.Include(m => m.BracketMatch).FirstAsync(m => m.SeasonMatchID == seriesGame.SeasonMatchID, ct);
            _context.SeasonMatches.Remove(match);
            await _context.SaveChangesAsync(ct);
        }
    }

    private static string? Truncate(string? value, int length) => value is null ? null : value.Trim().Length > length ? value.Trim()[..length] : value.Trim();

    /// <summary>The sheet's rows. Returns, by phase, each row's player (for roster suggestions).</summary>
    private async Task<Dictionary<bool, List<SheetPlayer>>> ImportManualStats(SeasonGroup group, Season season, Dictionary<bool, HashSet<int>> waypointUsers, PlayerResolver resolver, SeasonTeams teams, HashSet<PlayerSeasonStat> kept, HistoricalImportReport counts, CancellationToken ct)
    {
        var sheetPlayers = new Dictionary<bool, List<SheetPlayer>> { [false] = [], [true] = [] };
        var seen = new HashSet<(int UserID, int StintIndex, bool IsPlayoffs)>();
        // The season's rows, by key, loaded once: each sheet row updates its own or is added, and the rows of this run are
        // kept; the rest (a player taken off the sheet, a gamertag corrected) go when the season is reconciled
        var existing = await _context.PlayerSeasonStats.Where(s => s.SeasonID == season.SeasonID)
            .ToDictionaryAsync(s => (s.UserID, s.StintIndex, s.IsPlayoffs), ct);
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
                // On the team's roster even when the player's Waypoint stats replace the row
                var teamId = teams.Join(player.TeamName, userId);
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
                    GameVersionID = group.EntryGames[entry].GameVesionID,
                    Gamertag = HistoricalKeys.CleanGamertag(player.Gamertag),
                    TeamID = teamId,
                    TradeNote = player.TradeNote,
                    RoleNote = player.RoleNote,
                    StintIndex = stint,
                    IsPlayoffs = entry.IsPlayoffs,
                    IsCombined = entry.IsCombined,
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
                if (existing.TryGetValue((row.UserID, row.StintIndex, row.IsPlayoffs), out var stored))
                {
                    row.PlayerSeasonStatID = stored.PlayerSeasonStatID;
                    row.CreatedAt = stored.CreatedAt;
                    row.CreatedByID = stored.CreatedByID;
                    row.ModifiedAt = stored.ModifiedAt;
                    row.ModifiedByID = stored.ModifiedByID;
                    // Only the values that changed are written (and get a history row): a re-run of the same archive changes nothing
                    _context.Entry(stored).CurrentValues.SetValues(row);
                    kept.Add(stored);
                    counts.StatsUpdated++;
                }
                else
                {
                    _context.PlayerSeasonStats.Add(row);
                    kept.Add(row);
                    counts.StatsInserted++;
                }
            }
        }
        await _context.SaveChangesAsync(ct);
        return sheetPlayers;
    }

    /// <summary>
    /// For each phase with Waypoint stats: sheet rows no Xbox account matched (their user has no Waypoint row) and Xbox
    /// accounts no sheet row matched are probably the same players under old gamertags. Pairs on the same team (an
    /// account's team is its teammates' sheet team, by majority) become suggestions for a sysadmin (GamertagClaims,
    /// RosterMatch); nothing is merged automatically. The unmatched rows are warned about.
    /// </summary>
    private async Task SuggestRosterMatches(SeasonGroup group, Season season, SeasonTeams teams, Dictionary<bool, HashSet<int>> waypointUsers, Dictionary<bool, List<SheetPlayer>> sheetPlayers, HistoricalImportReport counts, CancellationToken ct)
    {
        var matchPhases = WaypointIds(group, null);
        var rowUsers = sheetPlayers.Values.SelectMany(p => p).Select(p => p.UserId).Distinct().ToList();
        var placeholders = (await _context.Users.Where(u => rowUsers.Contains(u.Id) && u.IsDummyUser).Select(u => u.Id).ToListAsync(ct)).ToHashSet();
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
                    if (!placeholders.Contains(row.UserId))
                        continue;
                    if (await _matching.ClaimAsync(GamertagClaimKind.RosterMatch, xuid, row.Gamertag, row.UserId, season.SeasonID, teams.Find(row.TeamName)?.TeamID, evenIfRejected: false, ct) is not null)
                        counts.SuggestionsCreated++;
                }
            }
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Each tier's title goes to its team (one the season's teams, made by <see cref="UpsertTeams"/>) through
    /// <see cref="ChampionshipService"/>, as a title won in the app does: a row per player on the record's roster, its
    /// captain flagged. The roster's players are on the team, and the captain is its captain.
    /// </summary>
    private async Task<HashSet<string>> ImportChampionships(SeasonGroup group, Season season, List<GameVersion> gameVersions, PlayerResolver resolver, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var tiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // A Late League season never crowns a champion: a title the record gives it is left out (and one imported before goes)
        if (season.Format == SeasonFormat.LateLeague)
        {
            foreach (var championship in group.Championships)
                Skip(counts, $"{group.SourceId} {championship.Tier} champions: a Late League season has no champion");
            return tiers;
        }
        foreach (var tier in group.Championships.GroupBy(c => c.Tier.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            tiers.Add(tier.Key);
            var championship = tier.First();
            foreach (var other in tier.Skip(1))
                Skip(counts, $"{group.SourceId} {tier.Key} champions: a second title for the tier ({other.Team})");
            var team = teams.Find(championship.Team);
            if (team is null)
            {
                Skip(counts, $"{group.SourceId} {tier.Key} champions: no team named");
                continue;
            }
            var members = new List<ChampionMember>();
            foreach (var gamertag in championship.Roster.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                var userId = (await resolver.Resolve(gamertag, null, ct)).UserId;
                if (members.Any(m => m.UserID == userId))
                {
                    Skip(counts, $"{group.SourceId} {tier.Key} champions: {gamertag} is listed twice for one player");
                    continue;
                }
                var isCaptain = championship.Captain is not null && HistoricalKeys.Normalize(championship.Captain) == HistoricalKeys.Normalize(gamertag);
                members.Add(new ChampionMember(userId, HistoricalKeys.CleanGamertag(gamertag), isCaptain));
                teams.Join(team.TeamName, userId);
                if (isCaptain)
                    teams.Captains[team.TeamID] = userId;
            }
            var slug = HistoricalKeys.GameVersionSlug(championship.Game);
            var gameVersionId = gameVersions.FirstOrDefault(g => g.Slug == slug)?.GameVesionID;
            var changes = await _championships.SetChampionsAsync(season.SeasonID, tier.Key, team.TeamID, members, gameVersionId, ct);
            counts.ChampionshipsInserted += changes.Inserted;
            counts.ChampionshipsUpdated += changes.Updated;
            counts.ChampionshipsRemoved += changes.Removed;
        }
        return tiers;
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
        report.TeamsCreated += season.TeamsCreated;
        report.TeamsRemoved += season.TeamsRemoved;
        report.StatsRemoved += season.StatsRemoved;
        report.ChampionshipsRemoved += season.ChampionshipsRemoved;
        report.StatsInserted += season.StatsInserted;
        report.StatsUpdated += season.StatsUpdated;
        report.SeasonMatchesCreated += season.SeasonMatchesCreated;
        report.GamesLinked += season.GamesLinked;
        report.SeasonMatchesRemoved += season.SeasonMatchesRemoved;
        report.RecordedGamesImported += season.RecordedGamesImported;
        report.RecordedGamesSkippedAsWaypoint += season.RecordedGamesSkippedAsWaypoint;
        report.RecordedGamesRemoved += season.RecordedGamesRemoved;
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
