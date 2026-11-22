using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Application.StatFlags;

/// <summary>
/// Plain-rule anomaly checks for game stats (#548): runs <see cref="StatFlagRule"/>'s rules against a game just saved -
/// by hand (<see cref="CheckRecordedGameAsync"/>, for a manual game), by a Halo pull (<see cref="CheckMatchAsync"/>), or
/// by the historical import (<see cref="CheckRecordedGameAsync"/> again, called the same way) - and reconciles the
/// result onto the game's <see cref="StatFlag"/>s: an open flag no longer found is removed, one still found is brought
/// up to date in place (never doubled), and a new finding is added. A flag never changes the game it's about: it only
/// tells staff something is worth a look (<see cref="StaffStatFlagService"/> is how they see and dismiss them).
/// </summary>
public class StatFlagService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;

    public StatFlagService(IApplicationDbContext context, TimeProvider? time = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>What one rule found: <paramref name="PlayerUserID"/> is null when the rule is about the whole game (a
    /// team, the series), not one player. <paramref name="Discriminator"/> tells apart two findings of the same rule
    /// with no player (a side's number, an unlinked Halo player's XUID): without it they'd share a reconciliation key.</summary>
    private sealed record Finding(StatFlagRule Rule, int? PlayerUserID, string Reason, long? Discriminator = null);

    /// <summary>Every "any game" rule run on a recorded game.</summary>
    private static readonly IReadOnlySet<StatFlagRule> RecordedGameRules = new HashSet<StatFlagRule> { StatFlagRule.NegativeStat };

    /// <summary>Every hand-entered rule, run only for <see cref="RecordedGame.IsManual"/>.</summary>
    private static readonly IReadOnlySet<StatFlagRule> HandEnteredRules = new HashSet<StatFlagRule>
    {
        StatFlagRule.ScoreDoesNotMatchGoals, StatFlagRule.WinnerDoesNotMatchScores,
        StatFlagRule.TeamSmallerThanFormat, StatFlagRule.DateOutsideSeason,
    };

    /// <summary>Every rule run on a Halo Infinite match.</summary>
    private static readonly IReadOnlySet<StatFlagRule> MatchRules = new HashSet<StatFlagRule>
        { StatFlagRule.NegativeStat, StatFlagRule.ImpossibleAccuracy, StatFlagRule.UndecodedCustomScore };

    /// <summary>The one rule a season's series is checked by.</summary>
    private static readonly IReadOnlySet<StatFlagRule> SeriesRules = new HashSet<StatFlagRule> { StatFlagRule.SeriesDoesNotMatchGames };

    /// <summary>
    /// Runs every rule that applies to <paramref name="game"/> (the "any game" rules always; the hand-entered ones only
    /// for a game entered by hand, <see cref="RecordedGame.IsManual"/>) and reconciles the result onto its flags, never
    /// touching a flag of a rule this game isn't checked by (<see cref="ReconcileAsync"/>'s <c>ownedRules</c>) - a
    /// StatFlagRule.SeriesDoesNotMatchGames flag the series check left on this same game, say. Called after the game
    /// itself is saved (its RecordedGameID is a real one): by manual match entry, right after <c>AddManualGame</c> or
    /// <c>EditManualGame</c> save, with the season match already loaded there (<paramref name="seasonMatch"/>); by the
    /// historical import, with no season match (a game it imports always counts through its series, never
    /// <see cref="RecordedGame.IsManual"/>, so the hand-entered rules never run there).
    /// </summary>
    public async Task CheckRecordedGameAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct = default)
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
    /// Runs the "any game" rules that apply to a Halo Infinite match (accuracy and shots, negative stats, an undecoded
    /// custom score) and reconciles the result onto its flags. Called from the one place every Halo pull saves a match
    /// (DataPullService.SaveMatch), once the match itself is saved, so a match is checked once whether it ends up
    /// reported to a season match or not. <paramref name="match"/> is already loaded with its teams and participants
    /// (it was just saved); <paramref name="userIDByXuid"/> and <paramref name="gamertagByXuid"/>, when the caller
    /// already has them (a bulk pull, looking them up once for every match rather than once a match), are used as
    /// given - otherwise looked up here, for a match checked on its own.
    /// </summary>
    public async Task CheckMatchAsync(Match match, IReadOnlyDictionary<long, int>? userIDByXuid = null,
        IReadOnlyDictionary<long, string>? gamertagByXuid = null, CancellationToken ct = default)
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

        await ReconcileAsync(match.MatchID, null, findings, _time.GetUtcNow().UtcDateTime, MatchRules, ct);
    }

    /// <summary>
    /// A season match's games against its series result (StatFlagRule.SeriesDoesNotMatchGames): only when every one of
    /// its games is a recorded game (a manual game, or the historical import's), never a Halo pull. A Waypoint game's
    /// side isn't the series' home or away team directly (MatchTeam.TeamID is Halo's own team number): which one it was
    /// is worked out from its players' rosters (SeasonMatchService.WaypointWinners), already run - and relied on -
    /// every time such a game is reported, so skipping it here misses nothing a live report wouldn't already have
    /// caught. The finding, when there is one, is attached to the series' last recorded game, a flag always being of
    /// one game; otherwise any flag left on one of the series' games by an earlier run - no longer decided by games, a
    /// Waypoint game now in the mix, undone, reopened - is cleared rather than left stuck open for good.
    /// </summary>
    public async Task CheckSeriesGamesAsync(int seasonMatchID, CancellationToken ct = default)
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
        // Every game of the series is reconciled, not only the one the finding (if any) attaches to: an earlier run
        // may have left this flag on a different game (the series' "last" one at the time), which would otherwise
        // never be looked at again once that's no longer the last, or no longer applies at all.
        foreach (var id in recordedGameIDs)
            await ReconcileAsync(null, id, id == lastGameID ? findings : [], now, SeriesRules, ct);
    }

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

    private static IEnumerable<Finding> NegativeStatFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid,
        IReadOnlyDictionary<long, string> gamertagByXuid)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var bad = NegativeStats(("kills", p.Kills), ("deaths", p.Deaths), ("assists", p.Assists), ("score", p.Score), ("goals", p.Goals ?? 0));
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

    /// <summary>Accuracy over 100%, or shots hit above shots fired. <paramref name="playerID"/> is null for a player not
    /// (yet) linked to a league account: its own XUID, rather than the player, then tells two such findings apart
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
    /// Brings a game's flags up to date with what was just found: an open flag for a (rule, player, discriminator)
    /// still found is rewritten in place; one no longer found is removed; a new finding is added. A dismissed flag is
    /// left to <see cref="StatFlag.Rewrite"/>'s own rule (stays dismissed unless its reason changed). Only ever touches
    /// flags of a rule in <paramref name="ownedRules"/> - the rules the caller actually ran - so one check never
    /// removes a flag another check left on the same game (a series check's game is often also a hand-entered game's).
    /// </summary>
    private async Task ReconcileAsync(Guid? matchID, int? recordedGameID, List<Finding> findings, DateTime now,
        IReadOnlySet<StatFlagRule> ownedRules, CancellationToken ct)
    {
        var existing = await _context.StatFlags
            .Where(f => matchID != null ? f.MatchID == matchID : f.RecordedGameID == recordedGameID)
            .Where(f => ownedRules.Contains(f.Rule))
            .ToListAsync(ct);

        foreach (var finding in findings)
        {
            var flag = existing.FirstOrDefault(f => f.Rule == finding.Rule && f.PlayerUserID == finding.PlayerUserID && f.Discriminator == finding.Discriminator);
            if (flag is null)
                _context.StatFlags.Add(StatFlag.Found(finding.Rule, matchID, recordedGameID, finding.PlayerUserID, finding.Reason, now, finding.Discriminator));
            else
                flag.Rewrite(finding.Reason, now);
        }

        foreach (var stale in existing.Where(f => f.IsOpen && !findings.Any(fi => fi.Rule == f.Rule && fi.PlayerUserID == f.PlayerUserID && fi.Discriminator == f.Discriminator)))
            _context.StatFlags.Remove(stale);

        if (findings.Count > 0 || existing.Count > 0)
            await _context.SaveChangesAsync(ct);
    }
}
