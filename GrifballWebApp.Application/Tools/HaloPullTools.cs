using System.ComponentModel;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Pulls from Halo, as the season match page's buttons start them: queued as background jobs, one per season match at a time.
/// For the roles the page lets pull.
/// </summary>
[ToolRoles(Player, Commissioner, Sysadmin)]
public class HaloPullTools
{
    public const string Player = "Player";
    public const string Commissioner = "Commissioner";
    public const string Sysadmin = "Sysadmin";
    private static readonly string[] Pullers = [Player, Commissioner, Sysadmin];

    private readonly ToolCaller _caller;
    private readonly SeasonMatchService _seasonMatches;
    private readonly ISeasonMatchPulls _pulls;

    public HaloPullTools(ToolCaller caller, SeasonMatchService seasonMatches, ISeasonMatchPulls pulls)
    {
        _caller = caller;
        _seasonMatches = seasonMatches;
        _pulls = pulls;
    }

    [Description("Queues a pull of a season match's players' recent Grifball games from Halo, as the match page's button does, so the games "
        + "can be reported (and get_recent_games shows them). It runs in the background, one pull per match at a time: the answer says whether "
        + "it is queued or running, and its last run. Players, commissioners and sysadmins may.")]
    public async Task<OnDemandJobStatusDto> PullSeasonMatchGames(
        [Description("The season match's ID (schedules and team matches list them).")] int seasonMatchID,
        [Description("Back through each player's history to the season's start, for an old match whose games are past their recent ones.")] bool backToSeasonStart = false,
        CancellationToken ct = default)
    {
        _caller.Require(Pullers);
        if (!await _seasonMatches.Exists(seasonMatchID, ct))
            throw new ToolException($"There is no season match {seasonMatchID}.");
        return await _pulls.Queue(seasonMatchID, _caller.UserID, backToSeasonStart, ct);
    }
}
