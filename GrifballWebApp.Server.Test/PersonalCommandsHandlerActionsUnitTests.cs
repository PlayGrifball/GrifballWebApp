using DiscordInterface.Generated;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// PersonalCommandsHandler's actions done, past the account gate PersonalCommandsHandlerUnitTests covers: on a temp-file
/// SQLite GrifballContext with the real services; the queue's store substituted (IQueueRepository), and the queue display's
/// refresh publishing to a substituted IPublisher.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PersonalCommandsHandlerActionsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private const ulong DiscordID = 42;

    private readonly IQueueRepository _queue = Substitute.For<IQueueRepository>();
    private readonly RecordingPublisher _publisher = new();
    private readonly FakeTimeProvider _time = new(Now);
    private ServiceProvider? _services;

    [TearDown]
    public async Task TearDownServices()
    {
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private PersonalCommandsHandler Handler()
    {
        _services = new ServiceCollection().AddSingleton<IPublisher>(_publisher).BuildServiceProvider();
        var membership = new QueueMembership(_queue, Db, Substitute.For<IQueueNotifier>());
        var buttons = new MatchmakingButtonHandler(membership, _services.GetRequiredService<IServiceScopeFactory>(), Db, Substitute.For<IDiscordRestClient>(), null!,
            new RecordingLogger<MatchmakingButtonHandler>());
        var seasons = new SeasonService(Db, null!, _time);
        return new PersonalCommandsHandler(new DiscordCaller(Db), new CommandLookups(Db, seasons), membership, _queue, buttons, null!,
            new UserGamertagClaimService(Db, new GamertagMatching(Db), _time, Options.Create(new UserClaimOptions())),
            new SignupsService(Db, _time), seasons, new AvailabilityService(Db), new RescheduleService(Db, Substitute.For<IOutbox>()), Urls());
    }

    private async Task<(CommandContextRecorder Discord, User Caller)> Caller(params string[] roles)
        => (new CommandContextRecorder(DiscordID), await AddUser(DiscordID, "Grifter1", roles));

    [Test]
    public async Task QueueStatus_APlayerNotQueued_IsToldSo()
    {
        _queue.PlayerStateByDiscord((long)DiscordID, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(1, true, false, false, 1));
        var discord = new CommandContextRecorder(DiscordID);

        await Handler().QueueStatus(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo("1 player is in the Late League queue.\nYou are not in the queue."));
    }

    [Test]
    public async Task QueueJoin_JoinsTheQueue_AndRefreshesItsDisplay()
    {
        var (discord, caller) = await Caller();
        _queue.PlayerState(caller.Id, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(caller.Id, true, false, false, 0));
        _queue.AddPlayerToQueue(Arg.Any<Domain.LateLeague.QueuedPlayer>(), Arg.Any<CancellationToken>()).Returns(true);

        await Handler().QueueJoin(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo(QueueMessages.For(QueueJoinResult.Joined)));
        await _queue.Received(1).AddPlayerToQueue(Arg.Is<Domain.LateLeague.QueuedPlayer>(x => x.UserID == caller.Id), Arg.Any<CancellationToken>());
        await _publisher.WaitForRefresh();
    }

    [Test]
    public async Task QueueJoin_AlreadyQueued_IsToldSo_AndAddsNothing()
    {
        var (discord, caller) = await Caller();
        _queue.PlayerState(caller.Id, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(caller.Id, true, true, false, 3));

        await Handler().QueueJoin(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo(QueueMessages.For(QueueJoinResult.AlreadyInQueue)));
        await _queue.DidNotReceiveWithAnyArgs().AddPlayerToQueue(default!);
    }

    [TestCase(true, QueueLeaveResult.Left)]
    [TestCase(false, QueueLeaveResult.NotInQueue)]
    public async Task QueueLeave_LeavesTheQueue_WhenIn(bool queued, QueueLeaveResult result)
    {
        var (discord, caller) = await Caller();
        _queue.RemovePlayerToQueue(caller.Id, Arg.Any<CancellationToken>()).Returns(queued);

        await Handler().QueueLeave(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo(QueueMessages.For(result)));
        await _queue.Received(1).RemovePlayerToQueue(caller.Id, Arg.Any<CancellationToken>());
        if (queued)
            await _publisher.WaitForRefresh();
    }

    [Test]
    public async Task Claim_NoGamertag_IsRefusedWithWhy()
    {
        var (discord, _) = await Caller();

        await Handler().Claim(discord.Context, "  ");

        Assert.That(discord.Content(), Is.EqualTo("Give the gamertag"));
    }

    [Test]
    public async Task Claim_APastGamertag_IsSubmitted_AndListedWithTheCallersClaims()
    {
        var (discord, caller) = await Caller();
        Db.Users.Add(User.ImportPlaceholder("historical:OldTag", "OldTag", null));
        await Db.SaveChangesAsync();
        var handler = Handler();
        var listed = new CommandContextRecorder(DiscordID);

        await handler.Claim(discord.Context, "OldTag");
        await handler.Claims(listed.Context);

        Db.ChangeTracker.Clear();
        var claim = await Db.GamertagClaims.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("Your claim of OldTag is pending: a sysadmin decides it."));
            Assert.That((claim.ClaimantUserID, claim.Gamertag), Is.EqualTo(((int?)caller.Id, "OldTag")));
            Assert.That(listed.Embed().Description, Is.EqualTo("OldTag · Pending"));
            Assert.That(listed.Embed().Url, Is.EqualTo($"https://grif.test/profile/{caller.Id}"));
        }
    }

    [Test]
    public async Task Logins_PointsAtTheProfilePage()
    {
        var (discord, caller) = await Caller();

        await Handler().Logins(discord.Context);

        var embed = discord.Embed();
        Assert.That((embed.Title, embed.Url, embed.Description), Is.EqualTo(("Your sign-ins", $"https://grif.test/profile/{caller.Id}", "Link or unlink sign-ins on your profile page.")));
    }

    private async Task<Season> SignupsOpenSeason()
    {
        var season = Season.Seed(seasonName: "Winter")
            .WithDates(signupsOpen: Now.UtcDateTime.AddDays(-1), signupsClose: Now.UtcDateTime.AddDays(5), draftStart: Now.UtcDateTime.AddDays(8),
                seasonStart: Now.UtcDateTime.AddDays(10), seasonEnd: Now.UtcDateTime.AddDays(60));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task Signup_SignsUp_ThenChangesOnlyWhatIsGiven()
    {
        var season = await SignupsOpenSeason();
        var (discord, caller) = await Caller(DiscordCaller.Roles.Player);
        var changed = new CommandContextRecorder(DiscordID);
        var handler = Handler();

        await handler.Signup(discord.Context, "Winter", "Grifs", willCaptain: true, needsDraftHelp: null);
        await handler.Signup(changed.Context, "Winter", null, willCaptain: null, needsDraftHelp: true);

        Db.ChangeTracker.Clear();
        var signup = await Db.SeasonSignups.SingleAsync();
        var form = $"[the signup form](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo($"You have signed up for Winter. Set the times you can play on {form}."));
            Assert.That(changed.Content(), Is.EqualTo($"Your signup for Winter is changed. Your times are on {form}."));
            Assert.That((signup.UserID, signup.TeamName, signup.WillCaptain, signup.RequiresAssistanceDrafting), Is.EqualTo((caller.Id, (string?)"Grifs", true, true)));
        }
    }

    [Test]
    public async Task AvailabilityShow_ListsTheSeasonsTimes_ByDay()
    {
        var season = await SignupsOpenSeason();
        var mondayLate = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(21, 0));
        var mondayEarly = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(20, 0));
        var friday = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Friday, time: new TimeOnly(19, 30));
        Db.Availability.AddRange(mondayLate, mondayEarly, friday);
        await Db.SaveChangesAsync();
        Db.Set<SeasonAvailability>().AddRange(
            SeasonAvailability.Seed(season.SeasonID, friday.AvailabilityOptionID),
            SeasonAvailability.Seed(season.SeasonID, mondayLate.AvailabilityOptionID),
            SeasonAvailability.Seed(season.SeasonID, mondayEarly.AvailabilityOptionID));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder(DiscordID);

        await Handler().AvailabilityShow(discord.Context, "Winter");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Winter availability"));
            Assert.That(embed.Description, Does.StartWith("**Monday**: 20:00, 21:00\n**Friday**: 19:30\n\nTimes are UTC."));
        }
    }

    [Test]
    public async Task RequestReschedule_ATimeItCantRead_PointsAtTheForm()
    {
        var season = await AddSeason("Winter");
        var match = await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);

        await Handler().RequestReschedule(discord.Context, match.SeasonMatchID.ToString(), "Away all week", "next tuesday-ish");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo(
                $"I couldn't read that time. Use UTC like 2026-11-20 20:00, or pick it on [the request form](https://grif.test/reschedule-request/{match.SeasonMatchID})."));
            Assert.That(await Db.MatchReschedules.AnyAsync(), Is.False);
        }
    }

    [TestCase("2026-11-20 20:00", ", for <t:1795204800>")]
    [TestCase(null, "")]
    public async Task RequestReschedule_IsRequested_WithTheTimeAskedFor(string? newTime, string forTime)
    {
        var season = await AddSeason("Winter");
        var match = await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
        var (discord, caller) = await Caller(DiscordCaller.Roles.Commissioner);

        await Handler().RequestReschedule(discord.Context, match.SeasonMatchID.ToString(), "Away all week", newTime);

        Db.ChangeTracker.Clear();
        var request = await Db.MatchReschedules.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo($"Your reschedule request is in{forTime}. A commissioner will decide it."));
            Assert.That((request.SeasonMatchID, request.RequestedByUserID, request.Reason), Is.EqualTo((match.SeasonMatchID, caller.Id, "Away all week")));
        }
    }
}
