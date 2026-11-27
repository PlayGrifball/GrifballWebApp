using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Staff's tools (#272): the commissioner dashboard and claims read, commissioners' and sysadmins' actions on the same services as
/// their pages and /admin, each refused to a caller without its roles. On a temp-file SQLite GrifballContext.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StaffToolsUnitTests : SqliteGrifballContextFixture
{
    private static readonly ToolCaller Commissioner = new(1, [SiteRoles.Commissioner]);
    private static readonly ToolCaller Sysadmin = new(2, [SiteRoles.Sysadmin]);
    private static readonly ToolCaller Player = new(3, [SiteRoles.Player]);

    private static readonly string[] CommissionersRefusals =
    [
        "Say who forfeits: home, away or both.",
        "There is no season match 999999.",
        "Home matches per team and best of must be at least 1.",
        "A bracket needs participants.",
        "Best of must be at least 1.",
        "There is no season 999999: list_seasons has their IDs.",
        "Reschedule request not found",
    ];

    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private readonly IJobControl _jobs = Substitute.For<IJobControl>();
    private readonly ISetGamertagService _setGamertag = Substitute.For<ISetGamertagService>();
    private readonly IScheduleRecommendationRuns _runs = Substitute.For<IScheduleRecommendationRuns>();
    private readonly IBracketService _brackets = Substitute.For<IBracketService>();
    private readonly IDiscordGamertagSetter _discordSetter = Substitute.For<IDiscordGamertagSetter>();
    private readonly IUserMergeService _userMerges = Substitute.For<IUserMergeService>();
    private readonly GrifballWebApp.Domain.Shared.ICurrentUserService _currentUser = Substitute.For<GrifballWebApp.Domain.Shared.ICurrentUserService>();

    private DuplicateSuggestionService Duplicates() => new(Db, _userMerges, TimeProvider.System);

    private UserMergeAuditService AuditedMerges() => new(Db, TimeProvider.System, _currentUser);

    private SeasonService Seasons() => new(Db, new SeasonStatsService(Db), Visibility());

    private ScheduleRecommendations Recommendations() => new(Db, TimeProvider.System);

    private SeasonVisibility Visibility() => new(Db);

    private StaffTools Staff(ToolCaller caller)
    {
        var reschedules = new RescheduleService(Db, Substitute.For<IOutbox>());
        return new(caller, new CommissionerDashboardService(reschedules, new EventOrganizerService(Db), new StaffStatFlagService(Db)), Claims(), Seasons(), Visibility(), Recommendations(), _runs, _jobs, Duplicates(), AuditedMerges());
    }

    private GamertagClaimService Claims() => new(Db, Substitute.For<IUserMergeService>(), null);

    private CommissionerTools Commissioners(ToolCaller caller) => new(caller, Seasons(), Visibility(),
        new RescheduleService(Db, Substitute.For<IOutbox>()),
        new SeasonMatchService(Db, _dataPull, Substitute.For<IBracketService>(), new ChampionshipService(Db)),
        new MatchPlannerService(Db), _brackets,
        new TeamService(Db, Substitute.For<IDatabaseLocks>()), Recommendations(), _runs);

    private SysadminTools Sysadmins(ToolCaller caller) => new(caller, Claims(), _dataPull, new MatchDetailsService(Db), _jobs, _setGamertag, Db, _discordSetter, Duplicates(), _userMerges, AuditedMerges());

    private async Task<int> AMatch()
    {
        var season = await AddSeason("Winter");
        return (await AddSeasonMatch(season, await AddTeam(season, "Grifs"), await AddTeam(season, "Rivals"))).SeasonMatchID;
    }

    [Test]
    public async Task EachStaffTool_RefusesACallerWithoutItsRoles()
    {
        var match = await AMatch();

        var refusals = new[]
        {
            await Assert.ThrowsAsync<ToolException>(() => Staff(Player).GetCommissionerDashboard()),
            await Assert.ThrowsAsync<ToolException>(() => Staff(Commissioner).ListPendingGamertagClaims()),
            await Assert.ThrowsAsync<ToolException>(() => Commissioners(Player).DecideReschedule(1, true)),
            await Assert.ThrowsAsync<ToolException>(() => Commissioners(Sysadmin).ForfeitSeasonMatch(match, "home")),
            await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Commissioner).RefreshMedals()),
        };

        Assert.That(refusals.Select(r => r!.Message), Has.All.StartWith("Only a"));
        await _dataPull.DidNotReceive().DownloadMedals();
    }

    [Test]
    public async Task TheDashboard_IsTheSitesDashboard_ForStaff()
    {
        var dashboard = await Staff(Sysadmin).GetCommissionerDashboard();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dashboard.PendingReschedules, Is.Empty);
            Assert.That(dashboard.Summary.OverdueMatchCount, Is.Zero);
            Assert.That(await Staff(Sysadmin).ListPendingGamertagClaims(), Is.Empty);
        }
    }

    [Test]
    public async Task AForfeit_IsAResult_ThatCanBeUndone()
    {
        var match = await AMatch();
        var tools = Commissioners(Commissioner);

        var forfeited = await tools.ForfeitSeasonMatch(match, " Home ");
        var undone = await tools.UndoSeasonMatchResult(match);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forfeited, Is.EqualTo("Forfeited (home)."));
            Assert.That(undone, Is.EqualTo("The result is undone."), "it had a result to undo");
        }
    }

    [Test]
    public async Task TheCommissionersTools_RefuseWhatTheyCantDo()
    {
        var match = await AMatch();
        var tools = Commissioners(Commissioner);

        var refusals = new[]
        {
            await Assert.ThrowsAsync<ToolException>(() => tools.ForfeitSeasonMatch(match, "sideways")),
            await Assert.ThrowsAsync<ToolException>(() => tools.UndoSeasonMatchResult(999999)),
            await Assert.ThrowsAsync<ToolException>(() => tools.GenerateSeasonMatches(0, 3)),
            await Assert.ThrowsAsync<ToolException>(() => tools.CreateSeasonBracket(0, false, 3)),
            await Assert.ThrowsAsync<ToolException>(() => tools.CreateSeasonBracket(4, false, 0)),
            await Assert.ThrowsAsync<ToolException>(() => tools.SeedSeasonBracket(999999)),
            await Assert.ThrowsAsync<ToolException>(() => tools.DecideReschedule(999999, true)),
        };

        Assert.That(refusals.Select(r => r!.Message), Is.EqualTo(CommissionersRefusals));
    }

    [Test]
    public async Task AMatchsTime_AndTheCaptainsLock_AreSet()
    {
        var match = await AMatch();
        var season = (await Db.SeasonMatches.AsNoTracking().SingleAsync(m => m.SeasonMatchID == match)).SeasonID;
        var tools = Commissioners(Commissioner);
        var when = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);

        var timed = await tools.SetSeasonMatchTime(match, when);
        var locked = await tools.SetCaptainsLocked(true, season);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timed, Is.EqualTo("The match is set for 2026-11-20 20:00 UTC."));
            Assert.That((await Db.SeasonMatches.AsNoTracking().SingleAsync(m => m.SeasonMatchID == match)).ScheduledTime, Is.EqualTo(when));
            Assert.That(locked, Is.EqualTo("The captains are locked."));
            Assert.That((await Db.Seasons.AsNoTracking().SingleAsync(s => s.SeasonID == season)).CaptainsLocked, Is.True);
        }
    }

    [Test]
    public async Task TheSysadminsTools_PullAGame_AndRefreshTheMedals_AndRefuseANonGame()
    {
        var game = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
        var notATeamGame = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
        _dataPull.When(d => d.GetAndSaveMatch(game)).Do(_ =>
        {
            Db.Matches.Add(HaloMatch.Seed(game));
            Db.SaveChanges();
        });
        var tools = Sysadmins(Sysadmin);

        var pulled = await tools.PullHaloGame(game);
        var medals = await tools.RefreshMedals();
        var refused = await Assert.ThrowsAsync<ToolException>(() => tools.PullHaloGame(Guid.Empty));
        var nothingSaved = await Assert.ThrowsAsync<ToolException>(() => tools.PullHaloGame(notATeamGame));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pulled, Is.EqualTo("Pulled: get_game shows it."));
            Assert.That(medals, Is.EqualTo("The medals are downloaded."));
            Assert.That(refused!.Message, Is.EqualTo("That isn't a game's match ID."));
            Assert.That(nothingSaved!.Message, Is.EqualTo($"Halo has no team game {notATeamGame}: nothing was saved."));
        }
        await _dataPull.Received(1).GetAndSaveMatch(game);
        await _dataPull.Received(1).DownloadMedals();
    }

    [Test]
    public async Task AClaimThatIsntThere_IsTheClaimServicesRefusal()
    {
        var refused = await Assert.ThrowsAsync<GamertagClaimException>(() => Sysadmins(Sysadmin).DecideGamertagClaim(987654, approve: false, "no"));

        Assert.That(refused!.Message, Does.Contain("There is no claim 987654"));
    }

    [Test]
    public async Task TheJobs_AreListed_Run_AndPaused_AndAJobThatIsntThere_IsRefused()
    {
        _jobs.List(Arg.Any<CancellationToken>()).Returns(new List<JobSummaryDto> { new("Discord", "Outbox", "Scheduled", true) });
        _jobs.RunNow("Discord", "Outbox", Sysadmin.UserID, Arg.Any<CancellationToken>()).Returns(JobControlResult.Done);
        _jobs.SetPaused("Discord", "Outbox", true, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(JobControlResult.Done);
        _jobs.RunNow("Nope", "Nothing", Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(JobControlResult.NotFound);
        _jobs.SetPaused("Discord", "Busy", false, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(JobControlResult.Conflict);
        // Mirrors JobsService.RunNow's own refusal of an access token on a merge-capable job (historical-refresh);
        // mocked here since IJobControl, not the real JobsService, backs this test.
        _jobs.RunNow("recurring", "historical-refresh", Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(JobControlResult.DeniedToAccessToken);

        var listed = await Staff(Sysadmin).ListJobs();
        var ran = await Sysadmins(Sysadmin).RunJob("Discord", "Outbox");
        var paused = await Sysadmins(Sysadmin).SetJobPaused("Discord", "Outbox", true);
        var missing = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).RunJob("Nope", "Nothing"));
        var busy = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetJobPaused("Discord", "Busy", false));
        var deniedToToken = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).RunJob("recurring", "historical-refresh"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(listed.Single().Name, Is.EqualTo("Outbox"));
            Assert.That(ran, Is.EqualTo("It's running."));
            Assert.That(paused, Is.EqualTo("It's paused."));
            Assert.That(missing!.Message, Does.StartWith("There is no such job"));
            Assert.That(busy!.Message, Does.StartWith("The job can't do that now"));
            Assert.That(deniedToToken!.Message, Does.Contain("access token"));
        }
    }

    [Test]
    public async Task AScheduleRecommendation_IsShown_Run_AndOneThatIsntThere_IsRefusedToApply()
    {
        var season = await AddSeason("Winter");
        _runs.Status(season.SeasonID, Arg.Any<CancellationToken>()).Returns(new OnDemandJobStatusDto { State = OnDemandJobState.Idle });
        _runs.Queue(season.SeasonID, Arg.Any<string?>(), Commissioner.UserID, Arg.Any<CancellationToken>()).Returns(new OnDemandJobStatusDto { State = OnDemandJobState.Queued });

        var shown = await Staff(Commissioner).GetScheduleRecommendation(season.SeasonID);
        var ran = await Commissioners(Commissioner).RunScheduleRecommendation(season.SeasonID);
        var refused = await Assert.ThrowsAsync<ToolException>(() => Commissioners(Commissioner).ApplyScheduleRecommendation(987654));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shown.Run.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(shown.Latest, Is.Null, "none worked out yet");
            Assert.That(ran.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(refused!.Message, Is.EqualTo("There is no recommendation 987654, or it was applied already."));
        }
    }

    [TestCase("away", "Forfeited (away).")]
    [TestCase("BOTH", "Forfeited (both).")]
    public async Task EitherSide_OrBoth_CanForfeit(string side, string answer)
    {
        var match = await AMatch();

        Assert.That(await Commissioners(Commissioner).ForfeitSeasonMatch(match, side), Is.EqualTo(answer));
    }

    [Test]
    public async Task AGameThatIsntThere_IsTheMatchServicesRefusal()
    {
        var match = await AMatch();

        var refused = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => Commissioners(Commissioner).ReportSeasonMatchGame(match, Guid.Parse("00000000-0000-0000-0000-0000000000c2")));

        Assert.That(refused!.Message, Is.EqualTo("Match does not exist"));
    }

    [TestCase(true, "Approved.", RescheduleStatus.Approved)]
    [TestCase(false, "Rejected.", RescheduleStatus.Rejected)]
    public async Task ARescheduleRequest_IsDecided_AsTheCaller(bool approve, string reply, RescheduleStatus status)
    {
        var match = await AMatch();
        var staffer = await AddUser(77, "Staffer", SiteRoles.Commissioner);
        var request = MatchReschedule.Request(match, null, null, "Away all week", staffer.Id, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Db.MatchReschedules.Add(request);
        await Db.SaveChangesAsync();

        var answer = await Commissioners(new ToolCaller(staffer.Id, [SiteRoles.Commissioner])).DecideReschedule(request.MatchRescheduleID, approve, "Fine by me");

        Db.ChangeTracker.Clear();
        var decided = await Db.MatchReschedules.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer, Is.EqualTo(reply));
            Assert.That((decided.Status, decided.ApprovedByUserID), Is.EqualTo((status, (int?)staffer.Id)));
        }
    }

    [Test]
    public async Task AClaim_IsRejected_WithItsReason()
    {
        var claimant = await AddUser(78, "Claimant");
        var claim = GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "Grifter1", normalizedGamertag: "GRIFTER1");
        Db.GamertagClaims.Add(claim);
        await Db.SaveChangesAsync();

        var answer = await Sysadmins(Sysadmin).DecideGamertagClaim(claim.GamertagClaimID, approve: false, "Not yours");

        Db.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer, Is.EqualTo("Rejected."));
            Assert.That((await Db.GamertagClaims.SingleAsync()).Status, Is.EqualTo(GamertagClaimStatus.Rejected));
        }
    }

    [Test]
    public async Task ALocalTime_IsTakenAsUtc()
    {
        var match = await AMatch();
        var local = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Local);

        await Commissioners(Commissioner).SetSeasonMatchTime(match, local);

        Assert.That((await Db.SeasonMatches.AsNoTracking().SingleAsync(m => m.SeasonMatchID == match)).ScheduledTime, Is.EqualTo(local.ToUniversalTime()));
    }

    [Test]
    public async Task ASeasonLeftOut_IsTheOneItsPurposeMeans()
    {
        var match = await AMatch();
        var season = (await Db.SeasonMatches.AsNoTracking().SingleAsync(m => m.SeasonMatchID == match)).SeasonID;
        var tools = Commissioners(Commissioner);

        var generated = await tools.GenerateSeasonMatches(1, 3);
        var bracket = await tools.CreateSeasonBracket(2, doubleElimination: true, 3);
        var seeded = await tools.SeedSeasonBracket();
        var unlocked = await tools.SetCaptainsLocked(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated, Is.EqualTo("The matches are made."));
            Assert.That(await Db.SeasonMatches.CountAsync(m => m.SeasonID == season), Is.EqualTo(2), "each team at home once");
            Assert.That(bracket, Is.EqualTo("The bracket is made."));
            Assert.That(seeded, Is.EqualTo("The bracket is seeded by the standings."));
            Assert.That(unlocked, Is.EqualTo("The captains are unlocked."));
        }
        await _brackets.Received(1).CreateBracket(2, season, true, 3, Arg.Any<CancellationToken>());
        await _brackets.Received(1).SetSeeds(season, ct: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NoSeasonAtAll_IsRefused_ForASeasonLeftOut()
    {
        var refused = await Assert.ThrowsAsync<ToolException>(() => Commissioners(Commissioner).SeedSeasonBracket());

        Assert.That(refused!.Message, Is.EqualTo("There is no season to do that to: name one (list_seasons has their IDs)."));
    }

    [Test]
    public async Task ARecommendation_IsApplied()
    {
        var season = await AddSeason("Winter");
        var match = await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
        var recommendation = ScheduleRecommendation.Propose(season.SeasonID, "commish", 0.75, 10,
            [new ProposedMatchTime(match.SeasonMatchID, new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc), false, true, true)]);
        Db.ScheduleRecommendations.Add(recommendation);
        await Db.SaveChangesAsync();

        Assert.That(await Commissioners(Commissioner).ApplyScheduleRecommendation(recommendation.ScheduleRecommendationID), Is.EqualTo("1 matches are scheduled."));
    }

    [Test]
    public async Task ApprovingAClaimThatIsntThere_IsTheClaimServicesRefusal()
    {
        var refused = await Assert.ThrowsAsync<GamertagClaimException>(() => Sysadmins(Sysadmin).DecideGamertagClaim(987654, approve: true));

        Assert.That(refused!.Message, Does.Contain("There is no claim 987654"));
    }

    [Test]
    public async Task SetUserGamertag_SetsItForAUserByID_ThroughTheSameServiceAsThePlayer()
    {
        var byID = GrifballWebApp.Domain.Players.User.Register("ById");
        Db.Users.Add(byID);
        await Db.SaveChangesAsync();
        _setGamertag.SetGamertag(Arg.Any<int>(), "Mavbrid", Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await Sysadmins(Sysadmin).SetUserGamertag(" Mavbrid ", userID: byID.Id);

        Assert.That(result, Does.Contain($"user {byID.Id}").And.Contain("Mavbrid"));
        await _setGamertag.Received(1).SetGamertag(byID.Id, "Mavbrid", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetUserGamertag_ByDiscordID_GoesThroughTheDiscordFlow_WithTheUsernameForANewAccount()
    {
        _discordSetter.SetGamertagForDiscordUser(758786051678142464UL, "newbie", "Mavbrid", Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await Sysadmins(Sysadmin).SetUserGamertag(" Mavbrid ", discordUserID: 758786051678142464L, discordUsername: " newbie ");

        Assert.That(result, Does.Contain("758786051678142464").And.Contain("Mavbrid"));
        await _discordSetter.Received(1).SetGamertagForDiscordUser(758786051678142464UL, "newbie", "Mavbrid", Arg.Any<CancellationToken>());
        await _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!, default);
    }

    [Test]
    public async Task SetUserGamertag_ByDiscordID_ASetterRefusalIsTheAnswer_AndANonsenseIDIsRefused()
    {
        _discordSetter.SetGamertagForDiscordUser(404UL, null, "Mavbrid", Arg.Any<CancellationToken>()).Returns(NoDiscordGamertagSetter.Refusal);

        var refused = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Mavbrid", discordUserID: 404));
        var nonsense = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Mavbrid", discordUserID: -5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused!.Message, Is.EqualTo(NoDiscordGamertagSetter.Refusal));
            Assert.That(nonsense!.Message, Does.Contain("Discord user ID"));
        }
    }

    [Test]
    public async Task SetUserGamertag_RefusesWhatThePlayerIsRefused_AWrongNaming_APlaceholder_AndNonSysadmins()
    {
        var real = GrifballWebApp.Domain.Players.User.Register("Real");
        var placeholder = GrifballWebApp.Domain.Players.User.ImportPlaceholder("historical:OldTag", "OldTag", null);
        Db.Users.AddRange(real, placeholder);
        await Db.SaveChangesAsync();
        _setGamertag.SetGamertag(real.Id, "Taken", Arg.Any<CancellationToken>()).Returns("That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect");

        var taken = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Taken", userID: real.Id));
        var noOne = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Mavbrid"));
        var both = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Mavbrid", userID: 5, discordUserID: 9));
        var missing = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Mavbrid", userID: 987654));
        var dummy = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag("Mavbrid", userID: placeholder.Id));
        var blank = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).SetUserGamertag(" ", userID: 5));
        var player = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Player).SetUserGamertag("Mavbrid", userID: 5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taken!.Message, Does.Contain("already attached"));
            Assert.That(noOne!.Message, Does.Contain("userID or by discordUserID"));
            Assert.That(both!.Message, Does.Contain("userID or by discordUserID"));
            Assert.That(missing!.Message, Does.Contain("There is no user 987654"));
            Assert.That(dummy!.Message, Does.Contain("placeholder"));
            Assert.That(blank!.Message, Does.Contain("Give the gamertag"));
            Assert.That(player!.Message, Is.EqualTo(SiteRoles.MissingRole(GrifTools.RolesByName["set_user_gamertag"])));
        }
        await _setGamertag.DidNotReceive().SetGamertag(Arg.Is<int>(i => i != real.Id), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MergeDuplicateUser_MergesAPlaceholderIntoTheTarget_AndTheServicesRefusalsAreTheAnswer()
    {
        var placeholder = GrifballWebApp.Domain.Players.User.ImportPlaceholder("historical:OldTag", "OldTag", null);
        var real = GrifballWebApp.Domain.Players.User.Register("Real");
        Db.Users.AddRange(placeholder, real);
        await Db.SaveChangesAsync();

        var done = await Sysadmins(Sysadmin).MergeDuplicateUser(placeholder.Id, real.Id);
        var notPlaceholder = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeDuplicateUser(real.Id, placeholder.Id));
        var missing = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeDuplicateUser(987654, real.Id));
        _userMerges.Merge(real.Id, placeholder.Id, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(_ => throw new MergeConflictException("Both are on a team in Winter"));
        var conflict = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeDuplicateUser(placeholder.Id, real.Id));
        var player = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Player).MergeDuplicateUser(placeholder.Id, real.Id));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(done, Does.Contain($"user {placeholder.Id}").And.Contain($"user {real.Id}"));
            Assert.That(notPlaceholder!.Message, Does.Contain("placeholder"));
            Assert.That(missing!.Message, Does.Contain("987654"));
            Assert.That(conflict!.Message, Is.EqualTo("Both are on a team in Winter"));
            Assert.That(player!.Message, Does.StartWith("Only a"));
        }
        await _userMerges.Received(2).Merge(real.Id, placeholder.Id, Arg.Is<MergeOptions>(o => o.Trigger == GrifballWebApp.Domain.Players.MergeTrigger.DuplicateSuggestion), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DismissDuplicateUser_RemembersThePair_AndAUserIsNoDuplicateOfItself()
    {
        var a = GrifballWebApp.Domain.Players.User.Register("A");
        var b = GrifballWebApp.Domain.Players.User.Register("B");
        Db.Users.AddRange(a, b);
        await Db.SaveChangesAsync();

        var done = await Sysadmins(Sysadmin).DismissDuplicateUser(a.Id, b.Id);
        var again = await Sysadmins(Sysadmin).DismissDuplicateUser(b.Id, a.Id);
        var same = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).DismissDuplicateUser(a.Id, a.Id));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(done, Does.Contain("Dismissed"));
            Assert.That(again, Does.Contain("Dismissed"));
            Assert.That(same!.Message, Does.Contain("no duplicate of itself"));
            Assert.That(await Db.DuplicateDismissals.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task MergeUsers_MergesWithTheNamedTransfers_AsTheCaller_AndRefusesWhatTheMergeRefuses()
    {
        var done = await Sysadmins(Sysadmin).MergeUsers(10, 11, transferDiscord: false, deleteMergeFrom: false);
        _userMerges.Merge(10, 12, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(_ => throw new UserNotFoundException(12));
        _userMerges.Merge(10, 13, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(_ => throw new MergeDeniedToAccessTokenException());
        _userMerges.Merge(10, 14, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(_ => throw new ArgumentException("A user can't be merged into itself"));
        _userMerges.Merge(10, 15, Arg.Any<MergeOptions>(), Arg.Any<CancellationToken>()).Returns(_ => throw new DbUpdateException("conflict"));
        var missing = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeUsers(10, 12));
        var token = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeUsers(10, 13));
        var argument = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeUsers(10, 14));
        var data = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).MergeUsers(10, 15));
        var player = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Player).MergeUsers(10, 11));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(done, Does.Contain("user 11 into user 10"));
            Assert.That(missing!.Message, Does.Contain("12"));
            Assert.That(token!.Message, Does.Contain("access token"));
            Assert.That(argument!.Message, Does.Contain("itself"));
            Assert.That(data!.Message, Does.Contain("nothing was changed"));
            Assert.That(player!.Message, Does.StartWith("Only a"));
        }
        await _userMerges.Received(1).Merge(10, 11,
            Arg.Is<MergeOptions>(o => !o.TransferDiscord && o.TransferLogins && o.TransferXbox && !o.DeleteMergeFrom && o.Trigger == GrifballWebApp.Domain.Players.MergeTrigger.Manual && o.TriggeredByUserID == 2),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UndoUserMerge_RefusesAMergeThatIsntThere_AndNonSysadmins()
    {
        var missing = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Sysadmin).UndoUserMerge(987654, "oops"));
        var player = await Assert.ThrowsAsync<ToolException>(() => Sysadmins(Player).UndoUserMerge(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(missing!.Message, Does.Contain("There is no merge 987654"));
            Assert.That(player!.Message, Does.StartWith("Only a"));
        }
    }

    [Test]
    public async Task ListDuplicateUsers_AndListUserMerges_AreForSysadmins_AndReadTheSitesLists()
    {
        var pairs = await Staff(Sysadmin).ListDuplicateUsers();
        var merges = await Staff(Sysadmin).ListUserMerges(page: 0, pageSize: 5000);
        var refusedPairs = await Assert.ThrowsAsync<ToolException>(() => Staff(Commissioner).ListDuplicateUsers());
        var refusedMerges = await Assert.ThrowsAsync<ToolException>(() => Staff(Player).ListUserMerges());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pairs, Is.Empty);
            Assert.That(merges.TotalCount, Is.Zero);
            Assert.That(refusedPairs!.Message, Does.StartWith("Only a"));
            Assert.That(refusedMerges!.Message, Does.StartWith("Only a"));
        }
    }
}
