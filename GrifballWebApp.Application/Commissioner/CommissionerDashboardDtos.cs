using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Application.Commissioner;

public class CommissionerDashboardDto
{
    public List<RescheduleDto> PendingReschedules { get; set; } = [];
    public List<OverdueMatchDto> OverdueMatches { get; set; } = [];
    /// <summary>Seasons still to start that won't be public by their signups: to fix on their edit page.</summary>
    public List<SeasonNotPublicBySignupsDto> SeasonsNotPublicBySignups { get; set; } = [];
    public DashboardSummaryDto Summary { get; set; } = new();
}

public class DashboardSummaryDto
{
    public int PendingRescheduleCount { get; set; }
    public int OverdueMatchCount { get; set; }
    public int CriticalOverdueCount { get; set; }
    public int SeasonsNotPublicBySignupsCount { get; set; }
}
/// <summary>
/// A season still to start that won't be public by the time its signups open (Season.NotPublicBySignups): players can't sign up
/// for a season they can't see, so its "Public at" needs setting no later than its signups open (#435).
/// </summary>
public class SeasonNotPublicBySignupsDto
{
    public required int SeasonID { get; init; }
    public required string SeasonName { get; init; }
    public required DateTime SignupsOpen { get; init; }
    /// <summary>When it's announced; null when it has no date.</summary>
    public required DateTime? PublicAt { get; init; }
}
