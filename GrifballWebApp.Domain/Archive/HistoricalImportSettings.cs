using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Archive;

/// <summary>
/// The scheduled refresh's last run: one row (ID 1, CK_Event_HistoricalImportSettings_Singleton), made when first needed.
/// Not temporal: the worker writes it on every scheduled run. Its schedule and pause are its Quartz trigger's.
/// </summary>
[AggregateRoot]
public class HistoricalImportSettings : AuditableEntity
{
    public const int SingletonID = 1;

    /// <summary>For EF. The one row is made by <see cref="Create"/>.</summary>
    private HistoricalImportSettings() { }

    /// <summary>The row, before any scheduled run.</summary>
    public static HistoricalImportSettings Create() => new() { HistoricalImportSettingsID = SingletonID };

    /// <summary>The row of a schedule that was paused before it moved to Quartz, for tests.</summary>
    internal static HistoricalImportSettings PausedBeforeQuartz() => new() { HistoricalImportSettingsID = SingletonID, SchedulePaused = true };

    public int HistoricalImportSettingsID { get; internal set; }
    /// <summary>
    /// Whether the schedule was paused before it moved to Quartz. Read once, when the worker first makes the refresh's
    /// trigger (GrifballWebApp.Server HistoricalRefreshJob), which then starts paused and clears it
    /// (<see cref="PauseCarriedOver"/>): the pause is the trigger's since.
    /// </summary>
    public bool SchedulePaused { get; private set; }
    public DateTime? LastScheduledRunAt { get; private set; }
    /// <summary>The refresh job the last scheduled run queued.</summary>
    public int? LastScheduledJobID { get; private set; }
    /// <summary>Why the last scheduled run didn't queue one (paused, a run in progress), for the page.</summary>
    public string? LastScheduledRunNote { get; private set; }

    /// <summary>A scheduled run queued the refresh <paramref name="jobID"/> at <paramref name="at"/>: no note.</summary>
    public void ScheduledRunQueued(int jobID, DateTime at)
    {
        LastScheduledRunAt = at;
        LastScheduledJobID = jobID;
        LastScheduledRunNote = null;
    }

    /// <summary>A scheduled run queued nothing, for <paramref name="note"/>. False (nothing to save) when a note is there
    /// already: the first one stays until a run is queued.</summary>
    public bool ScheduledRunWaited(string note)
    {
        if (LastScheduledRunNote is not null)
            return false;
        LastScheduledRunNote = note;
        return true;
    }

    /// <summary>The trigger holds the pause now: carried over once.</summary>
    public void PauseCarriedOver()
    {
        if (!SchedulePaused)
            throw new InvalidOperationException("The schedule wasn't paused before it moved to Quartz");
        SchedulePaused = false;
    }
}
