using System.Security.Cryptography;
using System.Text.Json;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using GrifballWebApp.Server.Jobs;
using Microsoft.AspNetCore.DataProtection;
using Quartz;

namespace GrifballWebApp.Server.DatabaseBackup;

public sealed record BackupRequest(BackupFormat Format, string? RequestedBy);

/// <summary>An export's target: the connection string protected (Data Protection, time-limited), so the job store never holds it in the clear.</summary>
public sealed record ExportRequest(DatabaseProvider Provider, string ProtectedConnectionString, string? RequestedBy);

public enum ExportState { None, Running, Succeeded, Failed }

/// <summary>The last export to a server: never its connection string.</summary>
public sealed record BackupExportStatus(ExportState State, string? Provider = null, DateTimeOffset? Started = null, DateTimeOffset? Finished = null,
    int Tables = 0, int Rows = 0, string? Message = null);

/// <summary>The last download's backup: being made (or queued), made (and ready, as <see cref="FileName"/>), or failed, and why.</summary>
public sealed record BackupDownloadStatus(ExportState State, string? Format = null, DateTimeOffset? Started = null, DateTimeOffset? Finished = null,
    string? FileName = null, int Tables = 0, int Rows = 0, string? Message = null);

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
    public const string RunName = "database-backup";

    public async ValueTask Execute(IJobExecutionContext context, BackupRequest input, CancellationToken ct = default)
    {
        var format = input.Format.ToString().ToLowerInvariant();
        // One kept at a time: the last one goes as the next one starts
        files.DiscardAll();
        context.ReportProgress(0, $"Making the {format} backup");
        try
        {
            var built = await backups.MakeAsync(input.Format, ct) ?? throw new InvalidOperationException("Another backup or export is running in this process.");
            var id = files.Keep(built.Path);
            logger.LogInformation("{User}'s {Format} backup is made: {FileName}", input.RequestedBy, format, built.FileName);
            context.Result = JobRunReport.Succeeded($"{built.FileName}: {built.Tables.Count} tables, {built.Tables.Sum(t => t.Rows)} rows")
                .With(BackupRuns.FileMetric, id)
                .With(BackupRuns.FileNameMetric, built.FileName)
                .With(BackupRuns.ContentTypeMetric, built.ContentType)
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

    public async ValueTask Execute(IJobExecutionContext context, ExportRequest input, CancellationToken ct = default)
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
            var tables = await backups.ExportAsync(input.Provider, connectionString, ct) ?? throw new InvalidOperationException("Another backup or export is running in this process.");
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

    /// <summary>How long a made backup can be fetched (BackupFiles keeps it as long): DatabaseBackup:ReadyFor, an hour by default.</summary>
    public TimeSpan ReadyFor { get; } = ReadyForFrom(configuration);

    public static TimeSpan ReadyForFrom(IConfiguration configuration) => configuration.GetValue<TimeSpan?>("DatabaseBackup:ReadyFor") ?? TimeSpan.FromHours(1);

    public static ITimeLimitedDataProtector ExportProtector(IDataProtectionProvider protection)
        => protection.CreateProtector("GrifballWebApp.DatabaseBackup.Export").ToTimeLimitedDataProtector();

    private async Task<bool> AnyRunningAsync(CancellationToken ct)
        => (await jobs.Status<DatabaseBackupJob>(DatabaseBackupJob.RunName, ct)).State != OnDemandJobState.Idle
           || (await jobs.Status<DatabaseExportJob>(DatabaseExportJob.RunName, ct)).State != OnDemandJobState.Idle;

    /// <summary>Queues a backup in the format; null when a backup or export is queued or running.</summary>
    public async Task<BackupDownloadStatus?> StartDownloadAsync(BackupFormat format, string? requestedBy, CancellationToken ct = default)
    {
        if (await AnyRunningAsync(ct))
            return null;
        await jobs.Enqueue<DatabaseBackupJob, BackupRequest>(DatabaseBackupJob.RunName, new BackupRequest(format, requestedBy),
            $"{format} backup", ExecutionGroup, ct);
        return await DownloadStatusAsync(ct) with { Format = format.ToString().ToLowerInvariant() };
    }

    /// <summary>Queues an export (the connection string protected); null when a backup or export is queued or running. Throws ArgumentException for a connection string the provider can't read.</summary>
    public async Task<BackupExportStatus?> StartExportAsync(DatabaseProvider provider, string connectionString, string? requestedBy, CancellationToken ct = default)
    {
        DatabaseBackupService.Validate(provider, connectionString);
        if (await AnyRunningAsync(ct))
            return null;
        var protectedConnectionString = ExportProtector(protection).Protect(connectionString, ExportRequestLifetime);
        await jobs.Enqueue<DatabaseExportJob, ExportRequest>(DatabaseExportJob.RunName, new ExportRequest(provider, protectedConnectionString, requestedBy),
            $"Export to {provider}", ExecutionGroup, ct);
        return await ExportStatusAsync(ct) with { Provider = provider.ToString() };
    }

    public async Task<BackupDownloadStatus> DownloadStatusAsync(CancellationToken ct = default)
    {
        var status = await jobs.Status<DatabaseBackupJob>(DatabaseBackupJob.RunName, ct);
        if (status.State != OnDemandJobState.Idle)
            return new BackupDownloadStatus(ExportState.Running, Started: status.StartedAt);
        if (status.LastRun is not { } run)
            return new BackupDownloadStatus(ExportState.None);
        var metrics = Metrics(run);
        var (started, finished) = Times(run);
        if (run.Result != nameof(JobRunResult.Succeeded))
            return new BackupDownloadStatus(ExportState.Failed, Text(metrics, FormatMetric), started, finished, Message: run.Error ?? run.Summary);
        // Kept as long as this says: after it, as if none was made
        if (finished + ReadyFor <= time.GetUtcNow())
            return new BackupDownloadStatus(ExportState.None);
        return new BackupDownloadStatus(ExportState.Succeeded, Text(metrics, FormatMetric), started, finished, Text(metrics, FileNameMetric),
            Number(metrics, TablesMetric), Number(metrics, RowsMetric));
    }

    public async Task<BackupExportStatus> ExportStatusAsync(CancellationToken ct = default)
    {
        var status = await jobs.Status<DatabaseExportJob>(DatabaseExportJob.RunName, ct);
        if (status.State != OnDemandJobState.Idle)
            return new BackupExportStatus(ExportState.Running, Started: status.StartedAt);
        if (status.LastRun is not { } run)
            return new BackupExportStatus(ExportState.None);
        var metrics = Metrics(run);
        var (started, finished) = Times(run);
        return run.Result == nameof(JobRunResult.Succeeded)
            ? new BackupExportStatus(ExportState.Succeeded, Text(metrics, ProviderMetric), started, finished, Number(metrics, TablesMetric), Number(metrics, RowsMetric))
            : new BackupExportStatus(ExportState.Failed, Text(metrics, ProviderMetric), started, finished, Message: run.Error ?? run.Summary);
    }

    /// <summary>The last backup made, while it can be fetched; null when there's none.</summary>
    public async Task<ReadyBackup?> ReadyAsync(CancellationToken ct = default)
    {
        var status = await jobs.Status<DatabaseBackupJob>(DatabaseBackupJob.RunName, ct);
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

    /// <summary>A number metric; 0 when it's missing or not a number.</summary>
    public static int Number(Dictionary<string, JsonElement> metrics, string name)
        => metrics.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
}
