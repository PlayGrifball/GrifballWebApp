using System.ComponentModel;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Sysadmins' actions, as their pages and /admin do them, on the same services: deciding gamertag claims (merges go through the
/// claim's process, never by hand), pulling a Halo game and refreshing the medals. A service's refusal is the answer.
/// </summary>
[ToolRoles(SiteRoles.Sysadmin)]
public class SysadminTools
{
    private readonly ToolCaller _caller;
    private readonly GamertagClaimService _claims;
    private readonly IDataPullService _dataPull;
    private readonly IJobControl _jobs;

    public SysadminTools(ToolCaller caller, GamertagClaimService claims, IDataPullService dataPull, IJobControl jobs)
    {
        _jobs = jobs;
        _caller = caller;
        _claims = claims;
        _dataPull = dataPull;
    }

    [Description("Approves or rejects a gamertag claim (list_pending_gamertag_claims lists them), as the claims page does: approved, the claimed "
        + "account's history becomes the claimant's. Rejecting can give a reason. Sysadmins.")]
    public async Task<string> DecideGamertagClaim(
        [Description("The claim's ID.")] int claimID,
        [Description("True to approve, false to reject.")] bool approve,
        [Description("Why it's rejected, optional.")] string? reason = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        if (approve)
            await _claims.Approve(claimID, ct);
        else
            await _claims.Reject(claimID, reason, ct);
        return approve ? "Approved." : "Rejected.";
    }

    [Description("Pulls one Halo game's stats from Halo and saves them, as the admin page does. Sysadmins.")]
    public async Task<string> PullHaloGame([Description("The game's match ID, a GUID.")] Guid matchID)
    {
        _caller.RequireRolesOf();
        if (matchID == Guid.Empty)
            throw new ToolException("That isn't a game's match ID.");
        await _dataPull.GetAndSaveMatch(matchID);
        return "Pulled: get_game shows it.";
    }

    [Description("Downloads Halo's medals again, as the admin page does. Sysadmins.")]
    public async Task<string> RefreshMedals()
    {
        _caller.RequireRolesOf();
        await _dataPull.DownloadMedals();
        return "The medals are refreshed.";
    }

    [Description("Runs a background job now, as the jobs page does (list_jobs gives each one's group and name). Refused for one that's off, "
        + "running, or needs its page's input. Sysadmins.")]
    public async Task<string> RunJob(
        [Description("The job's group.")] string group,
        [Description("The job's name.")] string name,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return Done(await _jobs.RunNow(group, name, _caller.UserID, ct), "It's running.");
    }

    [Description("Pauses or resumes a background job's schedule, as the jobs page does (list_jobs gives each one's group and name). Sysadmins.")]
    public async Task<string> SetJobPaused(
        [Description("The job's group.")] string group,
        [Description("The job's name.")] string name,
        [Description("True to pause, false to resume.")] bool paused,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return Done(await _jobs.SetPaused(group, name, paused, _caller.Name, ct), paused ? "It's paused." : "It's resumed.");
    }

    /// <summary>The answer for a job's change: <paramref name="done"/>, else the refusal saying why not.</summary>
    private static string Done(JobControlResult result, string done) => result switch
    {
        JobControlResult.Done => done,
        JobControlResult.NotFound => throw new ToolException("There is no such job: list_jobs gives each one's group and name."),
        _ => throw new ToolException("The job can't do that now: it's off, running, or needs its page's input."),
    };
}
