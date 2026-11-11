using System.Security.Cryptography;
using System.Text.Json;
using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.AspNetCore.DataProtection;
using Quartz;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.DatabaseBackup;

public sealed record BackupRequest(BackupFormat Format, string? RequestedBy, int? RequestedByUserID = null) : GrifballWebApp.Infrastructure.Jobs.IRequestedByUser;

/// <summary>An export's target: the connection string protected (Data Protection, time-limited), so the job store never holds it in the clear.</summary>
public sealed record ExportRequest(DatabaseProvider Provider, string ProtectedConnectionString, string? RequestedBy, int? RequestedByUserID = null)
    : GrifballWebApp.Infrastructure.Jobs.IRequestedByUser;

public enum ExportState { None, Running, Succeeded, Failed }

/// <summary>A backup or export's progress, as its job's (Quartz keeps it with the run, for any pod to read).</summary>
public sealed class JobProgress(IJobExecutionContext context) : IProgress<ConversionProgress>
{
    public void Report(ConversionProgress value) => context.ReportProgress(value.Percent, value.Message);
}

/// <summary>The last export to a server: never its connection string. While it runs, how far it got (0 to 100) and what it's on.</summary>
public sealed record BackupExportStatus(ExportState State, string? Provider = null, DateTimeOffset? Started = null, DateTimeOffset? Finished = null,
    int Tables = 0, int Rows = 0, string? Message = null, int? Progress = null, string? ProgressMessage = null);

/// <summary>
/// The last download's backup: being made (or queued), made (and ready, as <see cref="FileName"/>, <see cref="Size"/> bytes),
/// or failed, and why. While it's made, how far it got (0 to 100) and what it's on. One per format; <see cref="RunningFormat"/> is the
/// format being made now, whichever (one backup or export at a time). <see cref="Tables"/> and <see cref="Rows"/>: what it holds,
/// when counted (a script or SQLite file, built table by table); null for a server's own backup (.bak, .dump), taken whole.
/// </summary>
public sealed record BackupDownloadStatus(ExportState State, string? Format = null, DateTimeOffset? Started = null, DateTimeOffset? Finished = null,
    string? FileName = null, int? Tables = null, int? Rows = null, string? Message = null, int? Progress = null, string? ProgressMessage = null, long? Size = null,
    string? RunningFormat = null);

/// <summary>
/// A made backup ready to fetch: its ID on the pod that made it, that pod's address when it isn't this one's (a worker's:
/// DatabaseBackup:NodeUrl), and what the download is called.
/// </summary>
public sealed record ReadyBackup(string FileId, string FileName, string ContentType, string? NodeUrl = null);

/// <summary>
/// Makes the Database Backup page's download (<see cref="DatabaseBackupService.MakeAsync"/>) and keeps the file on this
/// pod (<see cref="BackupFiles"/>); its report carries the file's ID, name and type, which the page's download reads
/// (<see cref="BackupRuns"/>). A failure is reported with what went wrong, for the page.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DatabaseBackupJob(DatabaseBackupService backups, BackupFiles files, IConfiguration configuration, ILogger<DatabaseBackupJob> logger) : IJob<BackupRequest>
{
    public const string Description = "Makes the database as a file to download (from the Database Backup page)";
    /// <summary>A format's run: each format keeps its own backup (BackupFiles) and its own last run.</summary>
    public static string RunName(BackupFormat format) => $"database-backup-{format.ToString().ToLowerInvariant()}";

    public async ValueTask Execute(IJobExecutionContext context, BackupRequest input, CancellationToken cancellationToken = default)
    {
        var format = input.Format.ToString().ToLowerInvariant();
        // One kept a format: its last one goes as the next one starts
        files.Discard(input.Format);
        context.ReportProgress(0, $"Making the {format} backup");
        try
        {
            var built = await backups.MakeAsync(input.Format, cancellationToken, new JobProgress(context)) ?? throw new InvalidOperationException("Another backup or export is running in this process.");
            var size = new FileInfo(built.Path).Length;
            var id = files.Keep(input.Format, built.Path);
            logger.LogInformation("{User}'s {Format} backup is made: {FileName}", input.RequestedBy, format, built.FileName);
            context.Result = JobRunReport.Succeeded($"{built.FileName}: {built.Tables.Count} tables, {built.Tables.Sum(t => t.Rows)} rows")
                .With(BackupRuns.FileMetric, id)
                .With(BackupRuns.FileNameMetric, built.FileName)
                .With(BackupRuns.ContentTypeMetric, built.ContentType)
                .With(BackupRuns.SizeMetric, size)
                .With(BackupRuns.FormatMetric, format)
                .With(BackupRuns.TablesMetric, built.Tables.Count)
                .With(BackupRuns.RowsMetric, built.Tables.Sum(t => t.Rows))
                // Where it is: this pod, for an API pod to fetch it from (several workers: this one, not any)
                .With(BackupRuns.NodeMetric, configuration["DatabaseBackup:NodeUrl"] is { Length: > 0 } node ? node : null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "The {Format} backup failed", format);
            context.Result = JobRunReport.Failed(DatabaseBackupService.DescribeBackup(e)).With(BackupRuns.FormatMetric, format);
        }
    }
}

