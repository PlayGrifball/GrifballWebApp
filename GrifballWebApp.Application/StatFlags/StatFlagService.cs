using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
/// inside the transaction that makes it, so a check failing never takes the save down with it.
/// </summary>
public class StatFlagService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    private readonly ILogger<StatFlagService> _logger;

    public StatFlagService(IApplicationDbContext context, TimeProvider? time = null, ILogger<StatFlagService>? logger = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<StatFlagService>.Instance;
    }

    /// <summary>What one rule found: <paramref name="PlayerUserID"/> is null when the rule is about the whole game (a
    /// team, the series), not one player. <paramref name="Discriminator"/> tells apart two findings of the same rule
    /// with no player (a side's number, an unlinked Halo player's XUID): without it they'd share a reconciliation key.</summary>
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
    public Task CheckRecordedGameAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct = default) =>
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
        await ReconcileAsync(null, game.RecordedGameID, findings, now, ownedRules, ct);
    }

    /// <summary>
    /// The "any game" rules (never manual: the historical import's recorded games, <see cref="RecordedGame.IsManual"/>
    /// is always false for them) for many games at once - the import's own, batched rather than one
    /// <see cref="CheckRecordedGameAsync"/> a game: one query for every game's existing flags, one save for all of
    /// them.
    /// </summary>
    public Task CheckRecordedGamesAsync(IReadOnlyCollection<RecordedGame> games, CancellationToken ct = default) =>
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
            anyChange |= ReconcileInMemory(null, game.RecordedGameID, findings, existing, now);
        }
        if (anyChange)
            await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Runs the "any game" rules that apply to a Halo Infinite match (accuracy and shots, negative stats, an undecoded
    /// custom score) and reconciles the result onto its flags. Called from the one place every Halo pull saves a match
    /// (DataPullService.SaveMatch), once the match itself is saved, so a match is checked once whether it ends up
    /// reported to a season match or not - and again whenever it's decoded again (CustomScoreLayouts.DecodeAsync, a
    /// variant's layout filled in by the backfill). <paramref name="match"/> is already loaded with its teams and
    /// participants; <paramref name="userIDByXuid"/> and <paramref name="gamertagByXuid"/>, when the caller already
    /// has them (a bulk pull, looking them up once for every match rather than once a match), are used as given -
    /// otherwise looked up here, for a match checked on its own. <paramref name="isNewMatch"/>, true for a match this
    /// call is saving for the first time, skips the existing-flags query: it can't have any yet.
    /// </summary>
    public Task CheckMatchAsync(Match match, IReadOnlyDictionary<long, int>? userIDByXuid = null,
        IReadOnlyDictionary<long, string>? gamertagByXuid = null, bool isNewMatch = false, CancellationToken ct = default) =>
        SafelyAsync(() => CheckMatchCoreAsync(match, userIDByXuid, gamertagByXuid, isNewMatch, ct), $"match {match.MatchID}");

    private async Task CheckMatchCoreAsync(Match match, IReadOnlyDictionary<long, int>? userIDByXuid,
        IReadOnlyDictionary<long, string>? gamertagByXuid, bool isNewMatch, CancellationToken ct)
    {
        var xuids = match.MatchTeams.SelectMany(t => t.MatchParticipants).Select(p => p.XboxUserID).Distinct().ToList();
        userIDByXuid ??= await _context.Users.Where(u => u.XboxUserID != null && xuids.Contains(u.XboxUserID!.Value))
            .Select(u => new { XboxUserID = u.XboxUserID!.Value, u.Id })
            .ToDictionaryAsync(u => u.XboxUserID, u => u.Id, ct);
        gamertagByXuid ??= await _context.XboxUsers.Where(x => xuids.Contains(x.XboxUserID))
            .ToDictionaryAsync(x => x.XboxUserID, x => x.Gamertag, ct);

        var findings = new List<Finding>();
        findings.AddRange(NegativeStatFindings(match, userIDByXuid, gamertagByXuid));
        findings.AddRange(AccuracyFindings(match, userIDByXuid, gamertagByXuid));
        if (match.ScoreLayout is CustomScoreLayout.Undecoded)
            findings.Add(new Finding(StatFlagRule.UndecodedCustomScore, null,
                "The match's custom game mode couldn't be decoded (#217): its players' scores are still Halo's packed numbers, not goals"));

        await ReconcileAsync(match.MatchID, null, findings, _time.GetUtcNow().UtcDateTime, MatchRules, ct, skipExistingQuery: isNewMatch);
    }

    /// <summary>
    /// A season match's games against its series result (StatFlagRule.SeriesDoesNotMatchGames): only when every one of
    /// its games is a recorded game (a manual game, or the historical import's), never a Halo pull. A Waypoint game's
    /// side isn't the series' home or away team directly (MatchTeam.TeamID is Halo's own team number): which one it was
    /// is worked out from its players' rosters (SeasonMatchService.WaypointWinners), already run - and relied on -
    /// every time such a game is reported, so skipping it here misses nothing a live report wouldn't already have
    /// caught. The finding, when there is one, is attached to the series' last recorded game, a flag always being of
    /// one game; otherwise any flag left on one of the series' games by an earlier run - no longer decided by games, a
    /// Waypoint game now in the mix, undone, reopened - is cleared rather than left stuck open for good. Called
    /// whenever a game is added, replaced or removed, the series is forfeited or undone, and whenever a Waypoint game
    /// joins it (SeasonMatchService.ReportMatch) even though that alone never raises the finding, since it may be what
    /// clears one an earlier, all-recorded state left.
    /// </summary>
    public Task CheckSeriesGamesAsync(int seasonMatchID, CancellationToken ct = default) =>
        SafelyAsync(() => CheckSeriesGamesCoreAsync(seasonMatchID, ct), $"season match {seasonMatchID}'s series");

    private async Task CheckSeriesGamesCoreAsync(int seasonMatchID, CancellationToken ct)
    {
        var seasonMatch = await _context.SeasonMatches
            .Include(m => m.SeriesGames).ThenInclude(g => g.RecordedGame!.Teams)
            .Include(m => m.SeriesGames).ThenInclude(g => g.Match)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.SeasonMatchID == seasonMatchID, ct);
        if (seasonMatch is null)
            return;

        var recordedGameIDs = seasonMatch.SeriesGames.Where(g => g.RecordedGameID is not null)
            .Select(g => g.RecordedGameID!.Value).ToList();
        if (recordedGameIDs.Count == 0)
            return; // nothing a flag could ever have been attached to

        var applies = seasonMatch.DecidedBy is SeasonMatchDecision.Games && seasonMatch.SeriesGames.All(g => g.Match is null);
        int? lastGameID = null;
        var findings = new List<Finding>();
        if (applies)
        {
            lastGameID = seasonMatch.SeriesGames.OrderByDescending(g => g.GameNumber).First().RecordedGameID!.Value;
            var home = seasonMatch.SeriesGames.Count(g => g.RecordedGame!.WonBy(seasonMatch.HomeTeamID));
            var away = seasonMatch.SeriesGames.Count(g => g.RecordedGame!.WonBy(seasonMatch.AwayTeamID));
            if (home != (seasonMatch.HomeTeamScore ?? 0) || away != (seasonMatch.AwayTeamScore ?? 0))
                findings.Add(new Finding(StatFlagRule.SeriesDoesNotMatchGames, null,
                    $"The series shows {seasonMatch.HomeTeamScore}-{seasonMatch.AwayTeamScore}, but its {seasonMatch.SeriesGames.Count} game(s) add up to {home}-{away}"));
        }

        var now = _time.GetUtcNow().UtcDateTime;
        // One query for every game of the series' existing flags, one save for them all: not once a game.
        var existingByGame = (await _context.StatFlags
                .Where(f => f.RecordedGameID != null && recordedGameIDs.Contains(f.RecordedGameID.Value))
                .Where(f => SeriesRules.Contains(f.Rule))
                .ToListAsync(ct))
            .GroupBy(f => f.RecordedGameID!.Value).ToDictionary(g => g.Key, g => g.ToList());

        // Every game of the series is reconciled, not only the one the finding (if any) attaches to: an earlier run
        // may have left this flag on a different game (the series' "last" one at the time), which would otherwise
        // never be looked at again once that's no longer the last, or no longer applies at all.
        var anyChange = false;
        foreach (var id in recordedGameIDs)
            anyChange |= ReconcileInMemory(null, id, id == lastGameID ? findings : [], existingByGame.GetValueOrDefault(id, []), now);
        if (anyChange)
            await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Clears any open SeriesDoesNotMatchGames flag on a game just taken out of its series for good - replaced, removed
    /// by hand, or unlinked by the import (a stale or scrim game) - whose RecordedGameID the caller captured before the
    /// unlink: once it's out, <see cref="CheckSeriesGamesAsync"/> can no longer reach it through the series.
    /// </summary>
    public Task ClearSeriesFlagAsync(int recordedGameID, CancellationToken ct = default) =>
        SafelyAsync(() => ReconcileAsync(null, recordedGameID, [], _time.GetUtcNow().UtcDateTime, SeriesRules, ct),
            $"clearing game {recordedGameID}'s series flag");

    #region Hand-entered rules (RecordedGame.IsManual only)

    private async Task<List<Finding>> HandEnteredFindingsAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct)
    {
        var findings = new List<Finding>();
        findings.AddRange(ScoreDoesNotMatchGoalsFindings(game));
        findings.AddRange(WinnerDoesNotMatchScoresFindings(game));
        findings.AddRange(TeamSmallerThanFormatFindings(game));
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

    /// <summary>A side fielded fewer players than the season's format (StatFlagThresholds.MinPlayersPerTeam): only
    /// checked when players were entered at all (a score-only game says nothing about who played).</summary>
    private static IEnumerable<Finding> TeamSmallerThanFormatFindings(RecordedGame game)
    {
        if (game.Participants.Count == 0)
            yield break;
        foreach (var team in game.Teams)
        {
            var count = game.Participants.Count(p => p.RecordedGameTeamID == team.RecordedGameTeamID);
            if (count < StatFlagThresholds.MinPlayersPerTeam)
                yield return new Finding(StatFlagRule.TeamSmallerThanFormat, null,
                    $"{team.Name ?? $"Side {team.Side}"} fielded {count} player(s); the season's format calls for at least {StatFlagThresholds.MinPlayersPerTeam}", Discriminator: team.Side);
        }
    }

    /// <summary>The game's date is before the season started, or in the future.</summary>
    private async Task<List<Finding>> DateOutsideSeasonFindingsAsync(RecordedGame game, SeasonMatch seasonMatch, CancellationToken ct)
    {
        if (game.PlayedAt is not DateTime playedAt)
            return [];
        var now = _time.GetUtcNow().UtcDateTime;
        if (playedAt <= now)
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
    /// already flags that same match on its own. Kills, deaths, assists and goals, as the rule's own doc says.</summary>
    private static IEnumerable<Finding> NegativeStatFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid,
        IReadOnlyDictionary<long, string> gamertagByXuid)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var bad = NegativeStats(("kills", p.Kills), ("deaths", p.Deaths), ("assists", p.Assists), ("goals", p.Goals ?? 0));
            if (bad is not null)
            {
                var playerID = userIDByXuid.GetValueOrDefault(p.XboxUserID) is int id and > 0 ? id : (int?)null;
                yield return new Finding(StatFlagRule.NegativeStat, playerID,
                    $"{Gamertag(p.XboxUserID, gamertagByXuid)}: {bad}", Discriminator: playerID is null ? p.XboxUserID : null);
            }
        }
    }

    /// <summary>Which named stats, of what's given, are negative - joined into one sentence - or null when none are.</summary>
    private static string? NegativeStats(params (string Name, int Value)[] stats)
    {
        var bad = stats.Where(s => s.Value < 0).Select(s => $"{s.Value} {s.Name}").ToList();
        return bad.Count == 0 ? null : string.Join(", ", bad) + " (can't be negative)";
    }

    /// <summary>Accuracy over 100%, or shots hit above shots fired. A player not (yet) linked to a league account has
    /// no player ID; its own XUID, rather than the player, then tells two such findings apart
    /// (<see cref="Finding.Discriminator"/>), since they'd otherwise all share a (rule, null) key.</summary>
    private static IEnumerable<Finding> AccuracyFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid,
        IReadOnlyDictionary<long, string> gamertagByXuid)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var playerID = userIDByXuid.GetValueOrDefault(p.XboxUserID) is int id and > 0 ? id : (int?)null;
            var discriminator = playerID is null ? p.XboxUserID : (long?)null;
            if (p.ShotsHit > p.ShotsFired)
                yield return new Finding(StatFlagRule.ImpossibleAccuracy, playerID,
                    $"{Gamertag(p.XboxUserID, gamertagByXuid)}: {p.ShotsHit} shots hit, above {p.ShotsFired} shots fired", discriminator);
            else if (p.Accuracy > StatFlagThresholds.MaxAccuracy)
                yield return new Finding(StatFlagRule.ImpossibleAccuracy, playerID,
                    $"{Gamertag(p.XboxUserID, gamertagByXuid)}: {p.Accuracy:0.#}% accuracy", discriminator);
        }
    }

    private static string Gamertag(long xboxUserID, IReadOnlyDictionary<long, string> gamertagByXuid) =>
        gamertagByXuid.GetValueOrDefault(xboxUserID, xboxUserID.ToString());

    #endregion

    /// <summary>
    /// Brings a game's flags up to date with what was just found, querying its existing ones first unless
    /// <paramref name="skipExistingQuery"/> (a match just inserted: it can't have any). See
    /// <see cref="ReconcileInMemory"/> for the rest.
    /// </summary>
    private async Task ReconcileAsync(Guid? matchID, int? recordedGameID, List<Finding> findings, DateTime now,
        HashSet<StatFlagRule> ownedRules, CancellationToken ct, bool skipExistingQuery = false)
    {
        var existing = skipExistingQuery
            ? []
            : await _context.StatFlags
                .Where(f => matchID != null ? f.MatchID == matchID : f.RecordedGameID == recordedGameID)
                .Where(f => ownedRules.Contains(f.Rule))
                .ToListAsync(ct);

        if (ReconcileInMemory(matchID, recordedGameID, findings, existing, now))
            await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// An open flag for a (rule, player, discriminator) still found is rewritten in place; one no longer found is
    /// removed (<see cref="StatFlag.Clear"/>, then); a new finding is added. A dismissed flag is left to
    /// <see cref="StatFlag.Rewrite"/>'s own rule (stays dismissed unless its reason changed). Nothing but
    /// <paramref name="existing"/> is read or touched, so a caller checking many games at once
    /// (<see cref="CheckRecordedGamesAsync"/>, the series check) can batch the query and the save around many calls of
    /// this. Returns whether anything changed, so an all-quiet batch saves nothing.
    /// </summary>
    private bool ReconcileInMemory(Guid? matchID, int? recordedGameID, List<Finding> findings, List<StatFlag> existing, DateTime now)
    {
        var changed = false;
        foreach (var finding in findings)
        {
            var flag = existing.FirstOrDefault(f => f.Rule == finding.Rule && f.PlayerUserID == finding.PlayerUserID && f.Discriminator == finding.Discriminator);
            if (flag is null)
                _context.StatFlags.Add(StatFlag.Found(finding.Rule, matchID, recordedGameID, finding.PlayerUserID, finding.Reason, now, finding.Discriminator));
            else
                flag.Rewrite(finding.Reason, now);
            // A new flag, or an existing one Rewrite may have changed (its wording, or reopened): either way, worth a save
            changed = true;
        }

        foreach (var stale in existing.Where(f => f.IsOpen && !findings.Any(fi => fi.Rule == f.Rule && fi.PlayerUserID == f.PlayerUserID && fi.Discriminator == f.Discriminator)))
        {
            stale.Clear();
            _context.StatFlags.Remove(stale);
            changed = true;
        }
        return changed;
    }

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
