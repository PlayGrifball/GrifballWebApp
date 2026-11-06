using GrifballWebApp.Database;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Services;
using Quartz;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// Gives the matches saved before game variants were kept theirs (<see cref="IDataPullService.BackfillGameVariants"/>):
/// hourly by default, a batch at a time (<c>MatchVariantBackfill:BatchSize</c>, 200), so a run holds the Halo pulls'
/// execution group (and the gateway's quota) for minutes, not hours. Then decodes the matches not decoded yet and every
/// match of the variants it filled in, whose layout may have changed (<see cref="CustomScoreLayouts.DecodeAsync"/>).
/// Once every match has its variant and is decoded, a run is two queries. "Run now" on the Jobs page starts a batch at once.
/// </summary>
[DisallowConcurrentExecution]
// Also for "run now" on the Jobs page: the Halo calls stay one job at a time with the pulls'
[ExecutionGroup(JobsServiceCollectionExtensions.HaloMatchPulls)]
public sealed class MatchVariantBackfillJob : IJob
{
    public const int DefaultBatchSize = 200;

    public static readonly JobKey Key = new("match-variant-backfill", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Fetches the game variant of matches saved without one from Halo Infinite, a batch an hour, and decodes their custom scores",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(10))
            .WithExecutionGroup(JobsServiceCollectionExtensions.HaloMatchPulls)
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    private readonly IDataPullService _dataPull;
    private readonly GrifballContext _context;
    private readonly ILogger<MatchVariantBackfillJob> _logger;
    private readonly int _batchSize;

    public MatchVariantBackfillJob(IDataPullService dataPull, GrifballContext context, IConfiguration configuration, ILogger<MatchVariantBackfillJob> logger)
    {
        _dataPull = dataPull;
        _context = context;
        _logger = logger;
        _batchSize = configuration.GetValue("MatchVariantBackfill:BatchSize", DefaultBatchSize);
    }

    public static JobRunReport Report(VariantBackfillResult result, int decoded)
    {
        var summary = decoded > 0 ? $"{result.Summary}; {decoded} matches decoded again" : result.Summary;
        return (result.Failed.Count == 0 ? JobRunReport.Succeeded(summary) : JobRunReport.Failed(summary))
            .With("missing", result.Missing)
            .With("attempted", result.Attempted)
            .With("filled", result.Filled)
            .With("failed", result.Failed.Count)
            .With("decoded", decoded);
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default)
    {
        var result = await _dataPull.BackfillGameVariants(_batchSize,
            (done, of) => context.ReportProgress(of == 0 ? 100 : done * 100 / of, $"{done} of {of} matches looked up"), ct);

        var decoded = await new CustomScoreLayouts(_context).DecodeAsync(result.Versions ?? [],
            (done, of) => context.ReportProgress(of == 0 ? 100 : done * 100 / of, $"{done} of {of} matches decoded"), ct);
        if (decoded > 0)
            _logger.LogInformation("Decoded the custom scores of {Count} matches again", decoded);

        context.Result = result.Missing == 0 && decoded == 0
            ? JobRunReport.Skipped("Every match has its game variant and is decoded")
            : Report(result, decoded);
    }
}
