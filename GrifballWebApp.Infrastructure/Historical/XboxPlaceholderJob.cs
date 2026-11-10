using Quartz;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.Historical;

/// <summary>
/// Gives every Xbox account no user holds a placeholder (<see cref="GamertagMatching.EnsureXboxPlaceholdersAsync"/>),
/// hourly by default: the accounts known before placeholders were made for them, and those a gamertag lookup stored
/// since. Idempotent, one cheap query when there are none; one run at a time, or two would race to make the same one.
/// </summary>
public sealed class XboxPlaceholderJob : IJob
{
    public static readonly JobKey Key = new("xbox-placeholders", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Gives Xbox accounts no user holds a placeholder user",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now)
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    private readonly GamertagMatching _matching;
    private readonly ILogger<XboxPlaceholderJob> _logger;

    public XboxPlaceholderJob(GamertagMatching matching, ILogger<XboxPlaceholderJob> logger)
    {
        _matching = matching;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default)
    {
        var made = await _matching.EnsureXboxPlaceholdersAsync(null, ct);
        if (made > 0)
            _logger.LogInformation("Made {Count} placeholders for Xbox accounts no user holds", made);
    }
}
