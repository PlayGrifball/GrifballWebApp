using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Application.Commissioner;

/// <summary>
/// The commissioner dashboard: pending reschedules, overdue matches, the seasons still to start that won't be public by their
/// signups, and their counts. The site's dashboard and the agents' get_commissioner_dashboard both read it.
/// </summary>
public class CommissionerDashboardService
{
    /// <summary>Hours overdue past which a match is critical: three days.</summary>
    public const int CriticalHoursOverdue = 72;

    private readonly RescheduleService _reschedules;
    private readonly EventOrganizerService _seasons;

    public CommissionerDashboardService(RescheduleService reschedules, EventOrganizerService seasons)
    {
        _reschedules = reschedules;
        _seasons = seasons;
    }

    public async Task<CommissionerDashboardDto> GetAsync(CancellationToken ct = default)
    {
        var pendingReschedules = await _reschedules.GetPendingReschedulesAsync(ct);
        var overdueMatches = await _reschedules.GetOverdueMatchesAsync(ct);
        var notPublic = await _seasons.GetSeasonsNotPublicBySignups(ct);
        return new CommissionerDashboardDto
        {
            PendingReschedules = pendingReschedules,
            OverdueMatches = overdueMatches,
            SeasonsNotPublicBySignups = notPublic,
            Summary = new DashboardSummaryDto
            {
                PendingRescheduleCount = pendingReschedules.Count,
                OverdueMatchCount = overdueMatches.Count,
                CriticalOverdueCount = overdueMatches.Count(m => m.HoursOverdue > CriticalHoursOverdue),
                SeasonsNotPublicBySignupsCount = notPublic.Count,
            },
        };
    }
}
