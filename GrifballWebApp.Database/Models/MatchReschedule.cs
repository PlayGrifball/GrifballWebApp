#nullable disable

namespace GrifballWebApp.Database.Models;

public class MatchReschedule : AuditableEntity
{
    public int MatchRescheduleID { get; set; }
    public int SeasonMatchID { get; set; }
    public SeasonMatch SeasonMatch { get; set; }
    
    /// <summary>
    /// The original scheduled time before this reschedule
    /// </summary>
    public DateTime? OriginalScheduledTime { get; set; }
    
    /// <summary>
    /// The new scheduled time for this reschedule
    /// </summary>
    public DateTime? NewScheduledTime { get; set; }
    
    /// <summary>
    /// Reason for the reschedule
    /// </summary>
    public string Reason { get; set; }
    
    /// <summary>
    /// Who requested the reschedule
    /// </summary>
    public int RequestedByUserID { get; set; }
    public User RequestedByUser { get; set; }
    
    /// <summary>
    /// When the reschedule was requested
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Status of the reschedule request
    /// </summary>
    public RescheduleStatus Status { get; set; } = RescheduleStatus.Pending;
    
    /// <summary>
    /// Commissioner who approved/rejected the reschedule
    /// </summary>
    public int? ApprovedByUserID { get; set; }
    public User ApprovedByUser { get; set; }
    
    /// <summary>
    /// When the reschedule was approved/rejected
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>
    /// Discord thread ID for discussing this reschedule
    /// </summary>
    public ulong? DiscordThreadID { get; set; }
    
    /// <summary>
    /// Additional notes from commissioner
    /// </summary>
    public string CommissionerNotes { get; set; }
}

public enum RescheduleStatus
{
    /// <summary>
    /// Reschedule request is pending commissioner approval
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Reschedule request has been approved
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Reschedule request has been rejected
    /// </summary>
    Rejected = 2,
    
    /// <summary>
    /// Reschedule request was cancelled by the requester
    /// </summary>
    Cancelled = 3
}