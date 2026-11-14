using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /match pull and /player pull: the agents' pull tools (<see cref="HaloPullTools"/>), run as the caller, and the reply they
/// share. The commands check the caller's roles first (<see cref="DiscordCaller.Roles.Pullers"/>), as the tools do themselves.
/// </summary>
public static class PullReplies
{
    /// <summary>Where a queued pull is, in a sentence.</summary>
    public static string State(OnDemandJobStatusDto status) => status.State switch
    {
        OnDemandJobState.Queued => "is queued: the worker starts it within a few seconds",
        OnDemandJobState.Running => "is running",
        _ => "has finished",
    };

    /// <summary>The agents' caller for this Discord caller: their account and roles.</summary>
    public static ToolCaller AsToolCaller(CallerUser caller) => new(caller.UserID, caller.Roles);
}

/// <summary>The agents' pull tools, run as a Discord caller: what /match pull and /player pull call.</summary>
public sealed class PullToolsFor
{
    private readonly SeasonMatchService _seasonMatches;
    private readonly ISeasonMatchPulls _pulls;
    private readonly XboxAccountService _accounts;
    private readonly IXboxAccountPulls _accountPulls;

    public PullToolsFor(SeasonMatchService seasonMatches, ISeasonMatchPulls pulls, XboxAccountService accounts, IXboxAccountPulls accountPulls)
    {
        _seasonMatches = seasonMatches;
        _pulls = pulls;
        _accounts = accounts;
        _accountPulls = accountPulls;
    }

    public HaloPullTools As(CallerUser caller) => new(PullReplies.AsToolCaller(caller), _seasonMatches, _pulls, _accounts, _accountPulls);
}
