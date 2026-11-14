using System.Security.Claims;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Teams.Handlers;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A season not announced yet on the server's side (#435): the site's filter, the draft hub's staff group and its broadcasts,
/// the Discord caller's viewer, the commands' typed lookups and the team tools. On a temp-file SQLite GrifballContext.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonVisibilityServerUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = DateTime.UtcNow;

    /// <summary>A season starting in 40 days, announced in 10, with teams Hidden Home and Hidden Away and a match between them.</summary>
    private async Task<(Season Season, Team Team, SeasonMatch Match)> Hidden()
    {
        var season = Season.Seed(seasonName: "Secret").WithDates(signupsOpen: Now.AddDays(20), signupsClose: Now.AddDays(25),
            draftStart: Now.AddDays(30), seasonStart: Now.AddDays(40), seasonEnd: Now.AddDays(90), publicAt: Now.AddDays(10));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var home = await AddTeam(season, "Hidden Home");
        var match = await AddSeasonMatch(season, home, await AddTeam(season, "Hidden Away"));
        return (season, home, match);
    }

    private sealed class AnyController : ControllerBase;

    private static ClaimsPrincipal As(params string[] roles)
        => new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), roles.Length == 0 ? null : "test"));

    /// <summary>The filter run on an action with <paramref name="arguments"/>, for <paramref name="user"/>: its result, and whether the action ran.</summary>
    private async Task<(IActionResult? Result, bool Ran)> Filter(ClaimsPrincipal user, Dictionary<string, object?> arguments)
    {
        var http = new DefaultHttpContext { User = user };
        var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], arguments, new AnyController());
        var ran = false;
        await new SeasonVisibilityFilter(new SeasonVisibility(Db)).OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });
        return (context.Result, ran);
    }

    [Test]
    public async Task TheFilter_AnswersAHiddenTeamOrMatch_AsNoneThere_ToThePublic()
    {
        var (_, team, match) = await Hidden();

        var teamResult = await Filter(As(), new() { ["teamID"] = team.TeamID });
        var matchResult = await Filter(As(), new() { ["seasonMatchID"] = match.SeasonMatchID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teamResult.Ran, Is.False);
            Assert.That(((ProblemDetails)((ObjectResult)teamResult.Result!).Value!).Detail, Is.EqualTo(SeasonVisibilityFilter.NoSuchTeam));
            Assert.That(matchResult.Ran, Is.False);
            Assert.That(((ProblemDetails)((ObjectResult)matchResult.Result!).Value!).Detail, Is.EqualTo(SeasonVisibilityFilter.NoSuchSeasonMatch));
        }
    }

    [Test]
    public async Task TheFilter_LetsStaff_AndOtherArguments_Through()
    {
        var (season, team, _) = await Hidden();

        var staff = await Filter(As(SiteRoles.Commissioner), new() { ["seasonID"] = season.SeasonID, ["teamID"] = team.TeamID });
        var other = await Filter(As(), new() { ["userID"] = 5, ["name"] = "Secret" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(staff.Ran, Is.True);
            Assert.That(other.Ran, Is.True, "a user's ID or a string names no season");
        }
    }

    [Test]
    public async Task TheDraftHub_PutsStaffsConnections_InTheStaffGroup_AndNoOneElses()
    {
        async Task<IGroupManager> Connect(ClaimsPrincipal user)
        {
            var groups = Substitute.For<IGroupManager>();
            var context = Substitute.For<HubCallerContext>();
            context.User.Returns(user);
            context.ConnectionId.Returns("conn");
            var hub = new TeamsHub(null!, new SeasonVisibility(Db)) { Context = context, Groups = groups };
            await hub.OnConnectedAsync();
            return groups;
        }

        var staff = await Connect(As(SiteRoles.Sysadmin));
        var player = await Connect(As(SiteRoles.Player));

        await staff.Received(1).AddToGroupAsync("conn", TeamsHub.StaffGroup, Arg.Any<CancellationToken>());
        await player.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Test]
    public async Task AChangeToAHiddenSeason_MadeWithoutAConnection_GoesToTheWholeStaffGroup()
    {
        var (season, _, _) = await Hidden();
        var hub = Substitute.For<IHubContext<TeamsHub, ITeamsHubClient>>();
        var staff = Substitute.For<ITeamsHubClient>();
        hub.Clients.Group(TeamsHub.StaffGroup).Returns(staff);

        var scopes = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddSingleton(new SeasonVisibility(Db)).BuildServiceProvider()
            .GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        await new LockChangeHandler(hub, scopes).Handle(new LockChanged(true, season.SeasonID, null), CancellationToken.None);

        await staff.Received(1).LockCaptains(season.SeasonID);
        _ = hub.Clients.DidNotReceive().All;
    }

    [Test]
    public async Task ADiscordUsersViewer_IsTheirLinkedAccountsRoles()
    {
        await AddUser(5, "Commish", SiteRoles.Commissioner);
        await AddUser(6, "Grunt", SiteRoles.Player);
        var caller = new DiscordCaller(Db);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await caller.Viewer(5), Is.EqualTo(SeasonViewer.Staff));
            Assert.That(await caller.Viewer(6), Is.EqualTo(SeasonViewer.Public));
            Assert.That(await caller.Viewer(999), Is.EqualTo(SeasonViewer.Public), "no linked account");
        }
    }

    [Test]
    public async Task ATypedTeamOrTeamsMatch_OfAHiddenSeason_IsFoundForStaffAlone()
    {
        var (_, team, match) = await Hidden();
        var lookups = new CommandLookups(Db, new SeasonService(Db, new SeasonStatsService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)), new XboxAccountService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await lookups.Team("Hidden Home"), Is.Null);
            Assert.That(await lookups.Team("Hidden Home", SeasonViewer.Staff), Is.EqualTo((team.SeasonID, team.TeamID)));
            Assert.That(await lookups.SeasonMatch("Hidden Home"), Is.Null);
            Assert.That(await lookups.SeasonMatch("Hidden Home", SeasonViewer.Staff), Is.EqualTo(match.SeasonMatchID));
        }
    }

    [Test]
    public async Task TheTeamTools_RefuseAHiddenTeam_ToThePublic_AndShowItToStaff()
    {
        var (_, team, _) = await Hidden();
        TeamTools As(ToolCaller caller) => new(caller, new SeasonVisibility(Db), new TeamPageService(Db), new TeamHistoryService(Db, new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)),
            new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!));
        var everyone = As(ToolCaller.Anonymous);
        var staff = As(new ToolCaller(1, [SiteRoles.Commissioner]));

        var refusals = new[]
        {
            await Assert.ThrowsAsync<ToolException>(() => everyone.GetTeam(team.TeamID)),
            await Assert.ThrowsAsync<ToolException>(() => everyone.GetTeamMatches(team.TeamID)),
            await Assert.ThrowsAsync<ToolException>(() => everyone.GetTeamHistory(team.TeamID)),
            await Assert.ThrowsAsync<ToolException>(() => everyone.GetTeamVods(team.TeamID)),
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusals.Select(r => r!.Message), Has.All.StartWith($"There is no team {team.TeamID}"));
            Assert.That((await staff.GetTeam(team.TeamID)).TeamName, Is.EqualTo("Hidden Home"));
            Assert.That(await staff.GetTeamMatches(team.TeamID), Has.Length.EqualTo(1));
            Assert.That(await staff.GetTeamVods(team.TeamID), Is.Empty);
        }
    }
}
