using System.ComponentModel;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Teams;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Commissioners' actions, as the dashboard, the season match page and the season's setup pages do them, on the same services
/// as those pages and /admin: deciding reschedules, reporting and undoing results, scheduling, generating matches, making and
/// seeding the bracket, locking the draft's captains. A service's refusal (a match already reported, a season already played)
/// is the answer, as the site's 400 is. A season left out is the one its purpose means for staff, who see one not announced yet.
/// </summary>
public class CommissionerTools
{
    private readonly ToolCaller _caller;
    private readonly SeasonService _seasons;
    private readonly SeasonVisibility _visibility;
    private readonly RescheduleService _reschedules;
    private readonly SeasonMatchService _seasonMatches;
    private readonly MatchPlannerService _matchPlanner;
    private readonly IBracketService _brackets;
    private readonly TeamService _teams;

    public CommissionerTools(ToolCaller caller, SeasonService seasons, SeasonVisibility visibility, RescheduleService reschedules,
        SeasonMatchService seasonMatches, MatchPlannerService matchPlanner, IBracketService brackets, TeamService teams)
    {
        _caller = caller;
        _seasons = seasons;
        _visibility = visibility;
        _reschedules = reschedules;
        _seasonMatches = seasonMatches;
        _matchPlanner = matchPlanner;
        _brackets = brackets;
        _teams = teams;
    }

    /// <summary>The season match, if it's there (and the caller may see it); a <see cref="ToolException"/> else.</summary>
    private async Task<int> SeasonMatch(int seasonMatchID, CancellationToken ct)
        => await _visibility.CanSeeSeasonMatch(seasonMatchID, _caller.Viewer, ct) ? seasonMatchID : throw new ToolException($"There is no season match {seasonMatchID}.");

    /// <summary>The season asked for, if it's there, else the one <paramref name="purpose"/> means; a <see cref="ToolException"/> when there is none.</summary>
    private async Task<int> Season(int? seasonID, SeasonPurpose purpose, CancellationToken ct)
    {
        if (seasonID is int id)
            return await _visibility.CanSeeSeason(id, _caller.Viewer, ct) ? id : throw new ToolException($"There is no season {id}: list_seasons has their IDs.");
        var season = await _seasons.GetDefaultSeasonID(purpose, _caller.Viewer, ct);
        return season != 0 ? season : throw new ToolException("There is no season to do that to: name one (list_seasons has their IDs).");
    }

    [Description("Approves or rejects a reschedule request (get_commissioner_dashboard lists the pending ones), as the dashboard does: approved, "
        + "the match moves to the requested time. The notes are the commissioner's, shown with the decision. Commissioners and sysadmins.")]
    [ToolRoles(SiteRoles.Commissioner, SiteRoles.Sysadmin)]
    public async Task<string> DecideReschedule(
        [Description("The reschedule request's ID.")] int rescheduleID,
        [Description("True to approve, false to reject.")] bool approve,
        [Description("Notes for the players, optional.")] string? notes = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        try
        {
            await _reschedules.ProcessRescheduleAsync(rescheduleID, new ProcessRescheduleDto { Approved = approve, CommissionerNotes = notes }, _caller.Me(), ct);
        }
        catch (ArgumentException e)
        {
            // No such request: the service's own word for it, as /admin replies
            throw new ToolException(e.Message);
        }
        return approve ? "Approved." : "Rejected.";
    }

