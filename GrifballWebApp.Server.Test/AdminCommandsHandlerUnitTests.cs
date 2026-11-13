using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Server.Commands;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// AdminCommandsHandler's unit tests: the staff gate (<see cref="DiscordCaller"/>) and lookups (<see cref="CommandLookups"/>)
/// are real, on a temp-file SQLite GrifballContext (#402). <see cref="Handler"/> leaves the deeper services null, for the
/// gate and the validation every method does before reaching them (as the site's own 401/400 would); <see cref="DeepHandler"/>
/// makes the IApplicationDbContext-backed ones real too (plus the kept <see cref="_dataPullService"/>/<see cref="_bracketService"/>
/// substitutes, asserted on below), for the actions themselves.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AdminCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    // Kept so DeepHandler's tests can assert what the handler called them with, not just the reply.
    private readonly IDataPullService _dataPullService = Substitute.For<IDataPullService>();
    private readonly IBracketService _bracketService = Substitute.For<IBracketService>();

    private AdminCommandsHandler Handler() => new(
        new DiscordCaller(Db), new CommandLookups(Db, null!),
        null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, Urls());

    /// <summary>The deeper services real too (IApplicationDbContext-backed ones), where an action goes past the gate
    /// without needing Quartz (OnDemandJobs, JobsService) or a draft channel/locks' real behavior.</summary>
    private AdminCommandsHandler DeepHandler() => new(
        new DiscordCaller(Db), new CommandLookups(Db, null!),
        new RescheduleService(Db, Substitute.For<IOutbox>()),
        new SeasonMatchService(Db, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(Db)),
        new MatchPlannerService(Db),
        _bracketService,
        new TeamService(Db, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), null!),
        new ScheduleRecommendations(Db, TimeProvider.System),
        null!, null!,
        new GamertagClaimService(Db, Substitute.For<IUserMergeService>()),
        _dataPullService,
        Urls());

    private async Task AssertRefusesUnknownCaller(Func<AdminCommandsHandler, CommandContextRecorder, Task> call)
    {
        var discord = new CommandContextRecorder(discordUserId: 909090);

        await call(Handler(), discord);

        Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
    }

    private async Task<CommandContextRecorder> AuthorizedCaller(params string[] roles)
    {
        await AddUser(discordUserId: 42, gamertag: "Staffer", roles: roles);
        return new CommandContextRecorder(discordUserId: 42);
    }

    [Test]
    public Task Dashboard_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Dashboard(d.Context));

    [Test]
    public Task Overdue_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Overdue(d.Context));

    [Test]
    public Task PendingReschedules_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.PendingReschedules(d.Context));

    [Test]
    public Task DecideReschedule_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.DecideReschedule(d.Context, 1, true, null));

    [Test]
    public Task ReportGame_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.ReportGame(d.Context, "match", Guid.NewGuid().ToString(), null));

    [Test]
    public Task Forfeit_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Forfeit(d.Context, "match", ForfeitSide.Home));

    [Test]
    public Task UndoResult_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.UndoResult(d.Context, "match"));

    [Test]
    public Task PullGames_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.PullGames(d.Context, "match"));

    [Test]
    public Task SetTime_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.SetTime(d.Context, "match", "2026-11-20 20:00"));

    [Test]
    public Task GenerateMatches_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.GenerateMatches(d.Context, "season", 1, 3));

    [Test]
    public Task CreateBracket_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.CreateBracket(d.Context, "season", 8, false, 3));

    [Test]
    public Task SeedBracket_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.SeedBracket(d.Context, "season"));

    [Test]
    public Task LockCaptains_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.LockCaptains(d.Context, "season", true));

    [Test]
    public Task ScheduleShow_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.ScheduleShow(d.Context, "season"));

    [Test]
    public Task ScheduleRun_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.ScheduleRun(d.Context, "season"));

    [Test]
    public Task ScheduleApply_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.ScheduleApply(d.Context, 1));

    [Test]
    public Task HaloPull_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.HaloPull(d.Context, Guid.NewGuid().ToString()));

    [Test]
    public Task RefreshMedals_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.RefreshMedals(d.Context));

    [Test]
    public Task Jobs_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Jobs(d.Context));

    [Test]
    public Task Job_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Job(d.Context, "group/name", null));

    [Test]
    public Task Claims_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Claims(d.Context));

    [Test]
    public Task DecideClaim_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.DecideClaim(d.Context, 1, true, null));

    [TestCase(nameof(AdminCommandsHandler.ReportGame))]
    [TestCase(nameof(AdminCommandsHandler.Forfeit))]
    [TestCase(nameof(AdminCommandsHandler.UndoResult))]
    [TestCase(nameof(AdminCommandsHandler.PullGames))]
    [TestCase(nameof(AdminCommandsHandler.SetTime))]
    public async Task CommissionerOrStaffActions_ReplyNoSuchMatch_WhenTheMatchIsNotFound(string method)
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner, DiscordCaller.Roles.Sysadmin);
        var handler = Handler();

        Task task = method switch
        {
            nameof(AdminCommandsHandler.ReportGame) => handler.ReportGame(discord.Context, "nonsense", Guid.NewGuid().ToString(), null),
            nameof(AdminCommandsHandler.Forfeit) => handler.Forfeit(discord.Context, "nonsense", ForfeitSide.Home),
            nameof(AdminCommandsHandler.UndoResult) => handler.UndoResult(discord.Context, "nonsense"),
            nameof(AdminCommandsHandler.PullGames) => handler.PullGames(discord.Context, "nonsense"),
            _ => handler.SetTime(discord.Context, "nonsense", "2026-11-20 20:00"),
        };
        await task;

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchMatch));
    }

    [TestCase(nameof(AdminCommandsHandler.GenerateMatches))]
    [TestCase(nameof(AdminCommandsHandler.CreateBracket))]
    [TestCase(nameof(AdminCommandsHandler.SeedBracket))]
    [TestCase(nameof(AdminCommandsHandler.LockCaptains))]
    [TestCase(nameof(AdminCommandsHandler.ScheduleShow))]
    [TestCase(nameof(AdminCommandsHandler.ScheduleRun))]
    public async Task CommissionerOrStaffActions_ReplyNoSuchSeason_WhenTheSeasonIsNotFound(string method)
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner, DiscordCaller.Roles.Sysadmin);
        var handler = Handler();

        Task task = method switch
        {
            nameof(AdminCommandsHandler.GenerateMatches) => handler.GenerateMatches(discord.Context, "nonsense", 1, 3),
            nameof(AdminCommandsHandler.CreateBracket) => handler.CreateBracket(discord.Context, "nonsense", 8, false, 3),
            nameof(AdminCommandsHandler.SeedBracket) => handler.SeedBracket(discord.Context, "nonsense"),
            nameof(AdminCommandsHandler.LockCaptains) => handler.LockCaptains(discord.Context, "nonsense", true),
            nameof(AdminCommandsHandler.ScheduleShow) => handler.ScheduleShow(discord.Context, "nonsense"),
            _ => handler.ScheduleRun(discord.Context, "nonsense"),
        };
        await task;

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchSeason));
    }

    [Test]
    public async Task HaloPull_RepliesNoSuchGame_ForAnUnparsableID()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Sysadmin);

        await Handler().HaloPull(discord.Context, "not-a-guid");

        Assert.That(discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
    }

    [Test]
    public async Task Job_RepliesHowToNameIt_WhenNotGroupSlashName()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Sysadmin);

        await Handler().Job(discord.Context, "badname", null);

        Assert.That(discord.Content(), Is.EqualTo("Name the job as group/name: /admin jobs list shows them."));
    }

    [Test]
    public async Task SetTime_RepliesCouldNotReadTime_ForABadTimeString()
    {
        var season = await AddSeason("Winter");
        var home = await AddTeam(season, "Home");
        var away = await AddTeam(season, "Away");
        await AddSeasonMatch(season, home, away);
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await Handler().SetTime(discord.Context, "Home", "not a time");

        Assert.That(discord.Content(), Is.EqualTo("I couldn't read that time. Use UTC like 2026-11-20 20:00."));
    }

    [Test]
    public async Task GenerateMatches_RepliesValidationError_ForNonPositiveCounts()
    {
        await AddSeason("Winter");
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await Handler().GenerateMatches(discord.Context, "Winter", 0, 3);

        Assert.That(discord.Content(), Is.EqualTo("Home matches per team and best of must be at least 1."));
    }

    [Test]
    public async Task CreateBracket_RepliesValidationError_ForNonPositiveParticipants()
    {
        await AddSeason("Winter");
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await Handler().CreateBracket(discord.Context, "Winter", 0, false, 3);

        Assert.That(discord.Content(), Is.EqualTo("A bracket needs participants."));
    }

    [Test]
    public async Task Dashboard_ShowsNothingPendingOrOverdue_ForAnEmptyDatabase()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().Dashboard(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo("0 pending reschedules · 0 overdue matches · 0 over 3 days"));
    }

    [Test]
    public async Task Overdue_RepliesNoMatchIsOverdue_ForAnEmptyDatabase()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().Overdue(discord.Context);

        Assert.That(discord.Embed().Description, Does.Contain("No match is overdue."));
    }

    [Test]
    public async Task PendingReschedules_RepliesNoneArePending_ForAnEmptyDatabase()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().PendingReschedules(discord.Context);

        Assert.That(discord.Embed().Description, Does.Contain("No reschedule is pending."));
    }

    [Test]
    public async Task Claims_RepliesNoneArePending_ForAnEmptyDatabase()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Sysadmin);

        await DeepHandler().Claims(discord.Context);

        Assert.That(discord.Embed().Description, Does.Contain("No claim is pending."));
    }

    [Test]
    public async Task RefreshMedals_RepliesRefreshed()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Sysadmin);

        await DeepHandler().RefreshMedals(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo("The medals are refreshed."));
        await _dataPullService.Received(1).DownloadMedals();
    }

    [Test]
    public async Task HaloPull_RepliesPulled_ForAValidGuid()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Sysadmin);
        var matchID = Guid.NewGuid();

        await DeepHandler().HaloPull(discord.Context, matchID.ToString());

        Assert.That(discord.Content(), Is.EqualTo($"Pulled: [the game](https://grif.test/match/{matchID})."));
        await _dataPullService.Received(1).GetAndSaveMatch(matchID);
    }

    [Test]
    public async Task ScheduleApply_RepliesDoesNotExist_ForANonExistentRecommendation()
    {
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner, DiscordCaller.Roles.Sysadmin);

        await DeepHandler().ScheduleApply(discord.Context, 999);

        Assert.That(discord.Content(), Is.EqualTo("That recommendation doesn't exist or was applied already."));
    }

    [Test]
    public async Task Claims_ListsAPendingClaim()
    {
        // ClaimantUserID is a foreign key: a real user first
        var claimant = await AddUser(discordUserId: 2, gamertag: "Claimant");
        Db.Set<GamertagClaim>().Add(GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "Grifter1", normalizedGamertag: "GRIFTER1"));
        await Db.SaveChangesAsync();
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Sysadmin);

        await DeepHandler().Claims(discord.Context);

        Assert.That(discord.Embed().Description, Does.Contain("Grifter1"));
    }

    [Test]
    public async Task LockCaptains_LocksTheDraft_ForAFoundSeason()
    {
        var season = await AddSeason("Winter");
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().LockCaptains(discord.Context, "Winter", @lock: true);

        Assert.That(discord.Content(), Is.EqualTo("The captains are locked."));
        // _teams is a real TeamService (not a substitute): read the season back rather than assert on a mock.
        var locked = await Db.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => s.CaptainsLocked).FirstAsync();
        Assert.That(locked, Is.True);
    }

    [Test]
    public async Task GenerateMatches_RepliesTheMatchesAreMade_ForAFoundSeason()
    {
        await AddSeason("Winter");
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().GenerateMatches(discord.Context, "Winter", 1, 3);

        Assert.That(discord.Content(), Does.Contain("The matches are made."));
    }

    [Test]
    public async Task CreateBracket_RepliesTheBracketIsMade_ForAFoundSeason()
    {
        var season = await AddSeason("Winter");
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().CreateBracket(discord.Context, "Winter", 8, false, 3);

        Assert.That(discord.Content(), Is.EqualTo("The bracket is made. Seed it with /admin bracket seed, or set custom seeds on the site."));
        await _bracketService.Received(1).CreateBracket(8, season.SeasonID, false, 3, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SeedBracket_RepliesTheBracketIsSeeded_ForAFoundSeason()
    {
        var season = await AddSeason("Winter");
        var discord = await AuthorizedCaller(DiscordCaller.Roles.Commissioner);

        await DeepHandler().SeedBracket(discord.Context, "Winter");

        Assert.That(discord.Content(), Is.EqualTo("The bracket is seeded by the standings."));
        await _bracketService.Received(1).SetSeeds(season.SeasonID, null, Arg.Any<CancellationToken>());
    }
}
