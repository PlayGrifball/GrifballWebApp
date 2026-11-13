using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Home;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Application.Test;

/// <summary>AddApplication: every use case registered with its implementation and lifetime, and its settings bound to
/// their configuration sections.</summary>
[TestFixture]
internal sealed class ApplicationServiceCollectionExtensionsTests
{
    private static IEnumerable<TestCaseData> Registrations()
    {
        static TestCaseData Of(Type service, Type implementation, ServiceLifetime lifetime)
            => new TestCaseData(service, implementation, lifetime).SetArgDisplayNames(service.Name);

        yield return Of(typeof(UrlService), typeof(UrlService), ServiceLifetime.Transient);
        yield return Of(typeof(IQueueRepository), typeof(QueueRepository), ServiceLifetime.Transient);
        yield return Of(typeof(QueueMembership), typeof(QueueMembership), ServiceLifetime.Transient);
        yield return Of(typeof(IHistoricalImportService), typeof(HistoricalImportService), ServiceLifetime.Transient);
        yield return Of(typeof(HistoricalImportJobs), typeof(HistoricalImportJobs), ServiceLifetime.Transient);
        yield return Of(typeof(GamertagClaimService), typeof(GamertagClaimService), ServiceLifetime.Transient);
        yield return Of(typeof(GamertagMatching), typeof(GamertagMatching), ServiceLifetime.Transient);
        yield return Of(typeof(HistoricalImportSignal), typeof(HistoricalImportSignal), ServiceLifetime.Singleton);
        yield return Of(typeof(VodService), typeof(VodService), ServiceLifetime.Transient);
        yield return Of(typeof(UserGamertagClaimService), typeof(UserGamertagClaimService), ServiceLifetime.Transient);
        yield return Of(typeof(DuplicateSuggestionService), typeof(DuplicateSuggestionService), ServiceLifetime.Transient);
        yield return Of(typeof(UserMergeAuditService), typeof(UserMergeAuditService), ServiceLifetime.Transient);
        yield return Of(typeof(IBracketService), typeof(BracketService), ServiceLifetime.Transient);
        yield return Of(typeof(EventOrganizerService), typeof(EventOrganizerService), ServiceLifetime.Transient);
        yield return Of(typeof(SignupsService), typeof(SignupsService), ServiceLifetime.Transient);
        yield return Of(typeof(SeasonStatsService), typeof(SeasonStatsService), ServiceLifetime.Transient);
        yield return Of(typeof(MatchmakingStatsService), typeof(MatchmakingStatsService), ServiceLifetime.Transient);
        yield return Of(typeof(ProfileStatsService), typeof(ProfileStatsService), ServiceLifetime.Transient);
        yield return Of(typeof(LeaderboardsService), typeof(LeaderboardsService), ServiceLifetime.Transient);
        yield return Of(typeof(TeamPageService), typeof(TeamPageService), ServiceLifetime.Transient);
        yield return Of(typeof(TeamHistoryService), typeof(TeamHistoryService), ServiceLifetime.Transient);
        yield return Of(typeof(DeploymentHistoryService), typeof(DeploymentHistoryService), ServiceLifetime.Transient);
        yield return Of(typeof(ProfileCommendationsService), typeof(ProfileCommendationsService), ServiceLifetime.Transient);
        yield return Of(typeof(SeasonService), typeof(SeasonService), ServiceLifetime.Transient);
        yield return Of(typeof(HomeEventsService), typeof(HomeEventsService), ServiceLifetime.Transient);
        yield return Of(typeof(RegionService), typeof(RegionService), ServiceLifetime.Transient);
        yield return Of(typeof(QuickSearchService), typeof(QuickSearchService), ServiceLifetime.Transient);
        yield return Of(typeof(TeamService), typeof(TeamService), ServiceLifetime.Transient);
        yield return Of(typeof(IUserManagementService), typeof(UserManagementService), ServiceLifetime.Transient);
        yield return Of(typeof(MatchPlannerService), typeof(MatchPlannerService), ServiceLifetime.Transient);
        yield return Of(typeof(ChampionshipService), typeof(ChampionshipService), ServiceLifetime.Transient);
        yield return Of(typeof(SeasonMatchService), typeof(SeasonMatchService), ServiceLifetime.Transient);
        yield return Of(typeof(MatchDetailsService), typeof(MatchDetailsService), ServiceLifetime.Transient);
        yield return Of(typeof(TeamStandingsService), typeof(TeamStandingsService), ServiceLifetime.Transient);
        yield return Of(typeof(ISetGamertagService), typeof(SetGamertagService), ServiceLifetime.Transient);
        yield return Of(typeof(GradesService), typeof(GradesService), ServiceLifetime.Transient);
        yield return Of(typeof(ScheduleService), typeof(ScheduleService), ServiceLifetime.Transient);
        yield return Of(typeof(ScheduleRecommendations), typeof(ScheduleRecommendations), ServiceLifetime.Transient);
        yield return Of(typeof(AvailabilityService), typeof(AvailabilityService), ServiceLifetime.Transient);
        yield return Of(typeof(IUserMergeService), typeof(UserMergeService), ServiceLifetime.Transient);
        yield return Of(typeof(IDiscordAccountLinkService), typeof(DiscordAccountLinkService), ServiceLifetime.Scoped);
        yield return Of(typeof(RescheduleService), typeof(RescheduleService), ServiceLifetime.Transient);
    }

