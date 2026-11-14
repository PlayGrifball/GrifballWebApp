using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Teams;
using GrifballWebApp.Server.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// /admin and /reschedule pending|decide (<see cref="AdminCommandsHandler"/>): each held to its endpoint's role, every reply
/// ephemeral. The job-backed commands (pull, schedule run, jobs) run through the real scheduler in <see cref="DiscordTransportTests"/>.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AdminCommandsTests
{
    private GrifballContext _context = null!;
    private IDataPullService _dataPull = null!;
    private AdminCommandsHandler _handler = null!;
    private User _commissioner = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        var seasons = new SeasonService(_context, new SeasonStatsService(_context), new GrifballWebApp.Application.Seasons.SeasonVisibility(_context));
        _dataPull = Substitute.For<IDataPullService>();
        var brackets = new BracketService(_context, new TeamStandingsService(_context), new ChampionshipService(_context));
        var teams = new TeamService(_context, Substitute.For<IPublisher>(), new OutboxDraftChannel(_context, new ContextOutbox(_context)), new ContextDatabaseLocks(_context));
        _handler = new AdminCommandsHandler(new DiscordCaller(_context), new CommandLookups(_context, seasons, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(_context), new GrifballWebApp.Application.Seasons.SeasonVisibility(_context)), new RescheduleService(_context, new ContextOutbox(_context)),
            new SeasonMatchService(_context, _dataPull, brackets, new ChampionshipService(_context)), new MatchPlannerService(_context), brackets, teams,
            new ScheduleRecommendations(_context, TimeProvider.System), onDemand: null!, jobs: null!, new GamertagClaimService(_context, new UserMergeService(_context)),
            _dataPull, urls);
        _commissioner = await _context.AddUser("commish", gamertag: "CommishGT", discordName: "commish", commissioner: true);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static CommandContextRecorder As(User user) => new((ulong)user.DiscordUserID!.Value);

    private async Task<User> Sysadmin()
    {
        var user = await _context.AddUser("sysadmin", gamertag: "SysGT", discordName: "sysadmin");
        var role = await _context.Roles.SingleAsync(x => x.Name == "Sysadmin");
        _context.UserRoles.Add(new GrifballWebApp.Domain.Players.UserRole { UserId = user.Id, RoleId = role.Id });
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<SeasonMatch> AddMatch(DateTime? scheduled = null)
    {
        var season = await _context.AddSeason("Admin Season");
        var home = await _context.AddTeamWithCaptain(season, "AdminHome", 7_000_001);
        var away = await _context.AddTeamWithCaptain(season, "AdminAway", 7_000_002);
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, scheduledTime: scheduled);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    [Test]
    public async Task Dashboard_Should_RefuseAPlayer_Privately()
    {
        var player = await _context.AddUser("player", gamertag: "PlayerGT", discordName: "player");
        var discord = As(player);

        await _handler.Dashboard(discord.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.False);
            Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.Staff)));
        }
    }

    [Test]
    public async Task StaffLists_Should_ReplyEphemerally_ToACommissioner()
    {
        var dashboard = As(_commissioner);
        var overdue = As(_commissioner);
        var pending = As(_commissioner);

        await _handler.Dashboard(dashboard.Context);
        await _handler.Overdue(overdue.Context);
        await _handler.PendingReschedules(pending.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dashboard.DeferredPublicly(), Is.False);
            Assert.That(dashboard.Embed().Description, Is.EqualTo("0 pending reschedules · 0 overdue matches · 0 over 3 days"));
            Assert.That(overdue.Embed().Description, Is.EqualTo("No match is overdue."));
            Assert.That(pending.Embed().Description, Is.EqualTo("No reschedule is pending."));
        }
    }

    [Test]
    public async Task DecideReschedule_Should_SaySo_When_ThereIsNoSuchRequest()
    {
        var discord = As(_commissioner);

        await _handler.DecideReschedule(discord.Context, 424242, approve: true, notes: null);

        Assert.That(discord.Content(), Is.EqualTo("Reschedule request not found"));
    }

    [Test]
    public async Task MatchActions_Should_BeTheCommissionersAlone()
    {
        var sysadmin = As(await Sysadmin());

        await _handler.Forfeit(sysadmin.Context, "1", ForfeitSide.Home);

        Assert.That(sysadmin.Content(), Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.CommissionerOnly)));
    }

    [Test]
    public async Task ForfeitThenUndo_Should_ChangeTheMatchsResult()
    {
        var match = await AddMatch();
        var forfeit = As(_commissioner);
        var undo = As(_commissioner);

        await _handler.Forfeit(forfeit.Context, match.SeasonMatchID.ToString(), ForfeitSide.Away);
        var forfeited = await _context.SeasonMatches.AsNoTracking().SingleAsync(x => x.SeasonMatchID == match.SeasonMatchID);
        await _handler.UndoResult(undo.Context, match.SeasonMatchID.ToString());
        var undone = await _context.SeasonMatches.AsNoTracking().SingleAsync(x => x.SeasonMatchID == match.SeasonMatchID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forfeit.Content(), Is.EqualTo("Forfeited (away)."));
            Assert.That(forfeited.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(undo.Content(), Is.EqualTo("The result is undone."));
            Assert.That(undone.AwayTeamResult, Is.Null);
        }
    }

    [Test]
    public async Task UndoResult_Should_PassOnTheRefusal_When_ThereIsNoResult()
    {
        var match = await AddMatch();
        var discord = As(_commissioner);

        // The pipeline replies with its message (GlobalExceptionHandler.Expected), as the site's 400 does
        Assert.That(async () => await _handler.UndoResult(discord.Context, match.SeasonMatchID.ToString()),
            Throws.TypeOf<SeasonMatchActionNotAllowedException>().With.Message.EqualTo("This match has no result to undo"));
    }

    [Test]
    public async Task MatchActions_Should_SayThereIsNoSuchMatchOrGame()
    {
        var match = await AddMatch();
        var noMatch = As(_commissioner);
        var badGame = As(_commissioner);

        await _handler.ReportGame(noMatch.Context, "999999", Guid.NewGuid().ToString(), null);
        await _handler.ReportGame(badGame.Context, match.SeasonMatchID.ToString(), "nope", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noMatch.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
            Assert.That(badGame.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
        }
    }

    [Test]
    public async Task SetTime_Should_ScheduleTheMatch_InUtc()
    {
        var match = await AddMatch();
        var unreadable = As(_commissioner);
        var discord = As(_commissioner);

        await _handler.SetTime(unreadable.Context, match.SeasonMatchID.ToString(), "soon");
        await _handler.SetTime(discord.Context, match.SeasonMatchID.ToString(), "2030-05-06 21:30");

        var scheduled = await _context.SeasonMatches.AsNoTracking().Where(x => x.SeasonMatchID == match.SeasonMatchID).Select(x => x.ScheduledTime).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(unreadable.Content(), Does.StartWith("I couldn't read that time."));
            Assert.That(discord.Content(), Does.StartWith("The match is set for <t:"));
            Assert.That(scheduled, Is.EqualTo(new DateTime(2030, 5, 6, 21, 30, 0, DateTimeKind.Utc)));
        }
    }

    [Test]
    public async Task SeasonSetup_Should_RefuseBadNumbers_AndLockCaptains()
    {
        var season = await _context.AddSeason("Setup");
        var generate = As(_commissioner);
        var bracket = As(_commissioner);
        var @lock = As(_commissioner);

        await _handler.GenerateMatches(generate.Context, season.SeasonID.ToString(), 0, 3);
        await _handler.CreateBracket(bracket.Context, season.SeasonID.ToString(), 0, false, 3);
        await _handler.LockCaptains(@lock.Context, season.SeasonID.ToString(), @lock: true);

        var locked = await _context.Seasons.AsNoTracking().Where(x => x.SeasonID == season.SeasonID).Select(x => x.CaptainsLocked).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(generate.Content(), Is.EqualTo("Home matches per team and best of must be at least 1."));
            Assert.That(bracket.Content(), Is.EqualTo("A bracket needs participants."));
            Assert.That(@lock.Content(), Is.EqualTo("The captains are locked."));
            Assert.That(locked, Is.True);
        }
    }

    [Test]
    public async Task GenerateMatches_Should_SayThereIsNoSuchSeason_When_ItIsUnknown()
    {
        var discord = As(_commissioner);

        await _handler.GenerateMatches(discord.Context, "999999", 1, 3);

        Assert.That(discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task ScheduleApply_Should_SaySo_When_ThereIsNoSuchRecommendation()
    {
        var discord = As(_commissioner);

        await _handler.ScheduleApply(discord.Context, 999999);

        Assert.That(discord.Content(), Is.EqualTo("That recommendation doesn't exist or was applied already."));
    }

    [Test]
    public async Task SysadminCommands_Should_RefuseACommissioner()
    {
        var discord = As(_commissioner);

        await _handler.RefreshMedals(discord.Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.SysadminOnly)));
            await _dataPull.DidNotReceive().DownloadMedals();
        }
    }

    [Test]
    public async Task SysadminCommands_Should_RunForASysadmin()
    {
        var sysadmin = await Sysadmin();
        var medals = As(sysadmin);
        var pull = As(sysadmin);
        var badPull = As(sysadmin);
        var claims = As(sysadmin);
        var badJob = As(sysadmin);
        var game = Guid.NewGuid();

        await _handler.RefreshMedals(medals.Context);
        await _handler.HaloPull(pull.Context, game.ToString());
        await _handler.HaloPull(badPull.Context, "not-a-guid");
        await _handler.Claims(claims.Context);
        await _handler.Job(badJob.Context, "no-slash", pause: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(medals.Content(), Is.EqualTo("The medals are refreshed."));
            await _dataPull.Received(1).DownloadMedals();
            Assert.That(pull.Content(), Does.Contain($"https://grif.test/match/{game}"));
            await _dataPull.Received(1).GetAndSaveMatch(game);
            Assert.That(badPull.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
            Assert.That(claims.Embed().Description, Is.EqualTo("No claim is pending."));
            Assert.That(badJob.Content(), Does.StartWith("Name the job as group/name"));
        }
    }

    [Test]
    public async Task Overdue_Should_ListAMatchPastItsTime_ByItsCaptains()
    {
        var match = await AddMatch(DateTime.UtcNow.AddDays(-3));
        var discord = As(_commissioner);

        await _handler.Overdue(discord.Context);

        Assert.That(discord.Embed().Description, Does.Contain($"https://grif.test/seasonmatch/{match.SeasonMatchID}").And.Contain("hours overdue"));
    }

    [Test]
    public async Task PendingThenDecide_Should_ListTheRequest_ThenApproveIt()
    {
        var seeded = await RescheduleTestData.SeedAsync(_context, scheduled: DateTime.UtcNow.AddDays(3));
        var request = await new RescheduleService(_context, new ContextOutbox(_context)).RequestRescheduleAsync(
            new RescheduleRequestDto { SeasonMatchID = seeded.Match.SeasonMatchID, Reason = "Travel", NewScheduledTime = DateTime.UtcNow.AddDays(5) }, seeded.Requester.Id);
        var pending = As(_commissioner);
        var decide = As(_commissioner);

        await _handler.PendingReschedules(pending.Context);
        await _handler.DecideReschedule(decide.Context, request.MatchRescheduleID, approve: true, notes: "Fine");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pending.Embed().Description, Does.StartWith($"**#{request.MatchRescheduleID}**").And.Contain("Travel"));
            Assert.That(decide.Content(), Is.EqualTo("Approved."));
        }
    }

    [TestCase(ForfeitSide.Home, "Forfeited (home).")]
    [TestCase(ForfeitSide.Both, "Forfeited (both).")]
    public async Task Forfeit_Should_ForfeitTheSideNamed(ForfeitSide side, string reply)
    {
        var match = await AddMatch();
        var discord = As(_commissioner);

        await _handler.Forfeit(discord.Context, match.SeasonMatchID.ToString(), side);

        Assert.That(discord.Content(), Is.EqualTo(reply));
    }

    [Test]
    public async Task SeasonSetup_Should_MakeTheMatchesAndTheBracket()
    {
        var season = await _context.AddSeason("Built");
        await _context.AddTeamWithCaptain(season, "BuiltA", 7_100_001);
        await _context.AddTeamWithCaptain(season, "BuiltB", 7_100_002);
        var generate = As(_commissioner);
        var bracket = As(_commissioner);
        var seed = As(_commissioner);
        var id = season.SeasonID.ToString();

        await _handler.GenerateMatches(generate.Context, id, 1, 3);
        await _handler.CreateBracket(bracket.Context, id, 2, false, 3);
        await _handler.SeedBracket(seed.Context, id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generate.Content(), Does.StartWith("The matches are made."));
            Assert.That(bracket.Content(), Does.StartWith("The bracket is made."));
            Assert.That(seed.Content(), Is.EqualTo("The bracket is seeded by the standings."));
            Assert.That(await _context.SeasonMatches.CountAsync(x => x.SeasonID == season.SeasonID), Is.GreaterThan(1));
        }
    }

    [Test]
    public async Task DecideClaim_Should_PassOnTheServicesRefusal_ForAClaimThatIsGone()
    {
        var sysadmin = await Sysadmin();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await _handler.DecideClaim(As(sysadmin).Context, 999999, approve: true, reason: null), Throws.TypeOf<GamertagClaimException>());
            Assert.That(async () => await _handler.DecideClaim(As(sysadmin).Context, 999999, approve: false, reason: "No"), Throws.TypeOf<GamertagClaimException>());
        }
    }

    [TestCase("report")]
    [TestCase("forfeit")]
    [TestCase("undo")]
    [TestCase("time")]
    public async Task MatchActions_Should_SayThereIsNoSuchMatch_ToStaff(string action)
    {
        var discord = As(_commissioner);
        Func<Task> run = action switch
        {
            "report" => () => _handler.ReportGame(discord.Context, "999999", Guid.NewGuid().ToString(), null),
            "forfeit" => () => _handler.Forfeit(discord.Context, "999999", ForfeitSide.Home),
            "undo" => () => _handler.UndoResult(discord.Context, "999999"),
            _ => () => _handler.SetTime(discord.Context, "999999", "2030-01-01 20:00"),
        };

        await run();

        Assert.That(discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
    }

    [TestCase("bracket")]
    [TestCase("seed")]
    [TestCase("lock")]
    [TestCase("show")]
    [TestCase("run")]
    public async Task SeasonActions_Should_SayThereIsNoSuchSeason_ToStaff(string action)
    {
        var discord = As(_commissioner);
        Func<Task> run = action switch
        {
            "bracket" => () => _handler.CreateBracket(discord.Context, "999999", 4, false, 3),
            "seed" => () => _handler.SeedBracket(discord.Context, "999999"),
            "lock" => () => _handler.LockCaptains(discord.Context, "999999", @lock: false),
            "show" => () => _handler.ScheduleShow(discord.Context, "999999"),
            _ => () => _handler.ScheduleRun(discord.Context, "999999"),
        };

        await run();

        Assert.That(discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task DecideReschedule_Should_RejectARequest()
    {
        var seeded = await RescheduleTestData.SeedAsync(_context, scheduled: DateTime.UtcNow.AddDays(3));
        var request = await new RescheduleService(_context, new ContextOutbox(_context)).RequestRescheduleAsync(
            new RescheduleRequestDto { SeasonMatchID = seeded.Match.SeasonMatchID, Reason = "Travel" }, seeded.Requester.Id);
        var pending = As(_commissioner);
        var decide = As(_commissioner);

        await _handler.PendingReschedules(pending.Context);
        await _handler.DecideReschedule(decide.Context, request.MatchRescheduleID, approve: false, notes: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pending.Embed().Description, Does.Contain("no time given"));
            Assert.That(decide.Content(), Is.EqualTo("Rejected."));
        }
    }

    [Test]
    public async Task UnlockCaptains_Should_UnlockThem()
    {
        var season = await _context.AddSeason("Locked", captainsLocked: true);
        var discord = As(_commissioner);

        await _handler.LockCaptains(discord.Context, season.SeasonID.ToString(), @lock: false);

        Assert.That(discord.Content(), Is.EqualTo("The captains are unlocked."));
    }

    [Test]
    public async Task ClaimsAndDecideClaim_Should_ListThePendingClaims_ThenRejectOneAndApproveTheOther()
    {
        var sysadmin = await Sysadmin();
        var claimant = await _context.AddUser("claimant", gamertag: "NowGT", discordName: "claimant");
        _context.Users.AddRange(
            User.Seed(userName: "historical:Old GT", normalizedUserName: "HISTORICAL:OLD GT", displayName: "Old GT", isDummyUser: true),
            User.Seed(userName: "historical:Other GT", normalizedUserName: "HISTORICAL:OTHER GT", displayName: "Other GT", isDummyUser: true));
        await _context.SaveChangesAsync();
        var claims = new UserGamertagClaimService(_context, new GamertagMatching(_context), TimeProvider.System, Microsoft.Extensions.Options.Options.Create(new UserClaimOptions()));
        var claim = await claims.Submit(claimant.Id, new SubmitGamertagClaimRequest { Gamertag = "Old GT" });
        var other = await claims.Submit(claimant.Id, new SubmitGamertagClaimRequest { Gamertag = "Other GT" });
        var list = As(sysadmin);
        var reject = As(sysadmin);
        var approve = As(sysadmin);

        await _handler.Claims(list.Context);
        await _handler.DecideClaim(reject.Context, claim.GamertagClaimID, approve: false, reason: "Not them");
        await _handler.DecideClaim(approve.Context, other.GamertagClaimID, approve: true, reason: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list.Embed().Description, Does.Contain($"**#{claim.GamertagClaimID}** Old GT").And.Contain($"**#{other.GamertagClaimID}** Other GT"));
            Assert.That(reject.Content(), Is.EqualTo("Rejected."));
            Assert.That(approve.Content(), Is.EqualTo("Approved."));
        }
    }

    [Test]
    public async Task ReportGame_Should_RefuseAnEmptyGameID_AndPassAGameOnToTheService()
    {
        var match = await AddMatch();
        var empty = As(_commissioner);

        await _handler.ReportGame(empty.Context, match.SeasonMatchID.ToString(), Guid.Empty.ToString(), null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
            // A game Halo doesn't have: the service's own error, after it tried to pull it
            Assert.That(async () => await _handler.ReportGame(As(_commissioner).Context, match.SeasonMatchID.ToString(), Guid.NewGuid().ToString(), null), Throws.Exception);
        }
        await _dataPull.ReceivedWithAnyArgs(1).GetAndSaveMatch(default);
    }
}
