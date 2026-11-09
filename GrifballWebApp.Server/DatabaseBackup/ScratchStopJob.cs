using GrifballWebApp.Infrastructure.DatabaseBackup;
using Quartz;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>A queued stop of the scratch server: which pod queued it.</summary>
public sealed record ScratchStopRequest(string? QueuedBy = null);

/// <summary>
/// Stops the scratch server (<see cref="IScratchScaler.StopAsync"/>), queued by a worker as it starts
/// (<see cref="ScratchScaleDown"/>). It's in the backups' execution group (<see cref="BackupRuns.ExecutionGroup"/>, one at
/// a time across the pods, its count in the store), so it never runs while a backup or export is queued to run, starting
/// or running on any worker: it waits for that run, which stops the server itself when it's done. A run on a worker that
/// died holds the group until the cluster sees the worker gone and clears its runs; then this one runs.
/// </summary>
[DisallowConcurrentExecution]
[ExecutionGroup(Jobs.JobsServiceCollectionExtensions.DatabaseBackups)]
public sealed class ScratchStopJob(IScratchScaler scaler) : IJob<ScratchStopRequest>
{
    public const string Description = "Stops the Database Backup page's scratch server, queued as a worker starts (never during a backup or export)";
    public const string RunName = "scratch-stop";

    public async ValueTask Execute(IJobExecutionContext context, ScratchStopRequest input, CancellationToken cancellationToken = default)
    {
        await scaler.StopAsync(cancellationToken);
        context.Result = JobRunReport.Succeeded(input.QueuedBy is { } pod ? $"Stopped the scratch server (queued by {pod})" : "Stopped the scratch server");
    }
}

/// <summary>The stop, as an on-demand job, where the scratch server is started on demand (<c>DatabaseBackup:Scratch:Deployment</c>).</summary>
public static class ScratchStopJobRegistration
{
    public const string DeploymentKey = "DatabaseBackup:Scratch:Deployment";

    public static Jobs.RecurringJobs AddScratchStop(this Jobs.RecurringJobs jobs, IConfiguration configuration)
        => string.IsNullOrEmpty(configuration[DeploymentKey]) ? jobs : jobs.AddOnDemand<ScratchStopJob>(ScratchStopJob.RunName, ScratchStopJob.Description);
}

/// <summary>
/// Queues a stop of the scratch server (<see cref="ScratchStopJob"/>) when a worker starts, in case a backup on a worker
/// that died (or was stopped by a rollout mid-backup) left it running: in the background, so a job store that isn't
/// reachable yet never holds up startup, and tried again every <see cref="RetryDelay"/> until it's queued. The stop runs
/// as a job in the backups' execution group, so it waits for a backup or export running on another worker instead of
/// stopping the server under it, and runs once a dead worker's backup has been cleared.
/// </summary>
public sealed class ScratchScaleDown(Jobs.OnDemandJobs jobs, ILogger<ScratchScaleDown> logger) : BackgroundService
{
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Off the startup path before the first call.
        await Task.Yield();
        while (true)
        {
            try
            {
                await jobs.Enqueue<ScratchStopJob, ScratchStopRequest>(ScratchStopJob.RunName, new ScratchStopRequest(Environment.MachineName),
                    "Stop the scratch server", BackupRuns.ExecutionGroup, stoppingToken);
                logger.LogInformation("Queued the scratch server's stop: it runs once no backup or export is using it");
                return;
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Couldn't queue the scratch server's stop at startup (the job store didn't answer): trying again in {Delay}", RetryDelay);
            }
            // The host stopping ends it here (or in the call above), as a cancellation
            await Task.Delay(RetryDelay, stoppingToken);
        }
    }
}
