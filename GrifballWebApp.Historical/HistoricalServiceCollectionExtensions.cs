using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Historical.Worker;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Historical;

public static class HistoricalServiceCollectionExtensions
{
    /// <summary>
    /// The historical import: queued by HistoricalImportController, run in the background by
    /// <see cref="HistoricalImportWorker"/>; its sources downloaded and snapshotted by the worker (refresh jobs, by hand
    /// and on <see cref="HistoricalRefreshJob"/>'s schedule, which <see cref="AddHistoricalJobs"/> registers).
    /// </summary>
    public static IServiceCollection AddHistoricalImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HistoricalImportOptions>().Bind(configuration.GetSection("HistoricalImport"));
        services.AddOptions<HistoricalImportWorkerOptions>().Bind(configuration.GetSection("HistoricalImport:Worker"));
        services.AddOptions<HistoricalGuardrailOptions>().Bind(configuration.GetSection("HistoricalImport:Guardrail"));
        services.AddTransient<IHistoricalImportService, HistoricalImportService>();
        services.AddTransient<HistoricalImportJobs>();
        services.AddSingleton<HistoricalImportSignal>();
        services.AddHostedService<HistoricalImportWorker>();
        services.AddHttpClient<HistoricalSourceFetcher>(HistoricalSourceFetcher.Configure);
        services.AddTransient<HistoricalSourceService>();
        services.AddTransient<HistoricalImportRunner>();
        services.AddTransient<HistoricalImportSchedule>();
        return services;
    }

    /// <summary>
    /// The import's recurring job, for AddInfrastructure's jobs, with the names Quartz stored its type under before it
    /// moved here (from the Server, then from Infrastructure), for a rolling update's older pods.
    /// </summary>
    public static void AddHistoricalJobs(RecurringJobs jobs)
    {
        jobs.Add<HistoricalRefreshJob>(HistoricalRefreshJob.Definition);
        jobs.Renamed("GrifballWebApp.Server.Historical.HistoricalRefreshJob, GrifballWebApp.Server", typeof(HistoricalRefreshJob));
        jobs.Renamed("GrifballWebApp.Infrastructure.Historical.HistoricalRefreshJob, GrifballWebApp.Infrastructure", typeof(HistoricalRefreshJob));
    }
}