/// <summary>
/// Copies the database into a new one on another server, for the Database Backup page
/// (<see cref="DatabaseBackupService.ExportAsync"/>). Its target's connection string arrives protected and is unprotected
/// here only; a request older than <see cref="BackupRuns.ExportRequestLifetime"/> is refused.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DatabaseExportJob(DatabaseBackupService backups, IDataProtectionProvider protection, ILogger<DatabaseExportJob> logger) : IJob<ExportRequest>
{
    public const string Description = "Copies the database into a new one on another server (from the Database Backup page)";
    public const string RunName = "database-export";

    public async ValueTask Execute(IJobExecutionContext context, ExportRequest input, CancellationToken cancellationToken = default)
    {
        context.ReportProgress(0, $"Exporting to {input.Provider}");
        try
        {
            string connectionString;
            try
            {
                connectionString = BackupRuns.ExportProtector(protection).Unprotect(input.ProtectedConnectionString);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("The export waited too long to start, and its connection string is no longer readable: start it again.");
            }
            var tables = await backups.ExportAsync(input.Provider, connectionString, cancellationToken, new JobProgress(context)) ?? throw new InvalidOperationException("Another backup or export is running in this process.");
            logger.LogInformation("{User}'s export to {Provider} is done: {Tables} tables", input.RequestedBy, input.Provider, tables.Count);
            context.Result = JobRunReport.Succeeded($"{input.Provider}: {tables.Count} tables, {tables.Sum(t => t.Rows)} rows")
                .With(BackupRuns.ProviderMetric, input.Provider.ToString())
                .With(BackupRuns.TablesMetric, tables.Count)
                .With(BackupRuns.RowsMetric, tables.Sum(t => t.Rows));
        }
        catch (Exception e)
        {
            logger.LogError(e, "The export to {Provider} failed", input.Provider);
            context.Result = JobRunReport.Failed(DatabaseBackupService.Describe(e)).With(BackupRuns.ProviderMetric, input.Provider.ToString());
        }
    }
}

/// <summary>
/// The Database Backup page's runs (<see cref="DatabaseBackupJob"/>, <see cref="DatabaseExportJob"/>): starting one, one
/// backup or export at a time, and their status as the page reads it, from the jobs' status and run history (shared by
/// every pod). Works from any pod.
/// </summary>
public sealed class BackupRuns(OnDemandJobs jobs, IDataProtectionProvider protection, TimeProvider time, IConfiguration configuration)
{
    /// <summary>The execution group of the backup and the export: one of them at a time.</summary>
    public const string ExecutionGroup = JobsServiceCollectionExtensions.DatabaseBackups;
    /// <summary>How long a queued export's connection string stays readable.</summary>
    public static readonly TimeSpan ExportRequestLifetime = TimeSpan.FromHours(1);

