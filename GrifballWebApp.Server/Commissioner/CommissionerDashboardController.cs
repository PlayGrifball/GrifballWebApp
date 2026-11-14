using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Server.Commissioner;

[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class CommissionerDashboardController : ControllerBase
{
    private readonly CommissionerDashboardService _dashboard;
    private readonly RescheduleService _rescheduleService;

    public CommissionerDashboardController(CommissionerDashboardService dashboard, RescheduleService rescheduleService)
    {
        _dashboard = dashboard;
        _rescheduleService = rescheduleService;
    }

    /// <summary>
    /// The commissioner dashboard: pending reschedules, overdue matches, seasons that won't be public by their signups, and
    /// their counts. Commissioners and sysadmins only.
    /// </summary>
    [HttpGet("commissioner-dashboard", Name = "GetDashboardData")]
    [ProducesResponseType<CommissionerDashboardDto>(StatusCodes.Status200OK)]
    public Task<CommissionerDashboardDto> GetDashboardData(CancellationToken ct) => _dashboard.GetAsync(ct);

    /// <summary>Matches overdue for rescheduling. Commissioners and sysadmins only.</summary>
    [HttpGet("season-matches/overdue", Name = "GetOverdueMatches")]
    [ProducesResponseType<List<OverdueMatchDto>>(StatusCodes.Status200OK)]
    public Task<List<OverdueMatchDto>> GetOverdueMatches(CancellationToken ct)
    {
        return _rescheduleService.GetOverdueMatchesAsync(ct);
    }
}
