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
            Assert.That(stranger.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
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

        Assert.That(_discord.Content(), Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.PlayerOrSysadmin)));
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

        // The pipeline replies with its message (GlobalExceptionHandler.Expected), as the site's 403 does
        Assert.That(async () => await _handler.RequestReschedule(_discord.Context, seeded.Match.SeasonMatchID.ToString(), "Just because", null),
            Throws.TypeOf<RescheduleForbiddenException>());
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

    [Test]
    public async Task QueueStatus_Should_SayTheCallerIsNotInIt()
    {
        await _handler.QueueStatus(_discord.Context);

        Assert.That(_discord.Content(), Is.EqualTo("0 players are in the Late League queue.\nYou are not in the queue."));
    }

    [Test]
    public async Task Signup_Should_PassOnTheRefusals()
    {
        await GivePlayerRole(_me);
        var now = DateTime.UtcNow;
        var closed = Season.Seed(seasonName: "Closed").WithDates(signupsOpen: now.AddDays(-20), signupsClose: now.AddDays(-10), draftStart: now.AddDays(-9),
            seasonStart: now.AddDays(-8), seasonEnd: now.AddDays(40));
        var open = Season.Seed(seasonName: "Open Too").WithDates(signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(5), draftStart: now.AddDays(6),
            seasonStart: now.AddDays(7), seasonEnd: now.AddDays(40));
        _context.Seasons.AddRange(closed, open);
        await _context.SaveChangesAsync();
        var tooLong = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);
        var unknown = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.Signup(unknown.Context, "999999", null, false, false);

        using (Assert.EnterMultipleScope())
        {
            // The service's refusals reach the pipeline, which replies with their message (GlobalExceptionHandler.Expected)
            Assert.That(async () => await _handler.Signup(_discord.Context, closed.SeasonID.ToString(), null, false, false), Throws.TypeOf<SignupsClosedException>());
            Assert.That(async () => await _handler.Signup(tooLong.Context, open.SeasonID.ToString(), new string('x', 500), true, false),
                Throws.TypeOf<SignupTeamNameTooLongException>());
            Assert.That(unknown.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
        }
    }

    [Test]
    public async Task UnknownSeasonOrMatch_Should_BeSaidSo()
    {
        var availability = new CommandContextRecorder();
        var reschedule = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.AvailabilityShow(availability.Context, "999999");
        await _handler.RequestReschedule(reschedule.Context, "999999", "Busy", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(availability.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
            Assert.That(reschedule.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
        }
    }

    [Test]
    public async Task QueueStatus_Should_SayTheCallerIsInAMatch()
    {
        var other = await _context.AddUser("opponent", gamertag: "OpponentGT");
        _context.MatchedMatches.Add(MatchedMatch.Start(new MatchedTeam([new MatchedPlayer(_me.Id)]), new MatchedTeam([new MatchedPlayer(other.Id)])));
        await _context.SaveChangesAsync();

        await _handler.QueueStatus(_discord.Context);

        Assert.That(_discord.Content(), Does.EndWith("You are in a Late League match."));
    }

    private async Task AddPlaceholder(string gamertag, bool played)
    {
        var placeholder = User.Seed(userName: $"historical:{gamertag}", normalizedUserName: $"HISTORICAL:{gamertag.ToUpperInvariant()}", displayName: gamertag, isDummyUser: true);
        _context.Users.Add(placeholder);
        await _context.SaveChangesAsync();
        if (!played)
            return;
        var season = Season.Seed(seasonName: "Winter League 2019", historicalSourceID: "wl19/Stats")
            .WithDates(seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.Add(season);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(season.SeasonID, placeholder.Id, 1, new GrifballWebApp.Domain.Shared.StatSheetTotals { Gamertag = gamertag, Kills = 7 }));
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Claim_Should_SubmitIt_AndClaims_Should_ListEachWithItsStatusAndReason()
    {
        await AddPlaceholder("Old Me", played: true);
        await AddPlaceholder("Older Me", played: false);
        var second = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);
        var list = new CommandContextRecorder((ulong)_me.DiscordUserID!.Value);

        await _handler.Claim(_discord.Context, "Old Me");
        await _handler.Claim(second.Context, "Older Me");
        var older = await _context.GamertagClaims.SingleAsync(x => x.Gamertag == "Older Me");
        await new GrifballWebApp.Application.Historical.GamertagClaimService(_context, new GrifballWebApp.Application.Profile.UserMergeService(_context))
            .Reject(older.GamertagClaimID, "Not you");
        _context.ChangeTracker.Clear();
        await _handler.Claims(list.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.Content(), Is.EqualTo("Your claim of Old Me is pending: a sysadmin decides it. It covers Winter League 2019."));
            Assert.That(second.Content(), Is.EqualTo("Your claim of Older Me is pending: a sysadmin decides it."));
            Assert.That(list.Embed().Description, Does.Contain("Old Me · Pending").And.Contain("Older Me · Rejected · Not you"));
        }
    }
}