    public const string FileMetric = "file";
    public const string FileNameMetric = "fileName";
    public const string ContentTypeMetric = "contentType";
    public const string FormatMetric = "format";
    public const string ProviderMetric = "provider";
    public const string TablesMetric = "tables";
    public const string RowsMetric = "rows";
    public const string NodeMetric = "node";
    public const string SizeMetric = "size";

    /// <summary>How long a made backup can be fetched (BackupFiles keeps it as long): DatabaseBackup:ReadyFor, an hour by default.</summary>
    public TimeSpan ReadyFor { get; } = ReadyForFrom(configuration);

    public static TimeSpan ReadyForFrom(IConfiguration configuration) => configuration.GetValue<TimeSpan?>("DatabaseBackup:ReadyFor") ?? TimeSpan.FromHours(1);

    public static ITimeLimitedDataProtector ExportProtector(IDataProtectionProvider protection)
        => protection.CreateProtector("GrifballWebApp.DatabaseBackup.Export").ToTimeLimitedDataProtector();

    /// <summary>The format of the backup queued or being made, if any.</summary>
    private async Task<BackupFormat?> RunningFormatAsync(CancellationToken ct)
    {
        foreach (var format in Enum.GetValues<BackupFormat>())
            if ((await jobs.Status<DatabaseBackupJob>(DatabaseBackupJob.RunName(format), ct)).State != OnDemandJobState.Idle)
                return format;
        return null;
    }

    private async Task<bool> AnyRunningAsync(CancellationToken ct)
        => await RunningFormatAsync(ct) is not null
           || (await jobs.Status<DatabaseExportJob>(DatabaseExportJob.RunName, ct)).State != OnDemandJobState.Idle;

    /// <summary>Queues a backup in the format; null when a backup or export is queued or running.</summary>
    public async Task<BackupDownloadStatus?> StartDownloadAsync(BackupFormat format, string? requestedBy, int? requestedByUserID, CancellationToken ct = default)
    {
        if (await AnyRunningAsync(ct))
            return null;
        await jobs.Enqueue<DatabaseBackupJob, BackupRequest>(DatabaseBackupJob.RunName(format), new BackupRequest(format, requestedBy, requestedByUserID),
            $"{format} backup", ExecutionGroup, ct);
        return await DownloadStatusAsync(format, ct);
    }

    /// <summary>Queues an export (the connection string protected); null when a backup or export is queued or running. Throws ArgumentException for a connection string the provider can't read.</summary>
    public async Task<BackupExportStatus?> StartExportAsync(DatabaseProvider provider, string connectionString, string? requestedBy, int? requestedByUserID,
        CancellationToken ct = default)
    {
        DatabaseBackupService.Validate(provider, connectionString);
        if (await AnyRunningAsync(ct))
            return null;
        var protectedConnectionString = ExportProtector(protection).Protect(connectionString, ExportRequestLifetime);
        await jobs.Enqueue<DatabaseExportJob, ExportRequest>(DatabaseExportJob.RunName, new ExportRequest(provider, protectedConnectionString, requestedBy, requestedByUserID),
            $"Export to {provider}", ExecutionGroup, ct);
        return await ExportStatusAsync(ct) with { Provider = provider.ToString() };
    }

    /// <summary>The format's backup: being made, made, or failed; and the format being made now, whichever.</summary>
    public async Task<BackupDownloadStatus> DownloadStatusAsync(BackupFormat format, CancellationToken ct = default)
    {
        var running = (await RunningFormatAsync(ct))?.ToString().ToLowerInvariant();
        return await FormatStatusAsync(format, ct) with { RunningFormat = running };
    }

