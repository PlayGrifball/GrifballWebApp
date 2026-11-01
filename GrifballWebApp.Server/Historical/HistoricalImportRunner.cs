using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical.Parsing;
using GrifballWebApp.Server.Historical.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Runs the worker's jobs of the sources (HistoricalImportWorker runs uploads itself):
/// <list type="bullet">
/// <item>Refresh: downloads the sources. A scheduled one queues a guarded import of the seasons the changed sources make
/// up, and nothing when none changed.</item>
/// <item>Preview: a dry run of importing every source's latest snapshot; each season's removals are listed. Applying it
/// imports those same snapshots.</item>
/// <item>Import: parses the snapshots (the job's, or each source's latest) and imports them, the guardrail stopping a
/// season that would remove too much when the job is guarded.</item>
/// </list>
/// HistoricalImportService stays the only importer; this only builds its input from the snapshots.
/// </summary>
public class HistoricalImportRunner
{
    private readonly GrifballContext _context;
    private readonly HistoricalSourceService _sources;
    private readonly IHistoricalImportService _import;
    private readonly HistoricalImportJobs _jobs;
    private readonly HistoricalGuardrailOptions _guardrail;
    private readonly ILogger<HistoricalImportRunner> _logger;

    public HistoricalImportRunner(GrifballContext context, HistoricalSourceService sources, IHistoricalImportService import, HistoricalImportJobs jobs,
        IOptions<HistoricalGuardrailOptions> guardrail, ILogger<HistoricalImportRunner> logger)
    {
        _context = context;
        _sources = sources;
        _import = import;
        _jobs = jobs;
        _guardrail = guardrail.Value;
        _logger = logger;
    }

    public async Task<HistoricalImportReport> Run(int jobId, Func<HistoricalImportProgress, Task> progress, CancellationToken ct)
    {
        var job = await _context.HistoricalImportJobs.AsNoTracking().FirstAsync(j => j.HistoricalImportJobID == jobId, ct);
        return job.Kind switch
        {
            HistoricalImportJobKind.Refresh => await Refresh(job, progress, ct),
            HistoricalImportJobKind.Preview => await Import(job, dryRun: true, progress, ct),
            _ => await Import(job, dryRun: false, progress, ct),
        };
    }

    private async Task<HistoricalImportReport> Refresh(HistoricalImportJob job, Func<HistoricalImportProgress, Task> progress, CancellationToken ct)
    {
        var result = await _sources.Refresh(stage => progress(new HistoricalImportProgress(stage, 0, 0)), ct);
        var report = new HistoricalImportReport
        {
            SourcesChanged = result.Changed.Select(c => c.SourceID).ToList(),
            SourcesFailed = result.Failed.Select(f => $"{f.SourceID}: {f.Reason}").ToList(),
            SourcesUnchanged = result.Unchanged,
        };
        var scheduled = job.Trigger == HistoricalImportJobTrigger.Scheduled;
        if (scheduled && result.Changed.Count > 0)
        {
            report.ImportJobID = await _jobs.Enqueue(new HistoricalImportJob
            {
                Kind = HistoricalImportJobKind.Import,
                Trigger = HistoricalImportJobTrigger.Scheduled,
                Guarded = true,
                SnapshotIDs = string.Join(',', await _sources.LatestSnapshotIds(ct)),
                ChangedSourceIDs = string.Join(',', report.SourcesChanged),
            }, ct);
        }
        if (result.Failed.Count > 0)
            _logger.LogWarning("Historical sources refreshed ({Trigger}): {Changed} changed, {Unchanged} unchanged, {Failed} failed ({Failures}); import job {ImportJobID}",
                job.Trigger, result.Changed.Count, result.Unchanged, result.Failed.Count, string.Join("; ", report.SourcesFailed), report.ImportJobID);
        else
            _logger.LogInformation("Historical sources refreshed ({Trigger}): {Changed} changed, {Unchanged} unchanged, {Failed} failed; import job {ImportJobID}",
                job.Trigger, result.Changed.Count, result.Unchanged, result.Failed.Count, report.ImportJobID);
        return report;
    }

    private async Task<HistoricalImportReport> Import(HistoricalImportJob job, bool dryRun, Func<HistoricalImportProgress, Task> progress, CancellationToken ct)
    {
        await progress(new HistoricalImportProgress("Reading the sources", 0, 0));
        var snapshotIds = ParseIds(job.SnapshotIDs) ?? await _sources.LatestSnapshotIds(ct);
        var contents = await _sources.Load(snapshotIds, ct);
        var changed = job.ChangedSourceIDs?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var build = HistoricalArchiveBuilder.Build(contents, changed);
        var seasons = job.SeasonKeys?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? build.Seasons;
        // The snapshots read are the job's: applying a preview (or approving a season) imports what was seen
        await _context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == job.HistoricalImportJobID)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.SnapshotIDs, string.Join(',', contents.Select(c => c.SnapshotID))), ct);

        var options = new HistoricalImportRunOptions
        {
            DryRun = dryRun,
            Guardrail = job.Guarded ? _guardrail : null,
            Seasons = seasons,
        };
        var report = await _import.Run(build.Input, options, progress, ct);
        report.Warnings.InsertRange(0, build.Warnings);

        // Seasons a sysadmin decides on: the stopped ones, and every one a preview would remove rows from
        var reviews = report.SeasonRemovals.Where(r => r.Stopped || (dryRun && r.RemovesAnything)).Select(r => new HistoricalSeasonReview
        {
            HistoricalImportJobID = job.HistoricalImportJobID,
            SeasonKey = r.SeasonKey.Length > 200 ? r.SeasonKey[..200] : r.SeasonKey,
            SeasonName = r.SeasonName is { Length: > 200 } name ? name[..200] : r.SeasonName,
            Status = r.Stopped ? HistoricalSeasonReviewStatus.NeedsReview : HistoricalSeasonReviewStatus.Preview,
            StatsBefore = r.StatsBefore,
            StatsRemoved = r.StatsRemoved,
            TeamsBefore = r.TeamsBefore,
            TeamsRemoved = r.TeamsRemoved,
            TitlesBefore = r.TitlesBefore,
            TitlesRemoved = r.TitlesRemoved,
        }).ToList();
        if (reviews.Count > 0)
        {
            _context.HistoricalSeasonReviews.AddRange(reviews);
            await _context.SaveChangesAsync(ct);
        }

        var stopped = reviews.Count(r => r.Status == HistoricalSeasonReviewStatus.NeedsReview);
        if (stopped > 0 || report.Errors.Count > 0)
            _logger.LogWarning("Historical {Kind} job {JobID} ({Trigger}): {Seasons} seasons, {Stopped} need review, {Errors} failed",
                job.Kind, job.HistoricalImportJobID, job.Trigger, report.SeasonsCreated + report.SeasonsUpdated, stopped, report.Errors.Count);
        else
            _logger.LogInformation("Historical {Kind} job {JobID} ({Trigger}): {Seasons} seasons, none need review",
                job.Kind, job.HistoricalImportJobID, job.Trigger, report.SeasonsCreated + report.SeasonsUpdated);
        return report;
    }

    private static List<int>? ParseIds(string? ids)
        => ids is null ? null : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList();
}
