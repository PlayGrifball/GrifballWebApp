using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Events;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Ollama;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using GrifballWebApp.Infrastructure.Scheduler;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Infrastructure.Teams;
using GrifballWebApp.Infrastructure.UserManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// The database: the context (its saves audited and the outbox signalled), its factory, and the use cases' view of it
    /// (<see cref="IApplicationDbContext"/>, the scope's context).
    /// </summary>
    public static IServiceCollection AddGrifballDatabase(this IServiceCollection services)
    {
        // The audit interceptor's user: the request's, else the ambient one (a job's, a Discord interaction's)
        services.AddHttpContextAccessor();
        services.AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<GrifballContext>((provider, options) =>
        {
            options.UseGrifballDatabase(provider.GetRequiredService<IConfiguration>())
                .AddInterceptors(provider.GetRequiredService<AuditInterceptor>(),
                    provider.GetRequiredService<OutboxSignalInterceptor>());
        });

        // Its own IDbContextFactory<GrifballContext>: the default one conflicts with the scoped context
        services.AddDbContextFactory<GrifballContext, GrifballContextFactory>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<GrifballContext>());
        return services;
    }

    /// <summary>
    /// Discord's side of the app that isn't its gateway: its settings (validated; a preview without a bot gets a stand-in
    /// client and Discord off), the draft's on-deck messages, the matchmaking queue, and the outbox that sends what the
    /// use cases queue (OutboxDispatcher, which skips it all while Discord is off).
    /// </summary>
    public static IServiceCollection AddDiscordInfrastructure(this IServiceCollection services, IConfiguration configuration, bool discordOff)
    {
        if (discordOff)
            services.AddPreviewDiscord(configuration);
        else
            services.AddValidatedDiscordOptions(configuration);

        services.AddTransient<DiscordOnDeckMessages>();
        services.AddTransient<QueueService>();
        services.AddDiscordOutbox(configuration);
        services.AddMatchmakingOutboxHandlers();
        services.AddTransient<EventsService>();
        return services;
    }

    /// <summary>
    /// <see cref="DiscordOptions"/> from the Discord section, checked at startup: the bot's sign-in and its draft channel
    /// are required.
    /// </summary>
    public static IServiceCollection AddValidatedDiscordOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordOptions>()
            .Bind(configuration.GetSection("Discord"))
            .Validate(options =>
            {
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(options.ClientId))
                    errors.Add("ClientId is required");
                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                    errors.Add("ClientSecret is required");

                if (string.IsNullOrWhiteSpace(options.Token))
                    errors.Add("Token is required");
                if (options.DraftChannel is 0)
                    errors.Add("DraftChannel is required");

                if (errors.Count > 0)
                    throw new ArgumentException(string.Join(", ", errors));

                return true;
            })
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// The adapters behind the use cases' ports (the Halo client's pulls, the draft channel, the database locks, the chat
    /// model), the historical archive's sources and workers, the XUID lookups, the jobs (Quartz: the recurring ones, the
    /// ones a page starts, and the scheduler), the sheet export and the database backups.
    /// </summary>
    /// <param name="discordOff">Discord is off (a preview without a bot): its jobs are turned off.</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool discordOff)
    {
        services.AddTransient<IDataPullService, DataPullService>();
        // One for the app: every pull shares the gateway key's quota (HaloGateway:PermitsPerMinute), and scaled out
        // (ConnectionStrings:Redis) every pod's pulls share it, through Redis
        services.AddHaloCallPacer(configuration);
        services.AddHostedService<HistoricalImportWorker>();
        // The archive's sources: downloaded and snapshotted by the worker (manual refresh jobs and the schedule), parsed in .NET
        services.AddHttpClient<HistoricalSourceFetcher>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.MaxResponseContentBufferSize = 64 * 1024 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GrifballWebApp/1.0 (historical archive)");
        });
        services.AddTransient<HistoricalSourceService>();
        services.AddTransient<HistoricalImportRunner>();
        services.AddTransient<HistoricalImportSchedule>();
        // The VODs' feeds (VodService)
        services.AddOptions<YouTubeOptions>().Bind(configuration.GetSection("YouTube"));
        services.AddHttpClient<YouTubeClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(1);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GrifballWebApp/1.0 (VODs)");
        });
        services.AddOptions<XuidLookupOptions>().Bind(configuration.GetSection("XuidLookup"));
        services.AddTransient<XuidLookupService>();
        services.AddSingleton<XuidLookupSignal>();
        services.AddHostedService<XuidLookupWorker>();
        // Quartz.NET: the recurring jobs (their schedules, kept in the database, and the Jobs page) and the scheduler that runs them
        services.AddJobs(configuration, jobs =>
        {
            jobs.Add<HistoricalRefreshJob>(HistoricalRefreshJob.Definition);
            jobs.Add<XboxPlaceholderJob>(XboxPlaceholderJob.Definition);
            jobs.Add<ActiveSeasonPullJob>(ActiveSeasonPullJob.Definition);
            jobs.Add<MatchVariantBackfillJob>(MatchVariantBackfillJob.Definition);
            jobs.Add<PasswordResetLinkCleanupJob>(PasswordResetLinkCleanupJob.Definition);
            // Discord's: off while Discord is (a preview without a bot)
            jobs.Add<MatchmakingQueueJob>(MatchmakingQueueJob.Definition(enabled: !discordOff));
            jobs.Add<SeasonEventsJob>(SeasonEventsJob.Definition(enabled: !discordOff));
            // Started from a page, with an input (OnDemandJobs)
            jobs.AddOnDemand<SeasonMatchPullJob>("season-match-pull", SeasonMatchPullJob.Description);
            jobs.AddOnDemand<ScheduleRecommendationJob>("schedule-recommendation", ScheduleRecommendationJob.Description);
            jobs.AddOnDemand<SheetExportJob>("sheet-export", SheetExportJob.Description);
            jobs.AddOnDemand<DatabaseBackupJob>("database-backup", DatabaseBackupJob.Description);
            jobs.AddOnDemand<DatabaseExportJob>("database-export", DatabaseExportJob.Description);
            // Queued as a worker starts (ScratchScaleDown), with an on-demand scratch server only
            ScratchStopJobRegistration.AddScratchStop(jobs, configuration);
        });

        services.AddTransient<IDraftChannel, OutboxDraftChannel>();
        services.AddTransient<IDatabaseLocks, ContextDatabaseLocks>();
        services.AddTransient<ExcelService>();
        services.AddDatabaseBackups(configuration);
        services.AddTransient<GetsertXboxUserService>();
        // A preview's smoke players get made-up gamertags; everything else is looked up on Halo
        if (PreviewMode.IsEnabled(configuration))
            services.AddTransient<IGetsertXboxUserService, PreviewSmokeXboxUsers>();
        else
            services.AddTransient<IGetsertXboxUserService, GetsertXboxUserService>();
        services.AddSingleton<IBotChatClients, OllamaChatClients>();
        return services;
    }

    /// <summary>
    /// The Database Backup page's backup and export: run as jobs (BackupJobs); the made file stays on the pod that made it,
    /// and an API pod fetches it from there (DatabaseBackup:NodeUrl, set by the chart on a worker, recorded with the run).
    /// </summary>
    private static void AddDatabaseBackups(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<BackupFiles>();
        services.AddTransient<BackupRuns>();
        services.AddTransient<BackupDownloads>();
        // No overall timeout (a backup can be large, and streams for as long as it takes), but a worker that's gone is
        // given up on quickly.
        services.AddHttpClient(BackupDownloads.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) });
        // The scratch server started on demand (the chart's backupScratch.mode onDemand), and stopped at startup: by a job
        // queued then (ScratchStopJob), in the backups' execution group, so never under a backup on another worker.
        if (configuration["DatabaseBackup:Scratch:Deployment"] is { Length: > 0 } scratchDeployment)
        {
            services.AddSingleton<IScratchScaler>(provider => KubernetesScratchScaler.InCluster(
                scratchDeployment, provider.GetRequiredService<ILogger<KubernetesScratchScaler>>()));
            services.AddHostedService<ScratchScaleDown>();
        }
    }
}
