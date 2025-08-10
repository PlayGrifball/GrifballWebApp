using GrifballWebApp.Server.Reschedule;

namespace GrifballWebApp.Server.Commissioner;

public class CommissionerDashboardDto
{
    public List<RescheduleDto> PendingReschedules { get; set; } = [];
    public List<OverdueMatchDto> OverdueMatches { get; set; } = [];
    public DashboardSummaryDto Summary { get; set; } = new();
}

public class DashboardSummaryDto
{
    public int PendingRescheduleCount { get; set; }
    public int OverdueMatchCount { get; set; }
    public int CriticalOverdueCount { get; set; }
}