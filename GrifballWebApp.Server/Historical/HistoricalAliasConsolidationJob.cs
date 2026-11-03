using GrifballWebApp.Database;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Profile;
using Quartz;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Proactively resolves and consolidates historical alias placeholders into canonical player accounts.
/// Runs as a background Quartz job in clustered deployments with [DisallowConcurrentExecution] to prevent pod races.
/// </summary>
[DisallowConcurrentExecution]
public sealed class HistoricalAliasConsolidationJob : IJob
{
    public static readonly JobKey Key = new("historical-alias-consolidation", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Consolidates historical alias placeholders into canonical player accounts",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now)
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    private readonly GrifballContext _context;
    private readonly IUserMergeService _userMerges;
    private readonly ILogger<HistoricalAliasConsolidationJob> _logger;

    public HistoricalAliasConsolidationJob(GrifballContext context, IUserMergeService userMerges, ILogger<HistoricalAliasConsolidationJob> logger)
    {
        _context = context;
        _userMerges = userMerges;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default)
    {
        var merged = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _userMerges, _logger, ct);
        if (merged > 0)
            _logger.LogInformation("Consolidated {Count} historical alias placeholders into canonical accounts", merged);
    }
}
