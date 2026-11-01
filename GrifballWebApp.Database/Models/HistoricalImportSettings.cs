#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// The scheduled refresh's state: one row (ID 1), made when first needed. Not temporal: the worker writes it on every
/// scheduled run; who paused it is in the audit columns.
/// </summary>
public class HistoricalImportSettings : AuditableEntity
{
    public const int SingletonID = 1;

    public int HistoricalImportSettingsID { get; set; }
    /// <summary>Scheduled runs are skipped while paused; manual runs still work.</summary>
    public bool SchedulePaused { get; set; }
    public DateTime? LastScheduledRunAt { get; set; }
    /// <summary>The refresh job the last scheduled run queued.</summary>
    public int? LastScheduledJobID { get; set; }
    /// <summary>Why the last scheduled run didn't queue one (paused, a run in progress), for the page.</summary>
    public string LastScheduledRunNote { get; set; }
}
