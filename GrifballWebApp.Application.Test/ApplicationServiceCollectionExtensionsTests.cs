using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Calendar;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.UserManagement;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;

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
        yield return Of(typeof(GamertagClaimService), typeof(GamertagClaimService), ServiceLifetime.Transient);
        yield return Of(typeof(GamertagMatching), typeof(GamertagMatching), ServiceLifetime.Transient);
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
        yield return Of(typeof(MatchCalendarService), typeof(MatchCalendarService), ServiceLifetime.Transient);
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
        yield return Of(typeof(GrifChat), typeof(GrifChat), ServiceLifetime.Transient);
        yield return Of(typeof(ChatAccounts), typeof(ChatAccounts), ServiceLifetime.Transient);
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

    /// <summary>
    /// Every registration AddApplication makes can be built, its dependencies included, given what the host and
    /// Infrastructure register beside it (the ports below, substituted): a use case whose new constructor parameter nothing
    /// registers fails here, whether or not Registrations lists it.
    /// </summary>
    [Test]
    public void AddApplication_EveryRegistrationResolves_GivenTheHostsAndInfrastructuresServices()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddApplication(configuration);
        // The host's
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddTransient<HttpClient>();
        services.AddTransient<YouTubeClient>();
        services.AddScoped(_ => Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null!, null!, null!, null!, null!, new IdentityErrorDescriber(), null!, null!));
        // Infrastructure's ports
        services.AddScoped(_ => Substitute.For<IApplicationDbContext>());
        services.AddScoped(_ => Substitute.For<IOutbox>());
        services.AddScoped(_ => Substitute.For<IDataPullService>());
        services.AddScoped(_ => Substitute.For<IGetsertXboxUserService>());
        services.AddScoped(_ => Substitute.For<IPublisher>());
        services.AddScoped(_ => Substitute.For<IDatabaseLocks>());
        services.AddScoped(_ => Substitute.For<ICurrentUserService>());
        services.AddSingleton(_ => Substitute.For<IDistributedCache>());
        services.AddSingleton(_ => Substitute.For<IChatClientFactory>());

        Assert.That(() => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }).Dispose(), Throws.Nothing);
    }

    [TestCase(null, 10)]
    [TestCase("2.5", 2.5)]
    public void AddApplication_TheChatsMonthlyLimit_IsChatMonthlyLimit(string? setting, decimal limit)
    {
        using var provider = AddApplication(new() { [ChatLimits.MonthlyLimitKey] = setting }).BuildServiceProvider();

        Assert.That(provider.GetRequiredService<ChatLimits>().MonthlyLimit, Is.EqualTo(limit));
    }

    [TestCase(null, 1)]
    [TestCase("3", 3)]
    public void AddApplication_TheChatsLimits_OneProcessWide_AsManyRepliesAtOnceAsChatMaxRepliesSays(string? setting, int replies)
    {
        using var provider = AddApplication(new() { [ChatLimits.MaxRepliesKey] = setting }).BuildServiceProvider();

        var limits = provider.GetRequiredService<ChatLimits>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limits.MaxReplies, Is.EqualTo(replies));
            Assert.That(provider.GetRequiredService<ChatLimits>(), Is.SameAs(limits));
        }
    }

    [TestCase(null, false, false)]
    [TestCase("false", false, false)]
    [TestCase("true", true, false)]
    [TestCase("True", true, false)]
    // Not a boolean: off, with a warning, not a chat that can't start
    [TestCase("1", false, true)]
    [TestCase("yes", false, true)]
    public void AddApplication_TheChatsModel_OneProcessWide_WhatPeopleWroteInItsTelemetryOnlyWhenSwitchedOn(string? setting, bool sensitiveData, bool warned)
    {
        var services = AddApplication(new() { [ChatModel.SensitiveDataKey] = setting });
        services.AddFakeLogging().AddSingleton(_ => Substitute.For<IChatClientFactory>());
        using var provider = services.BuildServiceProvider();

        var model = provider.GetRequiredService<ChatModel>();

        var warnings = provider.GetFakeLogCollector().GetSnapshot().Where(r => r.Level == LogLevel.Warning).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.SensitiveData, Is.EqualTo(sensitiveData));
            Assert.That(provider.GetRequiredService<ChatModel>(), Is.SameAs(model));
            Assert.That(warnings, Has.Count.EqualTo(warned ? 1 : 0));
            Assert.That(warnings.Select(r => r.Message), Has.All.Contains(ChatModel.SensitiveDataKey));
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
            ["UserClaims:SubmissionsPerDay"] = "9",
        }).BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IOptions<UserClaimOptions>>().Value.SubmissionsPerDay, Is.EqualTo(9));
    }
}