    private static IServiceCollection AddApplication(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build();
        return new ServiceCollection().AddApplication(configuration);
    }

    [TestCaseSource(nameof(Registrations))]
    public void AddApplication_RegistersTheUseCase(Type service, Type implementation, ServiceLifetime lifetime)
    {
        var descriptor = AddApplication().Single(d => d.ServiceType == service);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(descriptor.ImplementationType, Is.EqualTo(implementation));
            Assert.That(descriptor.Lifetime, Is.EqualTo(lifetime));
        }
    }

    [Test]
    public void AddApplication_ReturnsTheSameCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddApplication(new ConfigurationBuilder().Build());

        Assert.That(returned, Is.SameAs(services));
    }

    [Test]
    public void AddApplication_BindsTheScheduleRecommendationsSection()
    {
        using var provider = AddApplication(new Dictionary<string, string?>
        {
            ["ScheduleRecommendations:MaxGenerations"] = "50",
            ["ScheduleRecommendations:TimeBudget"] = "00:00:30",
        }).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ScheduleRunOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.MaxGenerations, Is.EqualTo(50));
            Assert.That(options.TimeBudget, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }
    }

    [Test]
    public void AddApplication_NoSettings_KeepsTheDefaults()
    {
        using var provider = AddApplication().BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ScheduleRunOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.MaxGenerations, Is.EqualTo(2000));
            Assert.That(options.TimeBudget, Is.EqualTo(TimeSpan.FromMinutes(2)));
        }
    }

    [Test]
    public void AddApplication_BindsEachSettingsClassToItsOwnSection()
    {
        // One key under each class's section, a value none of the defaults has: a class bound to another section (or a
        // misspelled one) keeps its default and fails here
        using var provider = AddApplication(new Dictionary<string, string?>
        {
            ["HistoricalImport:MatchPullAttempts"] = "7",
            ["HistoricalImport:Worker:ReportLinesKept"] = "42",
            ["HistoricalImport:Guardrail:MaxRemovedFraction"] = "0.75",
            ["UserClaims:SubmissionsPerDay"] = "9",
        }).BuildServiceProvider();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.GetRequiredService<IOptions<HistoricalImportOptions>>().Value.MatchPullAttempts, Is.EqualTo(7));
            Assert.That(provider.GetRequiredService<IOptions<HistoricalImportWorkerOptions>>().Value.ReportLinesKept, Is.EqualTo(42));
            Assert.That(provider.GetRequiredService<IOptions<HistoricalGuardrailOptions>>().Value.MaxRemovedFraction, Is.EqualTo(0.75m));
            Assert.That(provider.GetRequiredService<IOptions<UserClaimOptions>>().Value.SubmissionsPerDay, Is.EqualTo(9));
        }
    }
}
