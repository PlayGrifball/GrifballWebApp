using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

/// <summary>
/// Keeps the gamer picture of every Xbox account (<see cref="XboxImageSource.RunGamerPicturesAsync"/>): every five minutes a run
/// takes the accounts without one (or last checked long ago), asks Halo's profile service for them 20 at a time, and stores
/// the pictures it names. When none is due, a run is one query.
/// </summary>
[DisallowConcurrentExecution]
public sealed class XboxGamerPictureJob(XboxImageSource images, ILogger<XboxGamerPictureJob> logger) : IJob
{
    public static readonly JobKey Key = new("xbox-gamer-pictures", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Fetches and keeps the gamer picture of every Xbox account, 100 accounts every five minutes",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(2))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var report = await images.RunGamerPicturesAsync(cancellationToken);
        context.Result = XboxImageJobReports.Of(report, "gamer picture", logger);
    }
}

/// <summary>
/// Keeps the Halo Infinite emblem of every Xbox account (<see cref="XboxImageSource.RunEmblemsAsync"/>): every five minutes a run
/// takes the accounts without one (or last checked long ago), reads each one's equipped emblem from Halo, and stores the
/// emblem's image from Waypoint. When none is due, a run is one query.
/// </summary>
[DisallowConcurrentExecution]
public sealed class XboxEmblemJob(XboxImageSource images, ILogger<XboxEmblemJob> logger) : IJob
{
    public static readonly JobKey Key = new("xbox-emblems", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Fetches and keeps the Halo Infinite emblem of every Xbox account, 60 accounts every five minutes",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(3))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var report = await images.RunEmblemsAsync(cancellationToken);
        context.Result = XboxImageJobReports.Of(report, "emblem", logger);
    }
}

internal static partial class XboxImageJobReports
{
    public static JobRunReport Of(XboxImagesReport report, string what, ILogger logger)
    {
        if (report.ThrottledFor is { } wait)
            LogThrottled(logger, what, wait);
        var summary = report.Due == 0
            ? $"Every Xbox account has its {what} checked"
            : $"{report.Checked} of {report.Due} accounts checked: {report.Stored} stored, {report.Unchanged} unchanged, {report.NotFound} without one, {report.Failed} failed"
              + (report.ThrottledFor is { } w ? $"; Halo is throttling, stopped (wait {w})" : "");
        var result = report.Due == 0 ? JobRunReport.Skipped(summary)
            : report.Failed > 0 && report.Stored + report.Unchanged + report.NotFound == 0 ? JobRunReport.Failed(summary)
            : JobRunReport.Succeeded(summary);
        return result.With("due", report.Due).With("checked", report.Checked).With("stored", report.Stored)
            .With("unchanged", report.Unchanged).With("notFound", report.NotFound).With("failed", report.Failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Xbox {What} job: Halo is throttling; stopped, next run in five minutes (asked to wait {Wait})")]
    private static partial void LogThrottled(ILogger logger, string what, TimeSpan wait);
}
