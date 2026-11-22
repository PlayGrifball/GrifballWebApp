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

    /// <summary>What one rule found: <paramref name="playerUserID"/> is null when the rule is about the whole game (a
    /// team, the series), not one player.</summary>
    private sealed record Finding(StatFlagRule Rule, int? PlayerUserID, string Reason);

    /// <summary>
    /// Runs every rule that applies to <paramref name="game"/> (the "any game" rules always; the hand-entered ones only
    /// for a game entered by hand, <see cref="RecordedGame.IsManual"/>) and reconciles the result onto its flags. Called
    /// after the game itself is saved (its RecordedGameID is a real one): by manual match entry, right after
    /// <c>AddManualGame</c> or <c>EditManualGame</c> save, with the season match already loaded there
    /// (<paramref name="seasonMatch"/>); by the historical import, with no season match (a game it imports always
    /// counts through its series, never <see cref="RecordedGame.IsManual"/>, so the hand-entered rules never run there).
    /// </summary>
    public async Task CheckRecordedGameAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var findings = new List<Finding>();
        findings.AddRange(NegativeStatFindings(game));
        if (game.IsManual)
            findings.AddRange(await HandEnteredFindingsAsync(game, seasonMatch, ct));
        await ReconcileAsync(null, game.RecordedGameID, findings, now, ct);
    }

    /// <summary>
    /// Runs the "any game" rules that apply to a Halo Infinite match (accuracy and shots, negative stats, an undecoded
    /// custom score) and reconciles the result onto its flags. Called from the one place every Halo pull saves a match
    /// (DataPullService.SaveMatch), so a match is checked once whether it ends up reported to a season match or not.
    /// </summary>
    public async Task CheckMatchAsync(Guid matchID, CancellationToken ct = default)
    {
        var match = await _context.Matches
            .Include(m => m.MatchTeams).ThenInclude(t => t.MatchParticipants).ThenInclude(p => p.XboxUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.MatchID == matchID, ct);
        if (match is null)
            return;

        var xuids = match.MatchTeams.SelectMany(t => t.MatchParticipants).Select(p => p.XboxUserID).Distinct().ToList();
        var userIDByXuid = await _context.Users.Where(u => u.XboxUserID != null && xuids.Contains(u.XboxUserID!.Value))
            .Select(u => new { XboxUserID = u.XboxUserID!.Value, u.Id })
            .ToDictionaryAsync(u => u.XboxUserID, u => u.Id, ct);

        var findings = new List<Finding>();
        findings.AddRange(NegativeStatFindings(match, userIDByXuid));
        findings.AddRange(AccuracyFindings(match, userIDByXuid));
        if (match.ScoreLayout is CustomScoreLayout.Undecoded)
            findings.Add(new Finding(StatFlagRule.UndecodedCustomScore, null,
                "The match's custom game mode couldn't be decoded (#217): its players' scores are still Halo's packed numbers, not goals"));

        await ReconcileAsync(matchID, null, findings, _time.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>
    /// A season match's games against its series result (StatFlagRule.SeriesDoesNotMatchGames): only when every one of
    /// its games is a recorded game (a manual game, or the historical import's), never a Halo pull. A Waypoint game's
    /// side isn't the series' home or away team directly (MatchTeam.TeamID is Halo's own team number): which one it was
    /// is worked out from its players' rosters (SeasonMatchService.WaypointWinners), already run - and relied on -
    /// every time such a game is reported, so skipping it here misses nothing a live report wouldn't already have
    /// caught. Attaches the finding to the series' last game, a flag always being of one game; a game added later
    /// without this being run again keeps whatever an earlier run attached to an earlier game (left for later).
    /// </summary>
    public async Task CheckSeriesGamesAsync(int seasonMatchID, CancellationToken ct = default)
    {
        var seasonMatch = await _context.SeasonMatches
            .Include(m => m.SeriesGames).ThenInclude(g => g.RecordedGame!.Teams)
            .Include(m => m.SeriesGames).ThenInclude(g => g.Match)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.SeasonMatchID == seasonMatchID, ct);
        if (seasonMatch is null || seasonMatch.DecidedBy is not SeasonMatchDecision.Games || seasonMatch.SeriesGames.Count == 0)
            return;
        if (seasonMatch.SeriesGames.Any(g => g.Match is not null))
            return;

        var home = seasonMatch.SeriesGames.Count(g => g.RecordedGame!.WonBy(seasonMatch.HomeTeamID));
        var away = seasonMatch.SeriesGames.Count(g => g.RecordedGame!.WonBy(seasonMatch.AwayTeamID));

        var findings = new List<Finding>();
        if (home != (seasonMatch.HomeTeamScore ?? 0) || away != (seasonMatch.AwayTeamScore ?? 0))
            findings.Add(new Finding(StatFlagRule.SeriesDoesNotMatchGames, null,
                $"The series shows {seasonMatch.HomeTeamScore}-{seasonMatch.AwayTeamScore}, but its {seasonMatch.SeriesGames.Count} game(s) add up to {home}-{away}"));

        var lastGame = seasonMatch.SeriesGames.OrderByDescending(g => g.GameNumber).First();
        await ReconcileAsync(null, lastGame.RecordedGameID, findings, _time.GetUtcNow().UtcDateTime, ct);
    }

    #region Hand-entered rules (RecordedGame.IsManual only)

    private async Task<List<Finding>> HandEnteredFindingsAsync(RecordedGame game, SeasonMatch? seasonMatch, CancellationToken ct)
    {
        var findings = new List<Finding>();
        findings.AddRange(ScoreDoesNotMatchGoalsFindings(game));
        findings.AddRange(WinnerDoesNotMatchScoresFindings(game));
        findings.AddRange(PlayerOnBothTeamsOrTwiceFindings(game));
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
                    $"{team.Name ?? $"Side {team.Side}"}'s score is {team.Score ?? 0}, but its players' goals add up to {goals}");
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

    /// <summary>A player listed more than once in the game. RecordedGame.SetPlayers keeps one line a user, so this
    /// should never fire either; kept for the same reason as the winner check above.</summary>
    private static IEnumerable<Finding> PlayerOnBothTeamsOrTwiceFindings(RecordedGame game)
        => game.Participants.GroupBy(p => p.UserID).Where(g => g.Count() > 1)
            .Select(g => new Finding(StatFlagRule.PlayerOnBothTeamsOrTwice, g.Key, $"{g.First().Gamertag} has more than one line in this game"));

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
                    $"{team.Name ?? $"Side {team.Side}"} fielded {count} player(s); the season's format calls for at least {StatFlagThresholds.MinPlayersPerTeam}");
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

    private static IEnumerable<Finding> NegativeStatFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var bad = NegativeStats(("kills", p.Kills), ("deaths", p.Deaths), ("assists", p.Assists), ("score", p.Score), ("goals", p.Goals ?? 0));
            if (bad is not null)
                yield return new Finding(StatFlagRule.NegativeStat, userIDByXuid.GetValueOrDefault(p.XboxUserID) is int id and > 0 ? id : null,
                    $"{p.XboxUser.Gamertag}: {bad}");
        }
    }

    /// <summary>Which named stats, of what's given, are negative - joined into one sentence - or null when none are.</summary>
    private static string? NegativeStats(params (string Name, int Value)[] stats)
    {
        var bad = stats.Where(s => s.Value < 0).Select(s => $"{s.Value} {s.Name}").ToList();
        return bad.Count == 0 ? null : string.Join(", ", bad) + " (can't be negative)";
    }

    /// <summary>Accuracy over 100%, or shots hit above shots fired.</summary>
    private static IEnumerable<Finding> AccuracyFindings(Match match, IReadOnlyDictionary<long, int> userIDByXuid)
    {
        foreach (var p in match.MatchTeams.SelectMany(t => t.MatchParticipants))
        {
            var playerID = userIDByXuid.GetValueOrDefault(p.XboxUserID) is int id and > 0 ? id : (int?)null;
            if (p.ShotsHit > p.ShotsFired)
                yield return new Finding(StatFlagRule.ImpossibleAccuracy, playerID,
                    $"{p.XboxUser.Gamertag}: {p.ShotsHit} shots hit, above {p.ShotsFired} shots fired");
            else if (p.Accuracy > StatFlagThresholds.MaxAccuracy)
                yield return new Finding(StatFlagRule.ImpossibleAccuracy, playerID, $"{p.XboxUser.Gamertag}: {p.Accuracy:0.#}% accuracy");
        }
    }

    #endregion

    /// <summary>
    /// Brings a game's flags up to date with what was just found: an open flag for a (rule, player) still found is
    /// rewritten in place; one no longer found is removed; a new finding is added. A dismissed flag is left to
    /// <see cref="StatFlag.Rewrite"/>'s own rule (stays dismissed unless its reason changed).
    /// </summary>
    private async Task ReconcileAsync(Guid? matchID, int? recordedGameID, List<Finding> findings, DateTime now, CancellationToken ct)
    {
        var existing = await _context.StatFlags
            .Where(f => matchID != null ? f.MatchID == matchID : f.RecordedGameID == recordedGameID)
            .ToListAsync(ct);

        foreach (var finding in findings)
        {
            var flag = existing.FirstOrDefault(f => f.Rule == finding.Rule && f.PlayerUserID == finding.PlayerUserID);
            if (flag is null)
                _context.StatFlags.Add(StatFlag.Found(finding.Rule, matchID, recordedGameID, finding.PlayerUserID, finding.Reason, now));
            else
                flag.Rewrite(finding.Reason, now);
        }

        foreach (var stale in existing.Where(f => f.IsOpen && !findings.Any(fi => fi.Rule == f.Rule && fi.PlayerUserID == f.PlayerUserID)))
            _context.StatFlags.Remove(stale);

        if (findings.Count > 0 || existing.Count > 0)
            await _context.SaveChangesAsync(ct);
    }
}
