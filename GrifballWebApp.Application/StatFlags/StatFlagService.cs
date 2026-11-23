using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Match = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Application.StatFlags;

/// <summary>
/// Plain-rule anomaly checks for game stats (#548): runs <see cref="StatFlagRule"/>'s rules against a game just saved -
/// by hand (<see cref="CheckRecordedGameAsync"/>, for a manual game), by a Halo pull (<see cref="CheckMatchAsync"/>), or
/// by the historical import (<see cref="CheckRecordedGameAsync"/> again, called the same way, or batched -
/// <see cref="CheckRecordedGamesAsync"/>) - and reconciles the result onto the game's <see cref="StatFlag"/>s: an open
/// flag no longer found is removed, one still found is brought up to date in place (never doubled), and a new finding
/// is added. A flag never changes the game it's about: it only tells staff something is worth a look
/// (<see cref="StaffStatFlagService"/> is how they see and dismiss them).
///
/// Every public method here is safe to call after the action it follows is already decided: a failure (a transient DB
/// error, a translation bug) is logged and let go, detaching anything of its own left tracked, rather than rolling
/// back a commissioner's save or an import. Called after the save that made the game real (its key is real), never
/// inside the transaction that makes it, so a check failing never takes the save down with it - on Postgres a failing
/// statement aborts the whole transaction outright (25P02), not only the statement that failed, so this holds even
/// though every check here already catches its own failure (third review).
/// </summary>
public class StatFlagService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    private readonly ILogger<StatFlagService> _logger;
    private readonly StatFlagOptions _options;

    public StatFlagService(IApplicationDbContext context, TimeProvider? time = null, ILogger<StatFlagService>? logger = null, IOptions<StatFlagOptions>? options = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<StatFlagService>.Instance;
        _options = options?.Value ?? new StatFlagOptions();
    }

    /// <summary>What one rule found: <paramref name="PlayerUserID"/> is null when the rule is about the whole game (a
    /// team, the series), not one player. <paramref name="Discriminator"/> tells apart two findings of the same rule
    /// with no player (a side's number, a Halo player by XUID): without it they'd share a reconciliation key - and,
    /// for a Halo match's per-player findings, it is always the player's XUID, linked or not, so linking an account
    /// never changes a finding's identity (third review): see <see cref="IdentityMatches"/>.</summary>
    private sealed record Finding(StatFlagRule Rule, int? PlayerUserID, string Reason, long? Discriminator = null);

    /// <summary>Every "any game" rule run on a recorded game.</summary>
    private static readonly HashSet<StatFlagRule> RecordedGameRules = new() { StatFlagRule.NegativeStat };

    /// <summary>Every hand-entered rule, run only for <see cref="RecordedGame.IsManual"/>.</summary>
    private static readonly HashSet<StatFlagRule> HandEnteredRules = new()
    {
        StatFlagRule.ScoreDoesNotMatchGoals, StatFlagRule.WinnerDoesNotMatchScores,
        StatFlagRule.TeamSmallerThanFormat, StatFlagRule.DateOutsideSeason,
    };

    /// <summary>Every rule run on a Halo Infinite match.</summary>
    private static readonly HashSet<StatFlagRule> MatchRules = new()
        { StatFlagRule.NegativeStat, StatFlagRule.ImpossibleAccuracy, StatFlagRule.UndecodedCustomScore };

    /// <summary>The one rule a season's series is checked by.</summary>
    private static readonly HashSet<StatFlagRule> SeriesRules = new() { StatFlagRule.SeriesDoesNotMatchGames };

    /// <summary>
    /// Runs every rule that applies to <paramref name="game"/> (the "any game" rules always; the hand-entered ones only
    /// for a game entered by hand, <see cref="RecordedGame.IsManual"/>) and reconciles the result onto its flags, never
    /// touching a flag of a rule this game isn't checked by (<c>ownedRules</c>) - a StatFlagRule.SeriesDoesNotMatchGames
    /// flag the series check left on this same game, say. Called after the game itself is saved (its RecordedGameID is
    /// a real one): by manual match entry, right after <c>AddManualGame</c> or <c>EditManualGame</c> save, with the
    /// season match already loaded there (<paramref name="seasonMatch"/>); by the historical import, with no season
    /// match (a game it imports always counts through its series, never <see cref="RecordedGame.IsManual"/>, so the
    /// hand-entered rules never run there) - or, for many games at once, <see cref="CheckRecordedGamesAsync"/>.
    /// </summary>
    public virtual Task CheckRecordedGameAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct = default) =>
        SafelyAsync(() => CheckRecordedGameCoreAsync(game, seasonMatch, ct), $"recorded game {game.RecordedGameID}");

    private async Task CheckRecordedGameCoreAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var findings = new List<Finding>();
        findings.AddRange(NegativeStatFindings(game));
        var ownedRules = RecordedGameRules;
        if (game.IsManual)
        {
            findings.AddRange(await HandEnteredFindingsAsync(game, seasonMatch, ct));
            ownedRules = new HashSet<StatFlagRule>(RecordedGameRules.Concat(HandEnteredRules));
        }
        await ReconcileAsync(null, game.RecordedGameID, null, findings, now, ownedRules, ct);
    }

    /// <summary>
    /// The "any game" rules (never manual: the historical import's recorded games, <see cref="RecordedGame.IsManual"/>
    /// is always false for them) for many games at once - the import's own, batched rather than one
    /// <see cref="CheckRecordedGameAsync"/> a game: one query for every game's existing flags, one save for all of
    /// them.
    /// </summary>
    public virtual Task CheckRecordedGamesAsync(IReadOnlyCollection<RecordedGame> games, CancellationToken ct = default) =>
        SafelyAsync(() => CheckRecordedGamesCoreAsync(games, ct), $"{games.Count} recorded game(s)");

    private async Task CheckRecordedGamesCoreAsync(IReadOnlyCollection<RecordedGame> games, CancellationToken ct)
    {
        if (games.Count == 0)
            return;
        var now = _time.GetUtcNow().UtcDateTime;
        var gameIDs = games.Select(g => g.RecordedGameID).ToList();
        var existingByGame = (await _context.StatFlags
                .Where(f => f.RecordedGameID != null && gameIDs.Contains(f.RecordedGameID.Value))
                .Where(f => RecordedGameRules.Contains(f.Rule))
                .ToListAsync(ct))
            .GroupBy(f => f.RecordedGameID!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var anyChange = false;
        foreach (var game in games)
        {
            var findings = NegativeStatFindings(game).ToList();
            var existing = existingByGame.GetValueOrDefault(game.RecordedGameID, []);
            anyChange |= ReconcileInMemory(null, game.RecordedGameID, null, findings, existing, now);
        }
        if (anyChange)
            await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Runs the "any game" rules that apply to a Halo Infinite match (accuracy and shots, negative stats, an undecoded
    /// custom score) and reconciles the result onto its flags. Called from the one place every Halo pull saves a match
    /// (DataPullService.SaveMatch), once the match itself is saved, so a match is checked once whether it ends up
    /// reported to a season match or not - and again whenever it's decoded again, one at a time
    /// (<see cref="CheckMatchesAsync"/> is the batched way, CustomScoreLayouts.DecodeAsync's). <paramref name="match"/>
    /// is already loaded with its teams and participants; <paramref name="userIDByXuid"/> and
    /// <paramref name="gamertagByXuid"/>, when the caller already has them (a bulk pull, looking them up once for every
    /// match rather than once a match), are used as given - otherwise looked up here, for a match checked on its own.
    /// <paramref name="isNewMatch"/>, true for a match this call is saving for the first time, skips the
    /// existing-flags query: it can't have any yet.
    /// </summary>
    public virtual Task CheckMatchAsync(Match match, IReadOnlyDictionary<long, int>? userIDByXuid = null,
        IReadOnlyDictionary<long, string>? gamertagByXuid = null, bool isNewMatch = false, CancellationToken ct = default) =>
        SafelyAsync(() => CheckMatchCoreAsync(match, userIDByXuid, gamertagByXuid, isNewMatch, ct), $"match {match.MatchID}");

    private async Task CheckMatchCoreAsync(Match match, IReadOnlyDictionary<long, int>? userIDByXuid,
        IReadOnlyDictionary<long, string>? gamertagByXuid, bool isNewMatch, CancellationToken ct)
    {
        (userIDByXuid, gamertagByXuid) = await ResolvedAsync([match], userIDByXuid, gamertagByXuid, ct);
        var findings = MatchFindings(match, userIDByXuid, gamertagByXuid, _options);
        await ReconcileAsync(match.MatchID, null, null, findings, _time.GetUtcNow().UtcDateTime, MatchRules, ct, skipExistingQuery: isNewMatch);
    }

    /// <summary>
    /// <see cref="CheckMatchAsync"/> for many matches at once - CustomScoreLayouts.DecodeAsync's, once a chunk of
    /// matches is re-decoded (third review: was one <see cref="CheckMatchAsync"/> a match, a lookup, a flags query and
    /// maybe a save each): one lookup for every match's players, one flags query, one save for them all.
    /// </summary>
    public virtual Task CheckMatchesAsync(IReadOnlyCollection<Match> matches, CancellationToken ct = default) =>
        SafelyAsync(() => CheckMatchesCoreAsync(matches, ct), $"{matches.Count} match(es)");

    private async Task CheckMatchesCoreAsync(IReadOnlyCollection<Match> matches, CancellationToken ct)
    {
        if (matches.Count == 0)
            return;
        var (userIDByXuid, gamertagByXuid) = await ResolvedAsync(matches, null, null, ct);

        var matchIDs = matches.Select(m => m.MatchID).ToList();
        var existingByMatch = (await _context.StatFlags
                .Where(f => f.MatchID != null && matchIDs.Contains(f.MatchID.Value))
                .Where(f => MatchRules.Contains(f.Rule))
                .ToListAsync(ct))
            .GroupBy(f => f.MatchID!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var now = _time.GetUtcNow().UtcDateTime;
        var anyChange = false;
        foreach (var match in matches)
        {
            var findings = MatchFindings(match, userIDByXuid, gamertagByXuid, _options);
            var existing = existingByMatch.GetValueOrDefault(match.MatchID, []);
            anyChange |= ReconcileInMemory(match.MatchID, null, null, findings, existing, now);
        }
        if (anyChange)
            await _context.SaveChangesAsync(ct);
    }

    /// <summary><paramref name="userIDByXuid"/> and <paramref name="gamertagByXuid"/> as given, or looked up once for
    /// every one of <paramref name="matches"/>' players.</summary>
    private async Task<(IReadOnlyDictionary<long, int> UserIDByXuid, IReadOnlyDictionary<long, string> GamertagByXuid)> ResolvedAsync(
        IReadOnlyCollection<Match> matches, IReadOnlyDictionary<long, int>? userIDByXuid, IReadOnlyDictionary<long, string>? gamertagByXuid, CancellationToken ct)
    {
        if (userIDByXuid is not null && gamertagByXuid is not null)
            return (userIDByXuid, gamertagByXuid);
        var xuids = matches.SelectMany(m => m.MatchTeams.SelectMany(t => t.MatchParticipants)).Select(p => p.XboxUserID).Distinct().ToList();
        userIDByXuid ??= await _context.Users.Where(u => u.XboxUserID != null && xuids.Contains(u.XboxUserID!.Value))
            .Select(u => new { XboxUserID = u.XboxUserID!.Value, u.Id })
            .ToDictionaryAsync(u => u.XboxUserID, u => u.Id, ct);
        gamertagByXuid ??= await _context.XboxUsers.Where(x => xuids.Contains(x.XboxUserID))
            .ToDictionaryAsync(x => x.XboxUserID, x => x.Gamertag, ct);
        return (userIDByXuid, gamertagByXuid);
    }

    private static List<Finding> MatchFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid, IReadOnlyDictionary<long, string> gamertagByXuid, StatFlagOptions options)
    {
        var findings = new List<Finding>();
        findings.AddRange(NegativeStatFindings(match, userIDByXuid, gamertagByXuid));
        findings.AddRange(AccuracyFindings(match, userIDByXuid, gamertagByXuid, options));
        if (match.ScoreLayout is CustomScoreLayout.Undecoded)
            findings.Add(new Finding(StatFlagRule.UndecodedCustomScore, null,
                "The match's custom game mode couldn't be decoded (#217): its players' scores are still Halo's packed numbers, not goals"));
        return findings;
    }

    /// <summary>
    /// A season match's games against its series result (StatFlagRule.SeriesDoesNotMatchGames): only when every one of
    /// its games is a recorded game (a manual game, or the historical import's), never a Halo pull. The finding is
    /// about the series as a whole, never one of its games (<see cref="StatFlag.SeasonMatchID"/>, third review: not
    /// attached to its "last" recorded game any more) - so adding, replacing or removing a game, undoing or
    /// forfeiting the series, or a Waypoint game joining it (none of which ever call anything else of this service for
    /// the game taken out) only ever need this one call afterwards to pick the change up, whatever it was. Called
    /// whenever any of that happens (SeasonMatchService), and after the historical import re-imports or re-links a
    /// series (<see cref="CheckSeriesGamesAsync(IReadOnlyCollection{int}, CancellationToken)"/> there, batched).
    /// </summary>
    public virtual Task CheckSeriesGamesAsync(int seasonMatchID, CancellationToken ct = default) =>
        SafelyAsync(() => CheckSeriesGamesCoreAsync([seasonMatchID], ct), $"season match {seasonMatchID}'s series");

    /// <summary><see cref="CheckSeriesGamesAsync(int, CancellationToken)"/> for many series at once - the historical
    /// import's, batched (third review: was one series at a time, each its own split-query reload, flags query and
    /// maybe save): one load for every series named, one flags query, one save for them all.</summary>
    public virtual Task CheckSeriesGamesAsync(IReadOnlyCollection<int> seasonMatchIDs, CancellationToken ct = default) =>
        SafelyAsync(() => CheckSeriesGamesCoreAsync(seasonMatchIDs, ct), $"{seasonMatchIDs.Count} season match(es)' series");

    private async Task CheckSeriesGamesCoreAsync(IReadOnlyCollection<int> seasonMatchIDs, CancellationToken ct)
    {
        if (seasonMatchIDs.Count == 0)
            return;
        var seasonMatches = await _context.SeasonMatches
            .Where(m => seasonMatchIDs.Contains(m.SeasonMatchID))
            .Include(m => m.SeriesGames).ThenInclude(g => g.RecordedGame!.Teams)
            .Include(m => m.SeriesGames).ThenInclude(g => g.Match!.MatchTeams).ThenInclude(t => t.MatchParticipants)
            .AsSplitQuery()
            .ToListAsync(ct);
        if (seasonMatches.Count == 0)
            return; // every one of them is gone already: its flag went with it (StatFlag.SeasonMatchID cascades)

        var ids = seasonMatches.Select(m => m.SeasonMatchID).ToList();
        // One query for every series' existing flag, one save for them all: not once a series.
        var existingByMatch = (await _context.StatFlags
                .Where(f => f.SeasonMatchID != null && ids.Contains(f.SeasonMatchID.Value))
                .Where(f => SeriesRules.Contains(f.Rule))
                .ToListAsync(ct))
            .GroupBy(f => f.SeasonMatchID!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var now = _time.GetUtcNow().UtcDateTime;
        var anyChange = false;
        foreach (var seasonMatch in seasonMatches)
        {
            var findings = await SeriesFindingsAsync(seasonMatch, ct);
            var existing = existingByMatch.GetValueOrDefault(seasonMatch.SeasonMatchID, []);
            anyChange |= ReconcileInMemory(null, null, seasonMatch.SeasonMatchID, findings, existing, now);
        }
        if (anyChange)
            await _context.SaveChangesAsync(ct);
    }

    /// <summary>Whether <paramref name="seasonMatch"/>'s games add up to its result, decided by games at all. A
    /// Waypoint game's side is worked out the same way reporting one does (SeasonMatchService.WaypointWinners,
    /// SeriesRosters/SeriesSides - fourth review: a series with one used to be skipped entirely). A game whose
    /// players match neither roster any more (a trade or a merge since) can't be decided; flagged for that by name,
    /// rather than silently skipped.</summary>
    private async Task<List<Finding>> SeriesFindingsAsync(SeasonMatch seasonMatch, CancellationToken ct)
    {
        if (seasonMatch.SeriesGames.Count == 0 || seasonMatch.DecidedBy is not SeasonMatchDecision.Games)
            return [];
        var rosters = await SeriesRosters.LoadAsync(_context, seasonMatch.HomeTeamID, seasonMatch.AwayTeamID, ct);
        var (home, away, undecidable) = SeriesWins(seasonMatch, rosters);
        if (undecidable.Count > 0)
            return [new Finding(StatFlagRule.SeriesDoesNotMatchGames, null,
                $"Game {string.Join(", ", undecidable)} (from Waypoint) no longer matches either roster (a trade or a merge since): the series can't be checked against its games")];
        if (home == (seasonMatch.HomeTeamScore ?? 0) && away == (seasonMatch.AwayTeamScore ?? 0))
            return [];
        return [new Finding(StatFlagRule.SeriesDoesNotMatchGames, null,
            $"The series shows {seasonMatch.HomeTeamScore}-{seasonMatch.AwayTeamScore}, but its {seasonMatch.SeriesGames.Count} game(s) add up to {home}-{away}")];
    }

    /// <summary>
    /// Each side's wins in the series. A recorded game's side is counted the same way the historical import counts
    /// them (HistoricalImportService.Wins, third review): by its recorded side (1 or 2) against the series' home and
    /// away team, the other way round when the teams played that particular game swapped - never by matching a side's
    /// <see cref="RecordedGameTeam.TeamID"/> to the series' (<see cref="RecordedGame.WonBy"/>), which undercounts a
    /// side the import never resolved to a season team (a null TeamID): such a side still won or lost by its
    /// position, not by a team ID that was never there. Ambiguous (neither arrangement matches, a null TeamID on one
    /// or both sides) defaults to unswapped, the same default the import falls back to. A Waypoint game's side is
    /// decided by <see cref="SeriesSides.Of{T}"/> against <paramref name="rosters"/> (fourth review), the same rule
    /// reporting one scores it by; a game whose teams match neither roster is returned in <c>Undecidable</c> by its
    /// number, counted toward neither side.
    /// </summary>
    private static (int Home, int Away, List<int> Undecidable) SeriesWins(SeasonMatch seasonMatch, SeriesRosters rosters)
    {
        var home = 0;
        var away = 0;
        var undecidable = new List<int>();
        foreach (var g in seasonMatch.SeriesGames)
        {
            if (g.Match is { } match)
            {
                var wins = WaypointGameWins(match, rosters);
                if (wins is null)
                {
                    undecidable.Add(g.GameNumber);
                    continue;
                }
                (home, away) = (home + wins.Value.Home, away + wins.Value.Away);
                continue;
            }
            var recordedWins = RecordedGameWins(g.RecordedGame, seasonMatch);
            (home, away) = (home + recordedWins.Home, away + recordedWins.Away);
        }
        return (home, away, undecidable);
    }

    /// <summary>A Waypoint game's side, decided by <see cref="SeriesSides.Of{T}"/> against <paramref name="rosters"/> -
    /// null when that can't decide (not two teams, or both lean alike), the whole game then uncounted toward either
    /// side (fourth review).</summary>
    private static (int Home, int Away)? WaypointGameWins(Match match, SeriesRosters rosters)
    {
        var sides = SeriesSides.Of(match.MatchTeams.ToList(), t => rosters.Lean(t.MatchParticipants.Select(p => p.XboxUserID)));
        if (sides is null)
            return null;
        return (sides.Value.Home.Outcome is Outcomes.Won ? 1 : 0, sides.Value.Away.Outcome is Outcomes.Won ? 1 : 0);
    }

    /// <summary>A recorded game's side, by its recorded side (1 or 2) against the series' home and away team, the
    /// other way round when the teams played that particular game swapped (see <see cref="SeriesWins"/>'s own doc for
    /// why). A game with no two sides to read (a score-only game never built that far) counts toward neither.</summary>
    private static (int Home, int Away) RecordedGameWins(RecordedGame? recordedGame, SeasonMatch seasonMatch)
    {
        var sides = recordedGame?.Teams.OrderBy(t => t.Side).ToList();
        if (sides is not { Count: 2 })
            return (0, 0);
        var (a, b) = (sides[0], sides[1]);
        var swapped = (a.TeamID, b.TeamID) != (seasonMatch.HomeTeamID, seasonMatch.AwayTeamID)
            && (b.TeamID, a.TeamID) == (seasonMatch.HomeTeamID, seasonMatch.AwayTeamID);
        var (homeSide, awaySide) = swapped ? (b, a) : (a, b);
        return (homeSide.Outcome is Outcomes.Won ? 1 : 0, awaySide.Outcome is Outcomes.Won ? 1 : 0);
    }

    #region Hand-entered rules (RecordedGame.IsManual only)

    private async Task<List<Finding>> HandEnteredFindingsAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct)
    {
        var findings = new List<Finding>();
        findings.AddRange(ScoreDoesNotMatchGoalsFindings(game));
        findings.AddRange(WinnerDoesNotMatchScoresFindings(game));
        findings.AddRange(TeamSizeFindings(game, _options));
        if (seasonMatch is not null)
            findings.AddRange(await DateOutsideSeasonFindingsAsync(game, seasonMatch, ct));
        return findings;
    }

    /// <summary>The team scores don't match the players' goals added up: only checked for a side with at least one
    /// player whose goals were entered (a score-only game gives nothing to add up).</summary>
    private static IEnumerable<Finding> ScoreDoesNotMatchGoalsFindings(RecordedGame game)
    {
        foreach (var team in game.Teams)
        {
            var onSide = game.Participants.Where(p => p.RecordedGameTeamID == team.RecordedGameTeamID).ToList();
            if (onSide.Count == 0 || onSide.All(p => p.Goals is null))
                continue;
            var goals = onSide.Sum(p => p.Goals ?? 0);
            if (goals != (team.Score ?? 0))
                yield return new Finding(StatFlagRule.ScoreDoesNotMatchGoals, null,
                    $"{team.Name ?? $"Side {team.Side}"}'s score is {team.Score ?? 0}, but its players' goals add up to {goals}", Discriminator: team.Side);
        }
    }

    /// <summary>The side with the higher score isn't the one recorded as the winner. Entering a game always derives the
    /// winner from the scores (SeasonMatchService.SetManualSidesAndPlayers), so this should never fire; kept as a
    /// safety net against a future save path that sets them separately.</summary>
    private static IEnumerable<Finding> WinnerDoesNotMatchScoresFindings(RecordedGame game)
    {
        var sides = game.Teams.ToList();
        if (sides.Count != 2)
            yield break;
        var (a, b) = (sides[0], sides[1]);
        if ((a.Score ?? 0) == (b.Score ?? 0))
            yield break;
        var higher = (a.Score ?? 0) > (b.Score ?? 0) ? a : b;
        if (higher.Outcome is not Outcomes.Won)
            yield return new Finding(StatFlagRule.WinnerDoesNotMatchScores, null,
                $"{higher.Name ?? $"Side {higher.Side}"} has the higher score ({higher.Score}) but isn't recorded as the winner");
    }

    /// <summary>A side fielded fewer players than <see cref="StatFlagOptions.MinTeamSize"/>, or more than
    /// <see cref="StatFlagOptions.MaxTeamSize"/> (fourth review: was fewer than a fixed 4 only): only checked when
    /// players were entered at all (a score-only game says nothing about who played).</summary>
    private static IEnumerable<Finding> TeamSizeFindings(RecordedGame game, StatFlagOptions options)
    {
        if (game.Participants.Count == 0)
            yield break;
        foreach (var team in game.Teams)
        {
            var count = game.Participants.Count(p => p.RecordedGameTeamID == team.RecordedGameTeamID);
            if (count < options.MinTeamSize || count > options.MaxTeamSize)
                yield return new Finding(StatFlagRule.TeamSmallerThanFormat, null,
                    $"{team.Name ?? $"Side {team.Side}"} fielded {count} player(s); the season's format calls for {options.MinTeamSize}-{options.MaxTeamSize}", Discriminator: team.Side);
        }
    }

    /// <summary>The game's date is before the season started, or more than <see cref="StatFlagThresholds.FutureMargin"/>
    /// in the future (third review: was any amount, flagging a game just played against a clock a little fast).</summary>
    private async Task<List<Finding>> DateOutsideSeasonFindingsAsync(RecordedGame game, SeasonMatch seasonMatch, CancellationToken ct)
    {
        if (game.PlayedAt is not DateTime playedAt)
            return [];
        var now = _time.GetUtcNow().UtcDateTime;
        if (playedAt <= now + StatFlagThresholds.FutureMargin)
        {
            var seasonStart = await _context.Seasons.Where(s => s.SeasonID == seasonMatch.SeasonID).Select(s => s.SeasonStart).FirstOrDefaultAsync(ct);
            if (playedAt >= seasonStart)
                return [];
            return [new Finding(StatFlagRule.DateOutsideSeason, null, $"Played at {playedAt:yyyy-MM-dd}, before the season started ({seasonStart:yyyy-MM-dd})")];
        }
        return [new Finding(StatFlagRule.DateOutsideSeason, null, $"Played at {playedAt:yyyy-MM-dd HH:mm} UTC, in the future")];
    }

    #endregion

    #region Any-game rules

    private static IEnumerable<Finding> NegativeStatFindings(RecordedGame game)
    {
        foreach (var p in game.Participants)
        {
            var bad = NegativeStats(("kills", p.Kills), ("deaths", p.Deaths), ("assists", p.Assists), ("goals", p.Goals ?? 0));
            if (bad is not null)
                yield return new Finding(StatFlagRule.NegativeStat, p.UserID, $"{p.Gamertag}: {bad}");
        }
    }

    /// <summary>Score isn't checked here (unlike a recorded game's): a Halo match's Score includes suicides and
    /// betrayals, which normal play can easily make negative - and an undecoded custom score (StatFlagRule.UndecodedCustomScore)
    /// already flags that same match on its own. Kills, deaths, assists and goals, as the rule's own doc says.
    /// <see cref="Finding.Discriminator"/> is always the player's XUID (linked or not, third review): a Halo finding's
    /// identity never depends on <see cref="Finding.PlayerUserID"/>, which linking an account later changes.</summary>
    private static IEnumerable<Finding> NegativeStatFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid,
        IReadOnlyDictionary<long, string> gamertagByXuid)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var bad = NegativeStats(("kills", p.Kills), ("deaths", p.Deaths), ("assists", p.Assists), ("goals", p.Goals ?? 0));
            if (bad is not null)
                yield return new Finding(StatFlagRule.NegativeStat, PlayerID(p.XboxUserID, userIDByXuid),
                    $"{Gamertag(p.XboxUserID, gamertagByXuid)}: {bad}", Discriminator: p.XboxUserID);
        }
    }

    /// <summary>Which named stats, of what's given, are negative - joined into one sentence - or null when none are.</summary>
    private static string? NegativeStats(params (string Name, int Value)[] stats)
    {
        var bad = stats.Where(s => s.Value < 0).Select(s => $"{s.Value} {s.Name}").ToList();
        return bad.Count == 0 ? null : string.Join(", ", bad) + " (can't be negative)";
    }

    /// <summary>Accuracy over <see cref="StatFlagOptions.MaxAccuracy"/>, or shots hit above shots fired.
    /// <see cref="Finding.Discriminator"/> is always the player's XUID (see
    /// <see cref="NegativeStatFindings(Match, IReadOnlyDictionary{long, int}, IReadOnlyDictionary{long, string})"/>).</summary>
    private static IEnumerable<Finding> AccuracyFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid,
        IReadOnlyDictionary<long, string> gamertagByXuid, StatFlagOptions options)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var playerID = PlayerID(p.XboxUserID, userIDByXuid);
            if (p.ShotsHit > p.ShotsFired)
                yield return new Finding(StatFlagRule.ImpossibleAccuracy, playerID,
                    $"{Gamertag(p.XboxUserID, gamertagByXuid)}: {p.ShotsHit} shots hit, above {p.ShotsFired} shots fired", p.XboxUserID);
            else if (p.Accuracy > options.MaxAccuracy)
                yield return new Finding(StatFlagRule.ImpossibleAccuracy, playerID,
                    $"{Gamertag(p.XboxUserID, gamertagByXuid)}: {p.Accuracy:0.#}% accuracy", p.XboxUserID);
        }
    }

    private static int? PlayerID(long xboxUserID, IReadOnlyDictionary<long, int> userIDByXuid) =>
        userIDByXuid.GetValueOrDefault(xboxUserID) is int id and > 0 ? id : null;

    private static string Gamertag(long xboxUserID, IReadOnlyDictionary<long, string> gamertagByXuid) =>
        gamertagByXuid.GetValueOrDefault(xboxUserID, xboxUserID.ToString());

    #endregion

    /// <summary>
    /// Brings a game's flags up to date with what was just found, querying its existing ones first unless
    /// <paramref name="skipExistingQuery"/> (a match just inserted: it can't have any). See
    /// <see cref="ReconcileInMemory"/> for the rest.
    /// </summary>
    private async Task ReconcileAsync(Guid? matchID, int? recordedGameID, int? seasonMatchID, List<Finding> findings, DateTime now,
        HashSet<StatFlagRule> ownedRules, CancellationToken ct, bool skipExistingQuery = false)
    {
        var existing = skipExistingQuery
            ? []
            : await _context.StatFlags
                .Where(f => matchID != null ? f.MatchID == matchID : recordedGameID != null ? f.RecordedGameID == recordedGameID : f.SeasonMatchID == seasonMatchID)
                .Where(f => ownedRules.Contains(f.Rule))
                .ToListAsync(ct);

        if (ReconcileInMemory(matchID, recordedGameID, seasonMatchID, findings, existing, now))
            await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// An open flag for a (rule, player-or-discriminator) still found is rewritten in place - which only actually
    /// changes (and so is worth saving and publishing) anything when the reason itself did
    /// (<see cref="StatFlag.Rewrite"/>); one no longer found is removed (<see cref="StatFlag.Clear"/>, then); a new
    /// finding is added. A dismissed flag is left to <see cref="StatFlag.Rewrite"/>'s own rule (stays dismissed unless
    /// its reason changed). Nothing but <paramref name="existing"/> is read or touched, so a caller checking many
    /// games or series at once can batch the query and the save around many calls of this. Returns whether anything
    /// changed, so an all-quiet batch saves nothing.
    /// </summary>
    private bool ReconcileInMemory(Guid? matchID, int? recordedGameID, int? seasonMatchID, List<Finding> findings, List<StatFlag> existing, DateTime now)
    {
        var changed = false;
        foreach (var finding in findings)
        {
            var flag = existing.FirstOrDefault(f => IdentityMatches(finding.Rule, finding.PlayerUserID, finding.Discriminator, f));
            if (flag is null)
            {
                _context.StatFlags.Add(StatFlag.Found(finding.Rule, matchID, recordedGameID, seasonMatchID, finding.PlayerUserID, finding.Reason, now, finding.Discriminator));
                changed = true;
                continue;
            }
            if (flag.Rewrite(finding.Reason, now))
                changed = true;
            // Matched by discriminator (a Halo finding, third review): the player may have linked (or merged) since
            // the flag was found, which Rewrite itself never touches
            if (finding.Discriminator is not null && flag.PlayerUserID != finding.PlayerUserID)
            {
                flag.CatchUpPlayer(finding.PlayerUserID);
                changed = true;
            }
        }

        foreach (var stale in existing.Where(f => f.IsOpen && !findings.Any(fi => IdentityMatches(fi.Rule, fi.PlayerUserID, fi.Discriminator, f))))
        {
            stale.Clear();
            _context.StatFlags.Remove(stale);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Whether an existing flag (<paramref name="f"/>) is the same finding as one with this rule, player and
    /// discriminator: by <paramref name="discriminator"/> when the finding has one (a side's number, or - always, for
    /// a Halo match's per-player findings (third review) - a player's XUID, stable whether or not the account is
    /// linked); by <paramref name="playerUserID"/> otherwise (a recorded game's own per-player findings, which have no
    /// XUID to fall back on and so must key on the real user ID directly).
    /// </summary>
    private static bool IdentityMatches(StatFlagRule rule, int? playerUserID, long? discriminator, StatFlag f) =>
        f.Rule == rule && (discriminator is not null ? f.Discriminator == discriminator : f.PlayerUserID == playerUserID);

    /// <summary>
    /// Runs <paramref name="action"/>, logging and letting go of a failure (never <see cref="OperationCanceledException"/>)
    /// rather than passing it on: a check never rolls back the action it followed. Detaches anything of this check's
    /// own a failure left tracked (<see cref="StatFlag.Found"/>, <see cref="StatFlag.Rewrite"/> and the removal above
    /// all go through this context), so the next unrelated save on it doesn't try to save that too.
    /// </summary>
    private async Task SafelyAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Stat flag check failed ({What}); the action it followed went through regardless", what);
            foreach (var entry in _context.ChangeTracker.Entries<StatFlag>().Where(e => e.State is not EntityState.Unchanged).ToList())
                entry.State = EntityState.Detached;
        }
    }
}
