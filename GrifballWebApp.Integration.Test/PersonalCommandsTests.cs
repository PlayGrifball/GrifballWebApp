using GrifballWebApp.Application;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Integration.Test;

/// <summary>/queue, /me, /signup, /availability and /reschedule request (<see cref="PersonalCommandsHandler"/>).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PersonalCommandsTests
{
    private GrifballContext _context = null!;
    private IPublisher _publisher = null!;
    private PersonalCommandsHandler _handler = null!;
    private User _me = null!;
    private CommandContextRecorder _discord = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        var seasons = new SeasonService(_context, new SeasonStatsService(_context));
        var queue = new QueueRepository(_context);
        _publisher = Substitute.For<IPublisher>();
        var buttons = new MatchmakingButtonHandler(null!, new ServiceCollection().AddScoped(_ => _publisher).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            null!, null!, null!, NullLogger<MatchmakingButtonHandler>.Instance);
        var claims = new UserGamertagClaimService(_context, new GamertagMatching(_context), TimeProvider.System, Options.Create(new UserClaimOptions()));
        _handler = new PersonalCommandsHandler(new DiscordCaller(_context), new CommandLookups(_context, seasons), new QueueMembership(queue, _context, Substitute.For<IQueueNotifier>()),
            queue, buttons, new RegionService(_context), claims, new SignupsService(_context), seasons, new AvailabilityService(_context),
            new RescheduleService(_context, new ContextOutbox(_context)), urls);

        _me = await _context.AddUser("me", gamertag: "MeGT", discordName: "me");
        _discord = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task QueueJoinThenLeave_Should_GoThroughQueueMembership_AndRefreshTheDisplay()
    {
        var leave = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);
        var status = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.QueueJoin(_discord.Context);
        await _handler.QueueStatus(status.Context);
        await _handler.QueueLeave(leave.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.DeferredPublicly(), Is.False);
            Assert.That(_discord.Content(), Is.EqualTo(QueueMessages.For(QueueJoinResult.Joined)));
            Assert.That(status.Content(), Is.EqualTo("1 player is in the Late League queue.\nYou are in the queue."));
            Assert.That(leave.Content(), Is.EqualTo(QueueMessages.For(QueueLeaveResult.Left)));
            Assert.That(await _context.QueuedPlayer.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task QueueJoin_Should_OfferTheSetGamertagButton_When_TheCallerHasNoAccount()
    {
        var stranger = new CommandContextRecorder(discordUserId: 999);

        await _handler.QueueJoin(stranger.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stranger.Message().Content, Is.EqualTo(DiscordCaller.NoGamertag));
            Assert.That(await _context.QueuedPlayer.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task QueueStatus_Should_NeedNoAccount()
    {
        var stranger = new CommandContextRecorder(discordUserId: 999);

        await _handler.QueueStatus(stranger.Context);

        Assert.That(stranger.Content(), Is.EqualTo("0 players are in the Late League queue."));
    }

    [Test]
    public async Task SetRegion_Should_SetItByName_AndClearItWithNone()
    {
        var clear = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.SetRegion(_discord.Context, "central north america");
        var set = await _context.Users.AsNoTracking().Where(x => x.Id == _me.Id).Select(x => x.RegionID).SingleAsync();
        await _handler.SetRegion(clear.Context, "none");
        var cleared = await _context.Users.AsNoTracking().Where(x => x.Id == _me.Id).Select(x => x.RegionID).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.Content(), Is.EqualTo("Your region is set."));
            Assert.That(set, Is.EqualTo(Regions.CentralNorthAmerica));
            Assert.That(clear.Content(), Is.EqualTo("Your region is cleared."));
            Assert.That(cleared, Is.Null);
        }
    }

    [Test]
    public async Task SetRegion_Should_ListTheRegions_When_ItIsUnknown()
    {
        await _handler.SetRegion(_discord.Context, "Atlantis");

        Assert.That(_discord.Content(), Does.StartWith(PersonalCommandsHandler.NoSuchRegion).And.Contain("Central North America"));
    }

    [Test]
    public async Task ClaimAndClaims_Should_ReportTheServicesAnswers()
    {
        var claims = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);
        var logins = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.Claim(_discord.Context, "MeGT");
        await _handler.Claims(claims.Context);
        await _handler.Logins(logins.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.Content(), Is.Not.Empty);
            Assert.That(claims.Embed().Url, Is.EqualTo($"https://grif.test/profile/{_me.Id}"));
            Assert.That(logins.Embed().Description, Is.EqualTo("Link or unlink sign-ins on your profile page."));
        }
    }

    [Test]
    public async Task Signup_Should_SignTheCallerUp_ThenKeepTheirTimes_When_TheyChangeIt()
    {
        var now = DateTime.UtcNow;
        var season = Season.Seed(seasonName: "Open").WithDates(signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(5), draftStart: now.AddDays(6),
            seasonStart: now.AddDays(7), seasonEnd: now.AddDays(40));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        await GivePlayerRole(_me);
        var change = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.Signup(_discord.Context, season.SeasonID.ToString(), null, willCaptain: false, needsDraftHelp: false);
        await _handler.Signup(change.Context, season.SeasonID.ToString(), "The Hammers", willCaptain: true, needsDraftHelp: false);

        var signup = await _context.SeasonSignups.AsNoTracking().SingleAsync(x => x.SeasonID == season.SeasonID && x.UserID == _me.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.Content(), Does.StartWith("You have signed up for Open."));
            Assert.That(change.Content(), Does.StartWith("Your signup for Open is changed."));
            Assert.That(signup.WillCaptain, Is.True);
            Assert.That(signup.TeamName, Is.EqualTo("The Hammers"));
        }
    }

    [Test]
    public async Task Signup_Should_NeedThePlayerRole()
    {
        await _handler.Signup(_discord.Context, null, null, false, false);

        Assert.That(_discord.Message().Content, Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.PlayerOrSysadmin)));
    }

    [Test]
    public async Task AvailabilityShow_Should_ListTheSeasonsTimesByDay_InUtc()
    {
        var season = await _context.AddSeason("Timed");
        var option = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));
        _context.Availability.Add(option);
        await _context.SaveChangesAsync();
        _context.Set<SeasonAvailability>().Add(SeasonAvailability.Seed(seasonID: season.SeasonID, availabilityOptionID: option.AvailabilityOptionID));
        await _context.SaveChangesAsync();
        var stranger = new CommandContextRecorder(discordUserId: 999);

        await _handler.AvailabilityShow(stranger.Context, season.SeasonID.ToString());

        Assert.That(stranger.Embed().Description, Does.StartWith("**Monday**: 18:00").And.Contain("Times are UTC."));
    }

    [Test]
    public async Task RequestReschedule_Should_FileTheRequest_ForAPlayerOnTheMatch()
    {
        var seeded = await RescheduleTestData.SeedAsync(_context, scheduled: DateTime.UtcNow.AddDays(3));
        var requester = new CommandContextRecorder((ulong)seeded.Requester.DiscordUserID!.Value);

        await _handler.RequestReschedule(requester.Context, seeded.Match.SeasonMatchID.ToString(), "Out of town", "2030-01-02 20:00");

        var reschedule = await _context.MatchReschedules.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requester.Content(), Does.StartWith("Your reschedule request is in, for <t:"));
            Assert.That(reschedule.NewScheduledTime, Is.EqualTo(new DateTime(2030, 1, 2, 20, 0, 0, DateTimeKind.Utc)));
        }
    }

    [Test]
    public async Task RequestReschedule_Should_PassOnTheServicesRefusal_ForSomeoneNotOnTheMatch()
    {
        var seeded = await RescheduleTestData.SeedAsync(_context, scheduled: DateTime.UtcNow.AddDays(3));

        await _handler.RequestReschedule(_discord.Context, seeded.Match.SeasonMatchID.ToString(), "Just because", null);

        Assert.That(_discord.Content(), Is.EqualTo(new RescheduleForbiddenException().Message));
    }

    [Test]
    public async Task RequestReschedule_Should_AskForAReadableTime()
    {
        var seeded = await RescheduleTestData.SeedAsync(_context, scheduled: DateTime.UtcNow.AddDays(3));

        await _handler.RequestReschedule(_discord.Context, seeded.Match.SeasonMatchID.ToString(), "Busy", "next tuesday-ish");

        Assert.That(_discord.Content(), Does.StartWith("I couldn't read that time."));
    }

    private async Task GivePlayerRole(User user)
    {
        var role = await _context.Roles.SingleAsync(x => x.Name == "Player");
        _context.UserRoles.Add(new GrifballWebApp.Domain.Players.UserRole { UserId = user.Id, RoleId = role.Id });
        await _context.SaveChangesAsync();
    }
}
