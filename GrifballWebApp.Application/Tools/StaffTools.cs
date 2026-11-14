using System.ComponentModel;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// What staff's pages show, read only: the commissioner dashboard and the pending gamertag claims. Each tool for the roles
/// its page is for (<see cref="ToolRolesAttribute"/>); the MCP server lists them to those alone, and the Discord bot's chat, which
/// answers in a public channel, doesn't get them.
/// </summary>
public class StaffTools
{
    private readonly ToolCaller _caller;
    private readonly CommissionerDashboardService _dashboard;
    private readonly GamertagClaimService _claims;

    public StaffTools(ToolCaller caller, CommissionerDashboardService dashboard, GamertagClaimService claims)
    {
        _caller = caller;
        _dashboard = dashboard;
        _claims = claims;
    }

    [Description("The commissioner dashboard: the pending reschedule requests (each one's ID for decide_reschedule), the matches overdue to be "
        + "played with how many hours late, the seasons still to start that won't be public by the time their signups open, and the counts. "
        + "Commissioners and sysadmins.")]
    [ToolRoles(SiteRoles.Commissioner, SiteRoles.Sysadmin)]
    public async Task<CommissionerDashboardDto> GetCommissionerDashboard(CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return await _dashboard.GetAsync(ct);
    }

    [Description("The gamertag claims waiting for a decision: each one's ID (for decide_gamertag_claim), the gamertag, who claims it and the "
        + "season it was played in. Sysadmins.")]
    [ToolRoles(SiteRoles.Sysadmin)]
    public async Task<List<GamertagClaimDto>> ListPendingGamertagClaims(CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return await _claims.List(GamertagClaimStatus.Pending, ct);
    }
}
