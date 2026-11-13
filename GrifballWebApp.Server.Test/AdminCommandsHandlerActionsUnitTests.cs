using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Scheduler;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using GrifballWebApp.Server.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// AdminCommandsHandler's actions done, past the staff gate AdminCommandsHandlerUnitTests covers: on a temp-file SQLite
/// GrifballContext with the real services, Quartz in memory for the runs it queues (<see cref="InMemoryQuartz"/>), and a
/// substituted scheduler for the Jobs page's actions (as JobsService's own unit tests do).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AdminCommandsHandlerActionsUnitTests : SqliteGrifballContextFixture
{
    private static readonly JobKey Cleanup = new("cleanup", "Maintenance");

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private InMemoryQuartz? _quartz;

    [TearDown]
    public async Task TearDownQuartz()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
    }

    private async Task<AdminCommandsHandler> Handler()
    {
        _quartz = await InMemoryQuartz.Create();
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns("admin-test");
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([new JobHeader(Cleanup, "Cleans up", "Cleanup", true, false, false, false)], false));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);
        _scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode>());
        var history = Substitute.For<IExecutionHistoryStore>();
        history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([], false));
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        return new AdminCommandsHandler(
            new DiscordCaller(Db), new CommandLookups(Db, null!),
            new RescheduleService(Db, Substitute.For<IOutbox>()),
            new SeasonMatchService(Db, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(Db)),
            new MatchPlannerService(Db),
            Substitute.For<IBracketService>(),
            null!,
            new ScheduleRecommendations(Db, time),
            _quartz.OnDemand,
            new JobsService(_schedulers, history, new RecurringJobs(), time),
            new GamertagClaimService(Db, Substitute.For<IUserMergeService>(), null, time),
            Substitute.For<IDataPullService>(),
            Urls());
    }

    private async Task<(CommandContextRecorder Discord, User Caller)> Caller(params string[] roles)
    {
        var user = await AddUser(discordUserId: 42, gamertag: "Staffer", roles: roles);
        return (new CommandContextRecorder(discordUserId: 42), user);
    }

    private async Task<SeasonMatch> AMatch(DateTime? scheduledTime = null)
    {
        var season = await AddSeason("Winter");
        var home = await AddTeam(season, "Home");
        var away = await AddTeam(season, "Away");
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3, scheduledTime: scheduledTime);
        Db.SeasonMatches.Add(match);
        await Db.SaveChangesAsync();
        return match;
    }

    private async Task<SeasonMatch> ReadBack(int seasonMatchID)
    {
        Db.ChangeTracker.Clear();
        return await Db.SeasonMatches.AsNoTracking().SingleAsync(x => x.SeasonMatchID == seasonMatchID);
    }

    [Test]
    public async Task Overdue_ListsAMatchPastItsTime_WithItsLink()
    {
        var match = await AMatch(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);

        await (await Handler()).Overdue(discord.Context);

        Assert.That(discord.Embed().Description, Does.StartWith($"[UNKNOWN vs UNKNOWN](https://grif.test/seasonmatch/{match.SeasonMatchID}) · ").And.EndWith(" hours overdue"));
    }

    [Test]
    public async Task Dashboard_CountsWhatIsPendingAndOverdue()
    {
        var match = await AMatch(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var (discord, caller) = await Caller(DiscordCaller.Roles.Commissioner);
        Db.MatchReschedules.Add(MatchReschedule.Request(match.SeasonMatchID, match.ScheduledTime, null, "Away all week", caller.Id, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await Db.SaveChangesAsync();

        await (await Handler()).Dashboard(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo("1 pending reschedules · 1 overdue matches · 1 over 3 days"));
    }

    [Test]
    public async Task PendingReschedules_ListsARequest_WithWhoAskedAndWhy()
    {
        var match = await AMatch();
        var (discord, caller) = await Caller(DiscordCaller.Roles.Commissioner);
        var request = MatchReschedule.Request(match.SeasonMatchID, null, null, "Away all week", caller.Id, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Db.MatchReschedules.Add(request);
        await Db.SaveChangesAsync();

        await (await Handler()).PendingReschedules(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo(
            $"**#{request.MatchRescheduleID}** [UNKNOWN vs UNKNOWN](https://grif.test/seasonmatch/{match.SeasonMatchID}) · no time given · by Staffer: Away all week"));
    }

    [TestCase(true, "Approved.", RescheduleStatus.Approved)]
    [TestCase(false, "Rejected.", RescheduleStatus.Rejected)]
    public async Task DecideReschedule_DecidesTheRequest_AsTheCaller(bool approve, string reply, RescheduleStatus status)
    {
        var match = await AMatch();
        var (discord, caller) = await Caller(DiscordCaller.Roles.Commissioner);
        var request = MatchReschedule.Request(match.SeasonMatchID, null, null, "Away all week", caller.Id, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Db.MatchReschedules.Add(request);
        await Db.SaveChangesAsync();

        await (await Handler()).DecideReschedule(discord.Context, request.MatchRescheduleID, approve, "Fine by me");

        Db.ChangeTracker.Clear();
        var decided = await Db.MatchReschedules.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo(reply));
            Assert.That((decided.Status, decided.ApprovedByUserID), Is.EqualTo((status, (int?)caller.Id)));
        }
    }

    [Test]
    public async Task DecideReschedule_AnUnknownRequest_RepliesWhy()
    {
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);

        await (await Handler()).DecideReschedule(discord.Context, 999, true, null);

        Assert.That(discord.Content(), Is.EqualTo("Reschedule request not found"));
    }

    [TestCase(ForfeitSide.Home, SeasonMatchResult.Forfeit, SeasonMatchResult.Won)]
    [TestCase(ForfeitSide.Away, SeasonMatchResult.Won, SeasonMatchResult.Forfeit)]
    [TestCase(ForfeitSide.Both, SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit)]
    public async Task Forfeit_DecidesTheMatch_ForTheSideNamed(ForfeitSide side, SeasonMatchResult home, SeasonMatchResult away)
    {
        var match = await AMatch();
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);

        await (await Handler()).Forfeit(discord.Context, match.SeasonMatchID.ToString(), side);

        var decided = await ReadBack(match.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo($"Forfeited ({side.ToString().ToLowerInvariant()})."));
            Assert.That((decided.HomeTeamResult, decided.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)home, (SeasonMatchResult?)away)));
        }
    }

    [Test]
    public async Task UndoResult_ReopensAForfeitedMatch()
    {
        var match = await AMatch();
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);
        var handler = await Handler();
        await handler.Forfeit(new CommandContextRecorder(discordUserId: 42).Context, match.SeasonMatchID.ToString(), ForfeitSide.Home);

        await handler.UndoResult(discord.Context, match.SeasonMatchID.ToString());

        var reopened = await ReadBack(match.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("The result is undone."));
            Assert.That((reopened.HomeTeamResult, reopened.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        }
    }

    [TestCase("not-a-guid")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public async Task ReportGame_AGameIDThatIsNoGame_RepliesNoSuchGame(string gameID)
    {
        var match = await AMatch();
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);

        await (await Handler()).ReportGame(discord.Context, match.SeasonMatchID.ToString(), gameID, null);

        Assert.That(discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
    }

    [Test]
    public async Task ReportGame_AGameNobodyWon_IsRefusedByTheService_ForThePipelineToReply()
    {
        var match = await AMatch();
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);
        var gameID = Guid.Parse("4b1e7c5e-0d7e-4a8e-9a4f-2f6d8e1c3b77");
        var game = HaloMatch.Seed(gameID);
        game.AddTeam(0, 3, GrifballWebApp.Domain.Halo.Outcomes.Tie);
        Db.Matches.Add(game);
        await Db.SaveChangesAsync();
        var handler = await Handler();

        var refusal = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => handler.ReportGame(discord.Context, match.SeasonMatchID.ToString(), gameID.ToString(), null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal!.Message, Is.EqualTo("There must be a winner for the match you are reporting"));
            Assert.That(discord.Edits, Is.Empty, "Nothing replied: the pipeline replies with the refusal");
        }
    }

    [Test]
    public async Task SetTime_SchedulesTheMatch()
    {
        var match = await AMatch();
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);

        await (await Handler()).SetTime(discord.Context, match.SeasonMatchID.ToString(), "2026-11-20 20:00");

        var scheduled = await ReadBack(match.SeasonMatchID);
        var when = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo($"The match is set for <t:{new DateTimeOffset(when).ToUnixTimeSeconds()}>."));
            Assert.That(scheduled.ScheduledTime, Is.EqualTo(when));
        }
    }

    [Test]
    public async Task PullGames_QueuesThePull_AsTheCaller()
    {
        var match = await AMatch();
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);
        var handler = await Handler();

        await handler.PullGames(discord.Context, match.SeasonMatchID.ToString());

        var trigger = OnDemandJobs.TriggerKeyOf<SeasonMatchPullJob>(SeasonMatchPullJob.RunName(match.SeasonMatchID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo($"The pull is queued. Its games show on [the match page](https://grif.test/seasonmatch/{match.SeasonMatchID})."));
            Assert.That(await _quartz!.Scheduler.GetTriggerState(trigger), Is.EqualTo(TriggerState.Normal));
        }
    }

    [Test]
    public async Task ScheduleRun_QueuesTheRecommendation_AndScheduleShowSaysSo()
    {
        var season = await AddSeason("Winter");
        var (discord, _) = await Caller(DiscordCaller.Roles.Commissioner);
        var shown = new CommandContextRecorder(discordUserId: 42);
        var handler = await Handler();

        await handler.ScheduleRun(discord.Context, "Winter");
        await handler.ScheduleShow(shown.Context, "Winter");

        var trigger = OnDemandJobs.TriggerKeyOf<ScheduleRecommendationJob>(ScheduleRecommendationJob.RunName(season.SeasonID));
        var embed = shown.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("The recommendation is queued. See it with /admin schedule show."));
            Assert.That(await _quartz!.Scheduler.GetTriggerState(trigger), Is.EqualTo(TriggerState.Normal));
            Assert.That((embed.Title, embed.Url), Is.EqualTo(("Schedule (queued)", $"https://grif.test/season/{season.SeasonID}")));
            Assert.That(embed.Description, Is.EqualTo("No recommendation yet: /admin schedule run works one out.\n\nNo matches."));
        }
    }

    [Test]
    public async Task Jobs_ListsEveryJob_WithItsState()
    {
        var (discord, _) = await Caller(DiscordCaller.Roles.Sysadmin);

        await (await Handler()).Jobs(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo("`Maintenance/cleanup` · Off"));
    }

    [Test]
    public async Task Job_RunsItNow_AsTheCaller()
    {
        var (discord, caller) = await Caller(DiscordCaller.Roles.Sysadmin);
        var handler = await Handler();
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(true);

        await handler.Job(discord.Context, " Maintenance/cleanup ", pause: null);

        Assert.That(discord.Content(), Is.EqualTo(AdminCommandsHandler.Done));
        await _scheduler.Received(1).TriggerJob(Cleanup, Arg.Is<JobDataMap>(x => (string?)x[JobUser.DataKey] == caller.Id.ToString()), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Job_Pauses_ByTheCaller()
    {
        var (discord, _) = await Caller(DiscordCaller.Roles.Sysadmin);
        var handler = await Handler();
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(true);

        await handler.Job(discord.Context, "Maintenance/cleanup", pause: true);

        Assert.That(discord.Content(), Is.EqualTo(AdminCommandsHandler.Done));
        await _scheduler.Received(1).PauseJobWith(Cleanup, Arg.Is<PauseDetails>(x => x.RequestedBy == "Staffer" && x.Reason == JobsService.PausedReason), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Job_ThatDoesNotExist_RepliesSo()
    {
        var (discord, _) = await Caller(DiscordCaller.Roles.Sysadmin);
        var handler = await Handler();
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(false);

        await handler.Job(discord.Context, "Maintenance/cleanup", pause: false);

        Assert.That(discord.Content(), Is.EqualTo("There is no such job."));
    }

    [Test]
    public async Task DecideClaim_Rejects_WithTheReason()
    {
        var claimant = await AddUser(discordUserId: 2, gamertag: "Claimant");
        var claim = GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "Grifter1", normalizedGamertag: "GRIFTER1");
        Db.Set<GamertagClaim>().Add(claim);
        await Db.SaveChangesAsync();
        var (discord, _) = await Caller(DiscordCaller.Roles.Sysadmin);

        await (await Handler()).DecideClaim(discord.Context, claim.GamertagClaimID, approve: false, "Not yours");

        Db.ChangeTracker.Clear();
        var decided = await Db.Set<GamertagClaim>().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("Rejected."));
            Assert.That((decided.Status, decided.RejectionReason), Is.EqualTo((GamertagClaimStatus.Rejected, (string?)"Not yours")));
        }
    }

    [Test]
    public async Task DecideClaim_ApprovingARejectedClaim_IsRefusedByTheService()
    {
        var claimant = await AddUser(discordUserId: 2, gamertag: "Claimant");
        var claim = GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "Grifter1", normalizedGamertag: "GRIFTER1");
        Db.Set<GamertagClaim>().Add(claim);
        await Db.SaveChangesAsync();
        var (discord, _) = await Caller(DiscordCaller.Roles.Sysadmin);
        var handler = await Handler();
        await handler.DecideClaim(new CommandContextRecorder(discordUserId: 42).Context, claim.GamertagClaimID, approve: false, null);

        var refusal = await Assert.ThrowsAsync<GamertagClaimException>(() => handler.DecideClaim(discord.Context, claim.GamertagClaimID, approve: true, null));

        Assert.That(refusal!.Message, Is.EqualTo($"Claim {claim.GamertagClaimID} was already rejected"));
    }
}
