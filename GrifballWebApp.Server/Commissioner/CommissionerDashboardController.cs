using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Server.Commissioner;

[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class CommissionerDashboardController : ControllerBase
{
    private readonly RescheduleService _rescheduleService;
    private readonly EventOrganizerService _seasons;

    public CommissionerDashboardController(RescheduleService rescheduleService, EventOrganizerService seasons)
    {
        _rescheduleService = rescheduleService;
        _seasons = seasons;
    }

    /// <summary>
    /// The commissioner dashboard: pending reschedules, overdue matches, seasons that won't be public by their signups, and
    /// their counts. Commissioners and sysadmins only.
    /// </summary>
    [HttpGet("commissioner-dashboard", Name = "GetDashboardData")]
    [ProducesResponseType<CommissionerDashboardDto>(StatusCodes.Status200OK)]
    public async Task<CommissionerDashboardDto> GetDashboardData(CancellationToken ct)
    {
        var pendingReschedules = await _rescheduleService.GetPendingReschedulesAsync(ct);
        var overdueMatches = await _rescheduleService.GetOverdueMatchesAsync(ct);
        var notPublic = await _seasons.GetSeasonsNotPublicBySignups(ct);

        var dashboardData = new CommissionerDashboardDto
        {
            PendingReschedules = pendingReschedules,
            OverdueMatches = overdueMatches,
            SeasonsNotPublicBySignups = notPublic,
            Summary = new DashboardSummaryDto
            {
                PendingRescheduleCount = pendingReschedules.Count,
                OverdueMatchCount = overdueMatches.Count,
                CriticalOverdueCount = overdueMatches.Count(m => m.HoursOverdue > 72), // More than 3 days overdue
                SeasonsNotPublicBySignupsCount = notPublic.Count,
            }
        };

        return dashboardData;
    }

    /// <summary>Matches overdue for rescheduling. Commissioners and sysadmins only.</summary>
    [HttpGet("season-matches/overdue", Name = "GetOverdueMatches")]
    [ProducesResponseType<List<OverdueMatchDto>>(StatusCodes.Status200OK)]
    public Task<List<OverdueMatchDto>> GetOverdueMatches(CancellationToken ct)
    {
        return _rescheduleService.GetOverdueMatchesAsync(ct);
    }
}