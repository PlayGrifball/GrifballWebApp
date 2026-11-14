using Microsoft.Extensions.DependencyInjection.Extensions;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.UserManagement;

namespace GrifballWebApp.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// The use cases and their settings. Their ports (the database, the outbox, the Halo client, the draft channel, the
    /// locks, the chat model, the queue's watchers) are the other layers' to register.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // The clock the services read (and the agents' get_current_time), unless a host or test set one first
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<UrlService>();
        // The chat the Discord bot and the site share (its model is Infrastructure's IChatClientFactory)
        services.AddTransient<GrifChat>();
        services.AddTransient<IQueueRepository, QueueRepository>();
        // Joining and leaving, from Discord or the site; the queue's size goes to its watchers (IQueueNotifier)
        services.AddTransient<QueueMembership>();

        // The historical import: queued by HistoricalImportController, run in the background by HistoricalImportWorker
        services.AddOptions<HistoricalImportOptions>().Bind(configuration.GetSection("HistoricalImport"));
        services.AddOptions<HistoricalImportWorkerOptions>().Bind(configuration.GetSection("HistoricalImport:Worker"));
        services.AddTransient<IHistoricalImportService, HistoricalImportService>();
        services.AddTransient<HistoricalImportJobs>();
        services.AddTransient<GamertagClaimService>();
        services.AddTransient<GamertagMatching>();
        // The cleanup of placeholders that are no league players (Infrastructure's XboxPlaceholderCleanupJob)
        services.AddTransient<XboxPlaceholderCleanup>();
        services.AddSingleton<HistoricalImportSignal>();
        services.AddOptions<HistoricalGuardrailOptions>().Bind(configuration.GetSection("HistoricalImport:Guardrail"));
        // VODs: found by the refresh jobs (no scheduler of their own), shown on the pages, reviewed by sysadmins
        services.AddTransient<VodService>();
        services.AddOptions<UserClaimOptions>().Bind(configuration.GetSection("UserClaims"));
        services.AddTransient<UserGamertagClaimService>();
        services.AddTransient<DuplicateSuggestionService>();

        services.AddTransient<UserMergeAuditService>();
        services.AddTransient<IBracketService, BracketService>();
        services.AddTransient<EventOrganizerService>();
        services.AddTransient<SignupsService>();
        services.AddTransient<SeasonStatsService>();
        services.AddTransient<MatchmakingStatsService>();
        services.AddTransient<ProfileStatsService>();
        services.AddTransient<LeaderboardsService>();
        services.AddTransient<TeamPageService>();
        services.AddTransient<TeamHistoryService>();
        services.AddTransient<DeploymentHistoryService>();
        services.AddTransient<ProfileCommendationsService>();
        services.AddTransient<SeasonService>();
        services.AddTransient<Commissioner.CommissionerDashboardService>();
        services.AddTransient<SeasonVisibility>();
        services.AddTransient<Home.HomeEventsService>();
        services.AddTransient<RegionService>();
        services.AddTransient<QuickSearchService>();
        services.AddTransient<TeamService>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        services.AddTransient<MatchPlannerService>();
        services.AddTransient<ChampionshipService>();
        services.AddTransient<SeasonMatchService>();
        services.AddTransient<MatchDetailsService>();
        services.AddTransient<XboxAccounts.XboxAccountService>();
        // The saved games, as /matches shows them and the agents' get_recent_games gives them
        services.AddTransient<Tools.HaloTools>();
        services.AddTransient<TeamStandingsService>();
        services.AddTransient<ISetGamertagService, SetGamertagService>();
        services.AddTransient<GradesService>();
        services.AddTransient<ScheduleService>();
        services.AddTransient<ScheduleRecommendations>();
        // How long a schedule recommendation may run (ScheduleRecommendationJob): 2000 generations or 2 minutes by default
        services.AddOptions<ScheduleRunOptions>().Bind(configuration.GetSection("ScheduleRecommendations"));
        services.AddTransient<AvailabilityService>();
        services.AddTransient<IUserMergeService, UserMergeService>();
        services.AddScoped<IDiscordAccountLinkService, DiscordAccountLinkService>();
        services.AddTransient<RescheduleService>();
        return services;
    }
}
