using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical.Parsing;
using GrifballWebApp.Server.Historical.Sources;
using GrifballWebApp.Server.Historical.Vods;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GrifballWebApp.Server.Profile;

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
    private readonly VodService? _vods;
    private readonly IUserMergeService? _userMerges;

    /// <param name="vods">The VODs, found on each refresh and matched again after each import; null leaves them out.</param>
    public HistoricalImportRunner(GrifballContext context, HistoricalSourceService sources, IHistoricalImportService import, HistoricalImportJobs jobs,
        IOptions<HistoricalGuardrailOptions> guardrail, ILogger<HistoricalImportRunner> logger, VodService? vods = null, IUserMergeService? userMerges = null)
    {
        _context = context;
        _sources = sources;
        _import = import;
        _jobs = jobs;
        _guardrail = guardrail.Value;
        _logger = logger;
        _vods = vods;
        _userMerges = userMerges;
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
        // The channel pages were just downloaded: their VODs (and the feed's) are found now, on the same schedule
        await VodStep(async vods =>
        {
            var found = await vods.Sync(stage => progress(new HistoricalImportProgress(stage, 0, 0)), ct);
            report.SourcesFailed.AddRange(found.Failed.Select(f => $"VODs: {f}"));
        }, report, ct);
        // A refresh too (daily), so placeholders made before an alias was known join their player without waiting for an import
        await ConsolidationStep(report, ct);
        var scheduled = job.Trigger == HistoricalImportJobTrigger.Scheduled;
        // Only a source the import reads (one with a config) is worth an import: the channel page differs on every
        // download, and its VODs were just synced above
        var changedIds = result.Changed.Select(c => c.SourceID).ToList();
        var imported = changedIds.Count == 0 ? 0 : await _context.HistoricalSources.CountAsync(s => changedIds.Contains(s.HistoricalSourceID) && s.Config != null, ct);
        if (scheduled && imported > 0)
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
        var changed = job.ChangedSourceIDs?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // One snapshot at a time: each is parsed and let go of before the next is fetched
        var builder = new HistoricalArchiveBuilder(changed);
        var read = new List<int>();
        await foreach (var content in _sources.Read(snapshotIds, ct))
        {
            builder.Add(content);
            read.Add(content.SnapshotID);
        }
        var build = builder.Finish();
        var seasons = job.SeasonKeys?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? build.Seasons;
        // The snapshots read are the job's: applying a preview (or approving a season) imports what was seen
        await _context.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == job.HistoricalImportJobID)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.SnapshotIDs, string.Join(',', read)), ct);

        var options = new HistoricalImportRunOptions
        {
            DryRun = dryRun,
            Guardrail = job.Guarded ? _guardrail : null,
            Seasons = seasons,
        };
        var report = await _import.Run(build.Input, options, progress, ct);
        report.Warnings.InsertRange(0, build.Warnings);
        // Seasons and teams the import added can be what a VOD waited for
        if (!dryRun)
        {
            await VodStep(async vods => await vods.Rematch(ct), report, ct);
            // The import's placeholders of known aliases join their player
            await ConsolidationStep(report, ct);
        }

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

    /// <summary>A VOD step: a failure there is reported, never the job's (the sources' refresh and import stand).</summary>
    private async Task VodStep(Func<VodService, Task> step, HistoricalImportReport report, CancellationToken ct)
    {
        if (_vods is null)
            return;
        try
        {
            await step(_vods);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "The VODs could not be refreshed");
            report.SourcesFailed.Add($"VODs: {ex.Message}");
            // What the step left unsaved would fail the job's own saves after it (the context is shared)
            _context.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// The alias consolidation (<see cref="HistoricalAliasConsolidator"/>), in the worker so it never runs during an import:
    /// its merges are listed with the warnings (a sysadmin can undo one), and its failures, like a VOD step's, are reported
    /// without failing the job.
    /// </summary>
    private async Task ConsolidationStep(HistoricalImportReport report, CancellationToken ct)
    {
        if (_userMerges is null)
            return;
        try
        {
            var done = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _userMerges, _logger, ct);
            report.Warnings.AddRange(done.Merged.Select(m => $"Alias consolidation: {m}"));
            report.SourcesFailed.AddRange(done.Failed.Select(f => $"Alias consolidation: {f}"));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Historical alias consolidation failed");
            report.SourcesFailed.Add($"Alias consolidation: {ex.Message}");
            _context.ChangeTracker.Clear();
        }
    }

    private static List<int>? ParseIds(string? ids)
        => ids is null ? null : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList();
}