    [Description("Reports a recorded Halo game as one of a season match's games, as the match page does (get_season_match lists the possible "
        + "games). Replacing one of its games: that game's series game ID. Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> ReportSeasonMatchGame(
        [Description("The season match's ID.")] int seasonMatchID,
        [Description("The Halo game's match ID, a GUID.")] Guid matchID,
        [Description("The series game it replaces, if any.")] int? replacesSeriesGameID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await _seasonMatches.ReportMatch(await SeasonMatch(seasonMatchID, ct), matchID, replacesSeriesGameID, ct);
        return "Reported.";
    }

    [Description("Forfeits a season match, as the match page does: home, away or both forfeit. Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> ForfeitSeasonMatch(
        [Description("The season match's ID.")] int seasonMatchID,
        [Description("Who forfeits: home, away or both.")] string side,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        var id = await SeasonMatch(seasonMatchID, ct);
        await (side.Trim().ToLowerInvariant() switch
        {
            "home" => _seasonMatches.HomeForfeit(id, ct),
            "away" => _seasonMatches.AwayForfeit(id, ct),
            "both" => _seasonMatches.DoubleForfeit(id, ct),
            _ => throw new ToolException("Say who forfeits: home, away or both."),
        });
        return $"Forfeited ({side.Trim().ToLowerInvariant()}).";
    }

    [Description("Undoes a season match's result (its reported games or forfeit), as the match page does. Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> UndoSeasonMatchResult([Description("The season match's ID.")] int seasonMatchID, CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await _seasonMatches.UndoResult(await SeasonMatch(seasonMatchID, ct), ct);
        return "The result is undone.";
    }

    [Description("Schedules a season match at a time, in UTC, as the schedule page does. Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> SetSeasonMatchTime(
        [Description("The season match's ID.")] int seasonMatchID,
        [Description("When it's played, in UTC (2026-11-20T20:00:00Z).")] DateTime time,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        var utc = time.Kind is DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
        await _matchPlanner.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = await SeasonMatch(seasonMatchID, ct), Time = utc }, ct);
        return $"The match is set for {utc:yyyy-MM-dd HH:mm} UTC.";
    }

    [Description("Makes a season's matches from its teams (each team's home matches against the others), as the season setup does. Refused "
        + "once any match of it is played: it replaces the matches. Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> GenerateSeasonMatches(
        [Description("Home matches per team, at least 1.")] int homeMatchesPerTeam,
        [Description("Games per match (best of), at least 1.")] int bestOf,
        [Description("The season's ID; left out, the running season, else the next to start.")] int? seasonID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        if (homeMatchesPerTeam <= 0 || bestOf <= 0)
            throw new ToolException("Home matches per team and best of must be at least 1.");
        await _matchPlanner.CreateSeasonMatches(await Season(seasonID, SeasonPurpose.Active, ct), homeMatchesPerTeam, bestOf, ct);
        return "The matches are made.";
    }

    [Description("Makes a season's playoff bracket, as the bracket setup does: single or double elimination. Refused once any of it is played. "
        + "Seed it next (seed_season_bracket). Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> CreateSeasonBracket(
        [Description("How many teams play in it.")] int participants,
        [Description("Double elimination (a losers' bracket) rather than single.")] bool doubleElimination,
        [Description("Games per match (best of).")] int bestOf,
        [Description("The season's ID; left out, the running season, else the next to start.")] int? seasonID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        if (participants <= 0)
            throw new ToolException("A bracket needs participants.");
        await _brackets.CreateBracket(participants, await Season(seasonID, SeasonPurpose.Active, ct), doubleElimination, bestOf, ct);
        return "The bracket is made.";
    }

    [Description("Seeds a season's bracket by its standings, as the bracket page does. Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> SeedSeasonBracket(
        [Description("The season's ID; left out, the running season, else the one that ended last.")] int? seasonID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await _brackets.SetSeeds(await Season(seasonID, SeasonPurpose.Current, ct), ct: ct);
        return "The bracket is seeded by the standings.";
    }

    [Description("Locks or unlocks a season's draft captains, as the draft page does: locked, they can't be changed and the draft can run. "
        + "Commissioners.")]
    [ToolRoles(SiteRoles.Commissioner)]
    public async Task<string> SetCaptainsLocked(
        [Description("True to lock, false to unlock.")] bool locked,
        [Description("The season's ID; left out, the next to start, else the running one.")] int? seasonID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await _teams.LockCaptains(await Season(seasonID, SeasonPurpose.Upcoming, ct), locked, ct: ct);
        return locked ? "The captains are locked." : "The captains are unlocked.";
    }
}
