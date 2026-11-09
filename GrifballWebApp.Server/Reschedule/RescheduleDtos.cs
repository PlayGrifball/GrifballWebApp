using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Server.Reschedule;

public class RescheduleRequestDto
{
    public int SeasonMatchID { get; set; }
    public DateTime? NewScheduledTime { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ProcessRescheduleDto
{
    public bool Approved { get; set; }
    public string? CommissionerNotes { get; set; }
}

public class RescheduleDto
{
    public int MatchRescheduleID { get; set; }
    public int SeasonMatchID { get; set; }
    public string HomeCaptain { get; set; } = string.Empty;
    public string AwayCaptain { get; set; } = string.Empty;
    public DateTime? OriginalScheduledTime { get; set; }
    public DateTime? NewScheduledTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestedByGamertag { get; set; } = string.Empty;
    /// <summary>Who asked, for the link to their profile.</summary>
    public int? RequestedByUserID { get; set; }
    public DateTime RequestedAt { get; set; }
    public RescheduleStatus Status { get; set; }
    public ulong? DiscordThreadID { get; set; }
    /// <summary>
    /// Why the request can only be rejected now (the match has a result since, or lost a team), or null when it can still be
    /// approved: the dashboard then offers only Reject, and says why.
    /// </summary>
    public string? WhyNotApprovable { get; set; }
    /// <summary>Whether an approval can still move the match (<see cref="WhyNotApprovable"/> is null).</summary>
    public bool CanApprove => WhyNotApprovable is null;
}

public class OverdueMatchDto
{
    public int SeasonMatchID { get; set; }
    public string HomeCaptain { get; set; } = string.Empty;
    public string AwayCaptain { get; set; } = string.Empty;
    public DateTime ScheduledTime { get; set; }
    public int HoursOverdue { get; set; }
}