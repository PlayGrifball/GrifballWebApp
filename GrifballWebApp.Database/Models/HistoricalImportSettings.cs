#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// The scheduled refresh's last run: one row (ID 1), made when first needed. Not temporal: the worker writes it on every
/// scheduled run. Its schedule and pause are its Quartz trigger's.
/// </summary>
public class HistoricalImportSettings : AuditableEntity
{
    public const int SingletonID = 1;

    public int HistoricalImportSettingsID { get; set; }
    /// <summary>
    /// Whether the schedule was paused before it moved to Quartz. Read once, when the worker first makes the refresh's
    /// trigger (GrifballWebApp.Server HistoricalRefreshJob), which then starts paused: the pause is the trigger's since.
    /// </summary>
    public bool SchedulePaused { get; set; }
    public DateTime? LastScheduledRunAt { get; set; }
    /// <summary>The refresh job the last scheduled run queued.</summary>
    public int? LastScheduledJobID { get; set; }
    /// <summary>Why the last scheduled run didn't queue one (paused, a run in progress), for the page.</summary>
    public string LastScheduledRunNote { get; set; }
}
