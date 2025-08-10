using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Commissioner;

[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
public class CommissionerDashboardController : ControllerBase
{
    private readonly RescheduleService _rescheduleService;

    public CommissionerDashboardController(RescheduleService rescheduleService)
    {
        _rescheduleService = rescheduleService;
    }

    [HttpGet(Name = "GetDashboardData")]
    public async Task<IActionResult> GetDashboardData(CancellationToken ct)
    {
        var pendingReschedules = await _rescheduleService.GetPendingReschedulesAsync(ct);
        var overdueMatches = await _rescheduleService.GetOverdueMatchesAsync(ct);

        var dashboardData = new CommissionerDashboardDto
        {
            PendingReschedules = pendingReschedules,
            OverdueMatches = overdueMatches,
            Summary = new DashboardSummaryDto
            {
                PendingRescheduleCount = pendingReschedules.Count,
                OverdueMatchCount = overdueMatches.Count,
                CriticalOverdueCount = overdueMatches.Count(m => m.HoursOverdue > 72) // More than 3 days overdue
            }
        };

        return Ok(dashboardData);
    }

    [HttpGet(Name = "GetOverdueMatches")]
    public async Task<IActionResult> GetOverdueMatches(CancellationToken ct)
    {
        var overdueMatches = await _rescheduleService.GetOverdueMatchesAsync(ct);
        return Ok(overdueMatches);
    }
}