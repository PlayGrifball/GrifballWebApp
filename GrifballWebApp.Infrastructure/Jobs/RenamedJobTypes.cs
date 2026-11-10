using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Events;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Scheduler;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using GrifballWebApp.Infrastructure.UserManagement;
using Quartz;

namespace GrifballWebApp.Infrastructure.Jobs;

/// <summary>
/// The job types' names before they moved from GrifballWebApp.Server to GrifballWebApp.Infrastructure (#375), as Quartz
/// stored them (JOB_CLASS_NAME: the type's name and assembly, no version). During a rolling update an older pod still
/// writes these names (its scheduler, made lazily on an API pod, declares every job again); without the aliases a newer
/// worker can't load the type, and Quartz puts the trigger in ERROR. Nothing is written back: the newer pods' own
/// declarations store the new names. Drop them a release after every environment runs the moved types.
/// </summary>
public static class RenamedJobTypes
{
    public static readonly IReadOnlyDictionary<string, Type> FromServer = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["GrifballWebApp.Server.DatabaseBackup.DatabaseBackupJob, GrifballWebApp.Server"] = typeof(DatabaseBackupJob),
        ["GrifballWebApp.Server.DatabaseBackup.DatabaseExportJob, GrifballWebApp.Server"] = typeof(DatabaseExportJob),
        ["GrifballWebApp.Server.DatabaseBackup.ScratchStopJob, GrifballWebApp.Server"] = typeof(ScratchStopJob),
        ["GrifballWebApp.Server.Events.SeasonEventsJob, GrifballWebApp.Server"] = typeof(SeasonEventsJob),
        ["GrifballWebApp.Server.Excel.SheetExportJob, GrifballWebApp.Server"] = typeof(SheetExportJob),
        ["GrifballWebApp.Server.HaloInfinite.MatchVariantBackfillJob, GrifballWebApp.Server"] = typeof(MatchVariantBackfillJob),
        ["GrifballWebApp.Server.Historical.HistoricalRefreshJob, GrifballWebApp.Server"] = typeof(HistoricalRefreshJob),
        ["GrifballWebApp.Server.Historical.XboxPlaceholderJob, GrifballWebApp.Server"] = typeof(XboxPlaceholderJob),
        ["GrifballWebApp.Server.Matchmaking.MatchmakingQueueJob, GrifballWebApp.Server"] = typeof(MatchmakingQueueJob),
        ["GrifballWebApp.Server.Scheduler.ScheduleRecommendationJob, GrifballWebApp.Server"] = typeof(ScheduleRecommendationJob),
        ["GrifballWebApp.Server.SeasonMatchPage.SeasonMatchPullJob, GrifballWebApp.Server"] = typeof(SeasonMatchPullJob),
        ["GrifballWebApp.Server.SeasonMatchPage.ActiveSeasonPullJob, GrifballWebApp.Server"] = typeof(ActiveSeasonPullJob),
        ["GrifballWebApp.Server.UserManagement.PasswordResetLinkCleanupJob, GrifballWebApp.Server"] = typeof(PasswordResetLinkCleanupJob),
    };

    /// <summary>Each old name resolves to the type it names now (Quartz's type loader, which the job store reads names with).</summary>
    public static void Declare(TypeLoaderOptions loader)
    {
        foreach (var (name, type) in FromServer)
            loader.Map(name, type);
    }
}
