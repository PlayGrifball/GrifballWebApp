using GrifballWebApp.Database.Models;

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
    public DateTime RequestedAt { get; set; }
    public RescheduleStatus Status { get; set; }
    public ulong? DiscordThreadID { get; set; }
}

public class OverdueMatchDto
{
    public int SeasonMatchID { get; set; }
    public string HomeCaptain { get; set; } = string.Empty;
    public string AwayCaptain { get; set; } = string.Empty;
    public DateTime ScheduledTime { get; set; }
    public int HoursOverdue { get; set; }
}