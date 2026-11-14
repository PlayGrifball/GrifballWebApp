using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /match pull and /player pull: the agents' pull tools (<see cref="HaloPullTools"/>), run as the caller, and the reply they
/// share. The commands check the caller's roles first, with the tools' own (<see cref="GrifTools.RolesByName"/>), as the
/// tools do themselves.
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

/// <summary>
/// The agents' pull tools, run as a Discord caller: what /match pull and /player pull call. Made by the factory the MCP server
/// and the chats use (<see cref="GrifTools.Factory"/>), from the command's services.
/// </summary>
public sealed class PullToolsFor
{
    private static readonly Func<IServiceProvider, ToolCaller, object> Create = GrifTools.Factory(typeof(HaloPullTools));

    private readonly IServiceProvider _services;

    public PullToolsFor(IServiceProvider services)
    {
        _services = services;
    }

    public HaloPullTools As(CallerUser caller) => (HaloPullTools)Create(_services, PullReplies.AsToolCaller(caller));
}
