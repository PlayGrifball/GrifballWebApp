using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.Profile;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

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
        "There is no season 999999: list_seasons has their IDs.",
        "Reschedule request not found",
    ];

    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();

    private SeasonVisibility Visibility() => new(Db);

    private StaffTools Staff(ToolCaller caller)
    {
        var reschedules = new RescheduleService(Db, Substitute.For<IOutbox>());
        return new(caller, new CommissionerDashboardService(reschedules, new EventOrganizerService(Db)), Claims());
    }

    private GamertagClaimService Claims() => new(Db, Substitute.For<IUserMergeService>(), null);

    private CommissionerTools Commissioners(ToolCaller caller) => new(caller, new SeasonService(Db, new SeasonStatsService(Db), Visibility()), Visibility(),
        new RescheduleService(Db, Substitute.For<IOutbox>()),
        new SeasonMatchService(Db, _dataPull, Substitute.For<IBracketService>(), new ChampionshipService(Db)),
        new MatchPlannerService(Db), Substitute.For<IBracketService>(),
        new TeamService(Db, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), Substitute.For<IDatabaseLocks>()));

    private SysadminTools Sysadmins(ToolCaller caller) => new(caller, Claims(), _dataPull);

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
        var tools = Sysadmins(Sysadmin);

        var pulled = await tools.PullHaloGame(game);
        var medals = await tools.RefreshMedals();
        var refused = await Assert.ThrowsAsync<ToolException>(() => tools.PullHaloGame(Guid.Empty));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pulled, Is.EqualTo("Pulled: get_game shows it."));
            Assert.That(medals, Is.EqualTo("The medals are refreshed."));
            Assert.That(refused!.Message, Is.EqualTo("That isn't a game's match ID."));
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
}
