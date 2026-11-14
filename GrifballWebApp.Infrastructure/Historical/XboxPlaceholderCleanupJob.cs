using Quartz;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.Historical;

/// <summary>
/// Removes the placeholders of Xbox accounts that are no league players (<see cref="XboxPlaceholderCleanup"/>). Run from the
/// Jobs page only (<see cref="RecurringJob.ManualOnly"/>): it deletes users, so a sysadmin starts it when they choose.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class XboxPlaceholderCleanupJob : IJob
{
    public static readonly JobKey Key = new("xbox-placeholder-cleanup", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Removes the placeholder users of Xbox accounts in no league game that nothing else names (run by hand)",
        ManualOnly = true,
        // Not used: a manual-only job has no trigger
        DefaultSchedule = (trigger, now) => trigger.StartAt(now),
    };

    private readonly XboxPlaceholderCleanup _cleanup;
    private readonly ILogger<XboxPlaceholderCleanupJob> _logger;

    public XboxPlaceholderCleanupJob(XboxPlaceholderCleanup cleanup, ILogger<XboxPlaceholderCleanupJob> logger)
    {
        _cleanup = cleanup;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var removed = await _cleanup.RemoveAsync(cancellationToken);
        foreach (var placeholder in removed)
            LogRemovedOne(_logger, placeholder.Gamertag, placeholder.UserID);
        LogRemoved(_logger, removed.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed the placeholder of {Gamertag} (user {UserID}): no league player")]
    private static partial void LogRemovedOne(ILogger logger, string gamertag, int userID);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} placeholders of Xbox accounts that are no league players")]
    private static partial void LogRemoved(ILogger logger, int count);
}