    private async Task<BackupDownloadStatus> FormatStatusAsync(BackupFormat format, CancellationToken ct)
    {
        var status = await jobs.Status<DatabaseBackupJob>(DatabaseBackupJob.RunName(format), ct);
        var name = format.ToString().ToLowerInvariant();
        if (status.State != OnDemandJobState.Idle)
            return new BackupDownloadStatus(ExportState.Running, name, Started: status.StartedAt, Progress: status.Progress, ProgressMessage: status.ProgressMessage);
        if (status.LastRun is not { } run)
            return new BackupDownloadStatus(ExportState.None, name);
        var metrics = Metrics(run);
        var (started, finished) = Times(run);
        if (run.Result != nameof(JobRunResult.Succeeded))
            return new BackupDownloadStatus(ExportState.Failed, name, started, finished, Message: run.Error ?? run.Summary);
        // Kept as long as this says: after it, as if none was made
        if (finished + ReadyFor <= time.GetUtcNow())
            return new BackupDownloadStatus(ExportState.None, name);
        return new BackupDownloadStatus(ExportState.Succeeded, name, started, finished, Text(metrics, FileNameMetric),
            OptionalNumber(metrics, TablesMetric), OptionalNumber(metrics, RowsMetric), Size: metrics.TryGetValue(SizeMetric, out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : null);
    }

    public async Task<BackupExportStatus> ExportStatusAsync(CancellationToken ct = default)
    {
        var status = await jobs.Status<DatabaseExportJob>(DatabaseExportJob.RunName, ct);
        if (status.State != OnDemandJobState.Idle)
            return new BackupExportStatus(ExportState.Running, Started: status.StartedAt, Progress: status.Progress, ProgressMessage: status.ProgressMessage);
        if (status.LastRun is not { } run)
            return new BackupExportStatus(ExportState.None);
        var metrics = Metrics(run);
        var (started, finished) = Times(run);
        return run.Result == nameof(JobRunResult.Succeeded)
            ? new BackupExportStatus(ExportState.Succeeded, Text(metrics, ProviderMetric), started, finished, Number(metrics, TablesMetric), Number(metrics, RowsMetric))
            : new BackupExportStatus(ExportState.Failed, Text(metrics, ProviderMetric), started, finished, Message: run.Error ?? run.Summary);
    }

    /// <summary>The format's backup, while it can be fetched; null when there's none.</summary>
    public async Task<ReadyBackup?> ReadyAsync(BackupFormat format, CancellationToken ct = default)
    {
        var status = await jobs.Status<DatabaseBackupJob>(DatabaseBackupJob.RunName(format), ct);
        if (status.State != OnDemandJobState.Idle || status.LastRun is not { Result: nameof(JobRunResult.Succeeded) } run)
            return null;
        if (Times(run).Finished + ReadyFor <= time.GetUtcNow())
            return null;
        var metrics = Metrics(run);
        return Text(metrics, FileMetric) is { } file && Text(metrics, FileNameMetric) is { } fileName
            ? new ReadyBackup(file, fileName, Text(metrics, ContentTypeMetric) ?? "application/octet-stream", Text(metrics, NodeMetric))
            : null;
    }

    private static (DateTimeOffset Started, DateTimeOffset Finished) Times(OnDemandRunDto run)
    {
        var started = new DateTimeOffset(DateTime.SpecifyKind(run.FiredAt, DateTimeKind.Utc));
        return (started, started + TimeSpan.FromMilliseconds(run.DurationMs));
    }

    private static Dictionary<string, JsonElement> Metrics(OnDemandRunDto run) => ParseMetrics(run.MetricsJson);

    /// <summary>A run's metrics (JobRunReport.With), by name; empty for none.</summary>
    public static Dictionary<string, JsonElement> ParseMetrics(string? json)
        => string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];

    /// <summary>A text metric; null when it's missing or not text.</summary>
    public static string? Text(Dictionary<string, JsonElement> metrics, string name)
        => metrics.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A number metric; null when it's missing or not a number (never measured).</summary>
    public static int? OptionalNumber(Dictionary<string, JsonElement> metrics, string name)
        => metrics.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    /// <summary>A number metric; 0 when it's missing or not a number.</summary>
    public static int Number(Dictionary<string, JsonElement> metrics, string name)
        => metrics.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
}
