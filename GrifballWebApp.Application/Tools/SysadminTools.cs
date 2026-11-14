using System.ComponentModel;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;

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

    public SysadminTools(ToolCaller caller, GamertagClaimService claims, IDataPullService dataPull)
    {
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
}
