using System.Collections.Concurrent;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Jobs;
using static GrifballWebApp.Infrastructure.Test.ScratchStopJobUnitTests;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The scratch server's stop at a worker's start: queued (ScratchScaleDown) as a job in the backups' execution group
/// (ScratchStopJob), on the app's own Quartz setup with its store in the suite's database, so it waits for a backup
/// using the server and runs once it's done.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScratchStopJobTests
{
    private GrifballContext _context = null!;
    private IHost? _host;
    private static readonly string[] expected = new[] { "stop" };

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        await _context.DropDatabaseAndDispose();
    }

    public sealed record HoldRequest(Guid Id);

    /// <summary>A backup standing in: in the backups' group, running until its test releases it.</summary>
    [DisallowConcurrentExecution]
    public sealed class HoldingBackupJob : IJob<HoldRequest>
    {
        public static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Releases = new();

        public async ValueTask Execute(IJobExecutionContext context, HoldRequest input, CancellationToken ct = default)
            => await Releases.GetOrAdd(input.Id, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(ct);
    }

    /// <summary>The app's jobs, started (not yet the scale-down): the stop and the stand-in backup on demand.</summary>
    private async Task<IServiceProvider> StartAsync(Scaler scaler)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _context.Database.GetConnectionString(),
        });
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IScratchScaler>(scaler);
        builder.Services.AddJobs(builder.Configuration, jobs => jobs
            .AddOnDemand<ScratchStopJob>(ScratchStopJob.RunName, ScratchStopJob.Description)
            .AddOnDemand<HoldingBackupJob>("holding-backup", "A backup standing in"));
        _host = builder.Build();
        await _host.StartAsync();
        return _host.Services;
    }

    private static async Task<ScratchScaleDown> ScaleDownAsync(IServiceProvider services)
    {
        var scaleDown = new ScratchScaleDown(services.GetRequiredService<OnDemandJobs>(), NullLogger<ScratchScaleDown>.Instance);
        await scaleDown.StartAsync(CancellationToken.None);
        await scaleDown.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        return scaleDown;
    }

    private static async Task UntilAsync(Func<Task<bool>> done, string what)
    {
        for (var i = 0; i < 600; i++)
        {
            if (await done())
                return;
            await Task.Delay(100);
        }
        Assert.Fail($"Timed out waiting: {what}");
    }

    [Test]
    public async Task A_starting_worker_stops_the_scratch_server_through_a_job()
    {
        var scaler = new Scaler();
        var services = await StartAsync(scaler);

        await ScaleDownAsync(services);

        await UntilAsync(() => Task.FromResult(scaler.Calls.Contains("stop")), "the stop job");
        var jobs = services.GetRequiredService<OnDemandJobs>();
        await UntilAsync(async () => (await jobs.Status<ScratchStopJob>(ScratchStopJob.RunName)).LastRun is { Result: "Succeeded" }, "the stop's run recorded");
        Assert.That(scaler.Calls, Is.EqualTo(expected));
    }

    [Test]
    public async Task The_stop_waits_for_a_backup_using_the_server_then_runs()
    {
        var scaler = new Scaler();
        var services = await StartAsync(scaler);
        var jobs = services.GetRequiredService<OnDemandJobs>();
        var hold = new HoldRequest(Guid.NewGuid());
        var release = HoldingBackupJob.Releases.GetOrAdd(hold.Id, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        try
        {
            // A backup on another worker, in the group
            await jobs.Enqueue<HoldingBackupJob, HoldRequest>("holding-backup", hold, executionGroup: BackupRuns.ExecutionGroup);
            await UntilAsync(async () => (await jobs.Status<HoldingBackupJob>("holding-backup")).State == OnDemandJobState.Running, "the backup to run");

            await ScaleDownAsync(services);
            await Task.Delay(TimeSpan.FromSeconds(3));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(scaler.Calls, Is.Empty, "Not stopped under the backup");
                Assert.That((await jobs.Status<ScratchStopJob>(ScratchStopJob.RunName)).State, Is.EqualTo(OnDemandJobState.Queued));
            }

            release.SetResult();
            await UntilAsync(() => Task.FromResult(scaler.Calls.Contains("stop")), "the stop once the backup is done");
        }
        finally
        {
            release.TrySetResult();
            HoldingBackupJob.Releases.TryRemove(hold.Id, out _);
        }
    }

    [Test]
    public async Task A_job_store_that_doesnt_answer_is_tried_again_until_the_worker_stops()
    {
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns<ValueTask<IScheduler>>(_ => throw new InvalidOperationException("The database isn't up yet"));
        var jobs = new OnDemandJobs(schedulers, Substitute.For<IExecutionHistoryStore>());
        var scaleDown = new ScratchScaleDown(jobs, NullLogger<ScratchScaleDown>.Instance) { RetryDelay = TimeSpan.FromMilliseconds(20) };

        var start = scaleDown.StartAsync(CancellationToken.None);
        Assert.That(start.IsCompleted, Is.True, "Startup goes on while the store is asked");
        await UntilAsync(() => Task.FromResult(schedulers.ReceivedCalls().Count() >= 3), "a few tries");

        await scaleDown.StopAsync(CancellationToken.None);
        await Task.WhenAny(scaleDown.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.That(scaleDown.ExecuteTask.IsCanceled, Is.True, "Ended by the host stopping");
    }
}
