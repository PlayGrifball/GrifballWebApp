using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Teams;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The high review of PR #390 (#271), one test per finding: each failed before its fix. Rebuilding a season's matches or
/// bracket is the site's API too (MatchPlannerService, BracketService), not only Discord's.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommandReviewFindingsTests
{
    private GrifballContext _context = null!;
    private UrlService _urls = null!;
    private SeasonService _seasons = null!;
    private CommandLookups _lookups = null!;
    private BracketService _brackets = null!;
    private User _commissioner = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        _seasons = new SeasonService(_context, new SeasonStatsService(_context), new GrifballWebApp.Application.Seasons.SeasonVisibility(_context));
        _lookups = new CommandLookups(_context, _seasons, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(_context), new GrifballWebApp.Application.Seasons.SeasonVisibility(_context));
        _brackets = new BracketService(_context, new TeamStandingsService(_context), new ChampionshipService(_context));
        _commissioner = await _context.AddUser("commish", gamertag: "CommishGT", discordName: "commish", commissioner: true);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static CommandContextRecorder As(User user) => new((ulong)user.DiscordUserID!.Value);

    private AdminCommandsHandler Admin() => new(new DiscordCaller(_context), _lookups, new RescheduleService(_context, new ContextOutbox(_context)),
        new SeasonMatchService(_context, Substitute.For<IDataPullService>(), _brackets, new ChampionshipService(_context)), new MatchPlannerService(_context), _brackets,
        new TeamService(_context, Substitute.For<IPublisher>(), new OutboxDraftChannel(_context, new ContextOutbox(_context)), new ContextDatabaseLocks(_context)),
        new ScheduleRecommendations(_context, TimeProvider.System), onDemand: null!, jobs: null!, new GamertagClaimService(_context, new UserMergeService(_context)),
        Substitute.For<IDataPullService>(), _urls);

    private PersonalCommandsHandler Personal()
    {
        var queue = new QueueRepository(_context);
        var buttons = new MatchmakingButtonHandler(null!, new ServiceCollection().AddScoped(_ => Substitute.For<IPublisher>()).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            null!, null!, null!, NullLogger<MatchmakingButtonHandler>.Instance);
        return new PersonalCommandsHandler(new DiscordCaller(_context), _lookups, new QueueMembership(queue, _context, Substitute.For<IQueueNotifier>()), queue, buttons,
            new RegionService(_context), new UserGamertagClaimService(_context, new GamertagMatching(_context), TimeProvider.System, Options.Create(new UserClaimOptions())),
            new SignupsService(_context), _seasons, new AvailabilityService(_context), new RescheduleService(_context, new ContextOutbox(_context)), _urls);
    }

    private async Task<(Season Season, Team Home, Team Away)> SeasonWithTeams(string name)
    {
        var season = await _context.AddSeason(name);
        var home = Team.Seed(seasonID: season.SeasonID, teamName: $"{name} Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: $"{name} Away");
        _context.Teams.AddRange(home, away);
        await _context.SaveChangesAsync();
        return (season, home, away);
    }

    private async Task PlayerRole(User user)
    {
        var role = await _context.Roles.SingleAsync(x => x.Name == "Player");
        _context.UserRoles.Add(new GrifballWebApp.Domain.Players.UserRole { UserId = user.Id, RoleId = role.Id });
        await _context.SaveChangesAsync();
    }

    private async Task Forfeit(SeasonMatch match)
        => await new SeasonMatchService(_context, Substitute.For<IDataPullService>(), _brackets, new ChampionshipService(_context)).HomeForfeit(match.SeasonMatchID);

    // Finding 1 (the site's API too): making the season's matches again deleted the ones already played, results and all

    [Test]
    public async Task CreateSeasonMatches_Should_Refuse_When_AMatchHasBeenPlayed()
    {
        var (season, home, away) = await SeasonWithTeams("Played");
        var played = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
        _context.SeasonMatches.Add(played);
        await _context.SaveChangesAsync();
        await Forfeit(played);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await new MatchPlannerService(_context).CreateSeasonMatches(season.SeasonID, 1, 3), Throws.TypeOf<SeasonRefusedException>());
            Assert.That(await _context.SeasonMatches.AnyAsync(x => x.SeasonMatchID == played.SeasonMatchID), Is.True, "the played match is kept");
        }
    }

    [Test]
    public async Task CreateSeasonMatches_Should_StillReplace_MatchesNotPlayed()
    {
        var (season, home, away) = await SeasonWithTeams("Unplayed");
        _context.SeasonMatches.Add(SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID));
        await _context.SaveChangesAsync();

        await new MatchPlannerService(_context).CreateSeasonMatches(season.SeasonID, 1, 3);

        Assert.That(await _context.SeasonMatches.CountAsync(x => x.SeasonID == season.SeasonID), Is.EqualTo(2));
    }

    [Test]
    public async Task CreateBracket_Should_Refuse_When_ABracketMatchHasBeenPlayed()
    {
        var (season, _, _) = await SeasonWithTeams("Bracketed");
        await _brackets.CreateBracket(2, season.SeasonID, false, 3);
        await _brackets.SetSeeds(season.SeasonID);
        var final = await _context.SeasonMatches.SingleAsync(x => x.SeasonID == season.SeasonID && x.BracketMatch != null);
        await Forfeit(final);

        Assert.That(async () => await _brackets.CreateBracket(4, season.SeasonID, false, 3), Throws.TypeOf<SeasonRefusedException>());
    }

    [Test]
    public async Task CreateBracket_Should_StillReplace_ABracketWithNothingPlayed()
    {
        var (season, _, _) = await SeasonWithTeams("Redrawn");
        await _brackets.CreateBracket(4, season.SeasonID, false, 3);

        await _brackets.CreateBracket(2, season.SeasonID, false, 3);

        Assert.That(await _context.SeasonMatches.CountAsync(x => x.SeasonID == season.SeasonID && x.BracketMatch != null), Is.EqualTo(1));
    }

    // Finding 2: /signup used to change one thing wiped the rest (it is checked through NetCord in DiscordTransportTests,
    // where an option left out is really left out)

    // Finding 3: pre-season commands defaulted to the season running now, not the one signing up

    [Test]
    public async Task Signup_Should_DefaultToTheSeasonSigningUp_NotTheOneRunning()
    {
        var now = DateTime.UtcNow;
        await _context.AddCurrentSeason("Running");
        var next = Season.Seed(seasonName: "Signing Up").WithDates(signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(5), draftStart: now.AddDays(6),
            seasonStart: now.AddDays(40), seasonEnd: now.AddDays(80), publicAt: now.AddDays(-2));
        _context.Seasons.Add(next);
        await _context.SaveChangesAsync();
        var player = await _context.AddUser("signer", gamertag: "SignerGT", discordName: "signer");
        await PlayerRole(player);

        await Personal().Signup(As(player).Context, null, null, false, false);

        Assert.That(await _context.SeasonSignups.AnyAsync(x => x.SeasonID == next.SeasonID && x.UserID == player.Id), Is.True);
    }

    // Finding 4: a team name typed for a match picked one of its matches quietly

    [Test]
    public async Task Forfeit_Should_Refuse_When_ATypedNameFitsSeveralMatches()
    {
        var (season, home, away) = await SeasonWithTeams("Bombers");
        var first = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
        var second = SeasonMatch.Seed(season.SeasonID, away.TeamID, home.TeamID);
        _context.SeasonMatches.AddRange(first, second);
        await _context.SaveChangesAsync();
        var discord = As(_commissioner);

        await Admin().Forfeit(discord.Context, "Bombers Home", ForfeitSide.Home);

        Assert.That(await _context.SeasonMatches.CountAsync(x => x.SeasonID == season.SeasonID && x.HomeTeamResult != null), Is.Zero, "no match was forfeited");
    }

    // Finding 5: a name of digits was taken for an ID

    [Test]
    public async Task Team_Should_FindATeamNamedWithDigits_ByItsName()
    {
        var (season, other, _) = await SeasonWithTeams("Digits");
        var named = Team.Seed(seasonID: season.SeasonID, teamName: other.TeamID.ToString());
        _context.Teams.Add(named);
        await _context.SaveChangesAsync();

        var found = await _lookups.Team(other.TeamID.ToString());

        Assert.That(found?.TeamID, Is.EqualTo(named.TeamID));
    }

    // Finding 6: text added after a full list went past Discord's 4096 characters

    [Test]
    public async Task Schedule_Should_StayWithinDiscordsLimit_WithALongScheduleAndUnscheduledMatches()
    {
        // Each line is "<t:unix> · [home vs away](https://grif.test/seasonmatch/NN)": 104 characters with its newline, so 39 lines
        // fill the list to its 4056 (two-digit match IDs: nine other matches come first); "…and 11 more" and the note have no room
        var filler = await _context.AddSeason("Filler");
        for (var i = 0; i < 9; i++)
            _context.SeasonMatches.Add(SeasonMatch.Seed(filler.SeasonID, null, null));
        var season = await _context.AddSeason("Long Schedule");
        var teams = new[] { Team.Seed(seasonID: season.SeasonID, teamName: new string('A', 23)), Team.Seed(seasonID: season.SeasonID, teamName: new string('B', 23)) };
        _context.Teams.AddRange(teams);
        await _context.SaveChangesAsync();
        for (var i = 0; i < 50; i++)
            _context.SeasonMatches.Add(SeasonMatch.Seed(season.SeasonID, teams[0].TeamID, teams[1].TeamID, scheduledTime: DateTime.UtcNow.AddDays(i + 1)));
        _context.SeasonMatches.Add(SeasonMatch.Seed(season.SeasonID, teams[1].TeamID, teams[0].TeamID));
        await _context.SaveChangesAsync();
        var discord = new CommandContextRecorder();
        var handler = new SeasonCommandsHandler(_lookups, _seasons, new TeamStandingsService(_context), new MatchPlannerService(_context),
            new GradesService(_context), new SignupsService(_context), _brackets, _urls);

        await handler.Schedule(discord.Context, season.SeasonID.ToString());

        Assert.That(discord.Embed().Description, Has.Length.LessThanOrEqualTo(4096));
    }

    // Finding 7: the search embed's title took the whole query

    [Test]
    public async Task Search_Should_KeepItsTitleWithinDiscordsLimit()
    {
        var stats = new SeasonStatsService(_context);
        var lateLeague = new MatchmakingStatsService(_context);
        var browse = new BrowseCommandsHandler(new LeaderboardsService(_context, stats, lateLeague,
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<LeaderboardsService>.Instance),
            lateLeague, new HomeEventsService(_context, new GrifballWebApp.Application.Seasons.SeasonVisibility(_context)), new QuickSearchService(_context, new GrifballWebApp.Application.Seasons.SeasonVisibility(_context)), _lookups, _urls);
        var discord = new CommandContextRecorder();

        await browse.Search(discord.Context, new string('q', 300));

        Assert.That(discord.Embed().Title, Has.Length.LessThanOrEqualTo(256));
    }

    // Finding 8: the account was looked up before Discord was answered (it must be within 3 seconds)

    [Test]
    public async Task StaffAndPersonalCommands_Should_DeferBeforeLookingUpTheCaller()
    {
        var admin = As(_commissioner);
        var stranger = new CommandContextRecorder(discordUserId: 424242);

        await Admin().Dashboard(admin.Context);
        await Personal().QueueJoin(stranger.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(admin.Callbacks[0].Type, Is.EqualTo(NetCord.Rest.InteractionCallbackType.DeferredMessage));
            Assert.That(stranger.Callbacks[0].Type, Is.EqualTo(NetCord.Rest.InteractionCallbackType.DeferredMessage));
        }
    }
}
