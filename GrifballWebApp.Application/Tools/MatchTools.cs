using System.ComponentModel;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Application.Tools;

/// <summary>Matches: a season match's page and a recorded game's details, open to everyone on the site.</summary>
public class MatchTools
{
    private readonly ToolCaller _caller;
    private readonly SeasonMatchService _seasonMatches;
    private readonly MatchDetailsService _matchDetails;
    private readonly SeasonVisibility _visibility;

    public MatchTools(ToolCaller caller, SeasonMatchService seasonMatches, MatchDetailsService matchDetails, SeasonVisibility visibility)
    {
        _caller = caller;
        _visibility = visibility;
        _seasonMatches = seasonMatches;
        _matchDetails = matchDetails;
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

    [Description("A recorded Halo game's details: its time and length, the season match it counts for, each team's score and outcome, and each player's stats and medals.")]
    public async Task<MatchDetailsDto> GetGame([Description("The game's match ID, a GUID (a season match lists its games').")] Guid matchID,
        CancellationToken ct = default)
        => await _matchDetails.GetAsync(matchID, ct) ?? throw new ToolException($"There is no recorded game {matchID}.");
}
