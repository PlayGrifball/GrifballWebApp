using System.ComponentModel;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Application.Tools;

/// <summary>Matches: a season match's page, the times suggested for it, and a recorded game's details, open to everyone on the site.</summary>
public class MatchTools
{
    private readonly ToolCaller _caller;
    private readonly SeasonMatchService _seasonMatches;
    private readonly MatchDetailsService _matchDetails;
    private readonly SeasonVisibility _visibility;
    private readonly SchedulingService _scheduling;

    public MatchTools(ToolCaller caller, SeasonMatchService seasonMatches, MatchDetailsService matchDetails, SeasonVisibility visibility, SchedulingService scheduling)
    {
        _caller = caller;
        _visibility = visibility;
        _seasonMatches = seasonMatches;
        _matchDetails = matchDetails;
        _scheduling = scheduling;
    }

    [Description("A season match: its teams, time, best-of, result and score, the games reported for it with each one's ID, "
        + "and whether you may request a reschedule.")]
    public async Task<SeasonMatchPageDto> GetSeasonMatch([Description("The season match's ID (schedules and team matches list them).")] int seasonMatchID,
        CancellationToken ct = default)
    {
        // One of a season not announced yet is no match to anyone but staff
        var page = await _visibility.CanSeeSeasonMatch(seasonMatchID, _caller.Viewer, ct) ? await _seasonMatches.GetSeasonMatchPage(seasonMatchID, _caller.UserID, ct) : null;
        return page ?? throw new ToolException($"There is no season match {seasonMatchID}.");
    }

    [Description("The best times (UTC) for a season match, from its season's weekly timeslots, best first: those with nothing wrong, "
        + "then the most of both teams' players able to play, then the soonest. Each says what is wrong with it (a warning: a team short "
        + "of players, too many matches that night or week) and how many of each team's players can play. A time either team already "
        + "plays, or outside the season, isn't suggested. For a reschedule request, or staff setting a time.")]
    public async Task<IReadOnlyList<TimeSuggestion>> SuggestMatchTimes([Description("The season match's ID.")] int seasonMatchID,
        [Description("From this time (UTC); left out, now.")] DateTime? from = null,
        [Description("Before this time (UTC); left out, the season's end.")] DateTime? to = null,
        [Description("How many, 1 to 10; left out, 5.")] int count = SchedulingService.DefaultSuggestions,
        CancellationToken ct = default)
    {
        var suggestions = await _visibility.CanSeeSeasonMatch(seasonMatchID, _caller.Viewer, ct)
            ? await _scheduling.SuggestAsync(seasonMatchID, from, to, Math.Clamp(count, 1, 10), ct)
            : null;
        return suggestions ?? throw new ToolException($"There is no season match {seasonMatchID} with both its teams.");
    }

    [Description("A recorded Halo game's details: its time and length, the season match it counts for, each team's score and outcome, and each player's stats and medals.")]
    public async Task<MatchDetailsDto> GetGame([Description("The game's match ID, a GUID (a season match lists its games').")] Guid matchID,
        CancellationToken ct = default)
        => await _matchDetails.GetAsync(matchID, ct) ?? throw new ToolException($"There is no recorded game {matchID}.");
}
