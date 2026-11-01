using GrifballWebApp.Database;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace GrifballWebApp.Server.Jobs;

/// <summary>
/// Quartz.NET, for the scheduled and one-off background jobs: its job store is in the app's database (the AddQuartz
/// migration made the tables, on each provider), so the jobs, their schedules and their history are shared by every
/// pod. Only the worker roles start the scheduler (<see cref="Hosting.AppRole.WorkerHostedServices"/>); an API pod
/// builds it in standby, which reads and changes the store (the Jobs page, "run now") without firing anything.
/// </summary>
public static class JobsServiceCollectionExtensions
{
    /// <summary>The execution history kept, in the database: a week, at most this many runs per job (failures not counted).</summary>
    public static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(7);
    public const int HistoryPerJob = 200;
    public const int HistoryPerScheduler = 5000;

    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration, Action<RecurringJobs> recurring)
    {
        var jobs = new RecurringJobs();
        recurring(jobs);
        services.AddSingleton(jobs);
        services.TryAddSingleton(TimeProvider.System);

        var provider = configuration.GetDatabaseProvider();
        var connectionString = configuration.GetConnectionString("GrifballWebApp") ?? throw new Exception("GrifballContext failed to configure");

        services.AddQuartz(q =>
        {
            // A run started from an API pod is a trigger in the database, which the worker only sees when it next looks
            // (30 s when idle by default): look every 5 s, so "run now" on the Jobs page runs within a few seconds.
            q.ConfigureScheduler(scheduler => scheduler.IdleWaitTime = TimeSpan.FromSeconds(5));
            q.UsePersistentStore(store =>
            {
                _ = provider switch
                {
                    DatabaseProvider.SqlServer => store.UseSqlServer(connectionString),
                    DatabaseProvider.Postgres => store.UsePostgres(connectionString),
                    DatabaseProvider.Sqlite => store.UseSqlite(connectionString),
                    _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
                };
                store.UseSystemTextJsonSerializer();
                store.UseExecutionHistory();
                // Several pods share the store: clustered, each trigger fires on one of them, a rolling deploy's two
                // workers never run a job twice, and a worker that dies has its runs recovered. SQLite locks in
                // process and can't be clustered (Quartz refuses it), but it is one pod anyway (docs/sqlite.md).
                if (provider != DatabaseProvider.Sqlite)
                    store.UseClustering();
            });

            foreach (var (type, job) in jobs.All)
            {
                q.AddJob(type, j => j
                    .WithIdentity(job.Key)
                    .WithDescription(job.Description)
                    // Durable: the job stays while its trigger is removed (turned off), so the Jobs page still lists it
                    .StoreDurably()
                    // A slow run delays the next instead of overlapping it, on every pod
                    .DisallowConcurrentExecution());
            }
        });

        services.AddQuartzExecutionHistory(options =>
        {
            options.Retention = HistoryRetention;
            options.MaxEntriesPerJob = HistoryPerJob;
            options.MaxEntriesPerScheduler = HistoryPerScheduler;
        });

        // In this order: the seeder's StartAsync makes the missing triggers before Quartz's starts the scheduler.
        services.AddHostedService<RecurringJobSeeder>();
        services.AddQuartzHostedService(options =>
        {
            // A deploy lets a running job finish (within the host's shutdown timeout) instead of abandoning it
            options.WaitForJobsToComplete = true;
            // Firing starts once the host has started, not while the other hosted services are still starting
            options.AwaitApplicationStarted = true;
        });
        return services;
    }
}
