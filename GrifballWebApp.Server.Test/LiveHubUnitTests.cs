using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Live;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// LiveHub and what is around it: who may watch which topics, the changes sent once a topic to its watchers, and who is
/// on the site, a hidden season's pages and the staff's shown as the staff area alone.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class LiveHubUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly IHubContext<LiveHub, ILiveHubClient> _hubContext = Substitute.For<IHubContext<LiveHub, ILiveHubClient>>();

    /// <summary>A season starting in 40 days and announced in 10: the public may not see it yet.</summary>
    private async Task<Season> HiddenSeason()
    {
        var season = Season.Seed(seasonName: "Secret").WithDates(signupsOpen: Now.AddDays(20), signupsClose: Now.AddDays(25),
            draftStart: Now.AddDays(30), seasonStart: Now.AddDays(40), seasonEnd: Now.AddDays(90), publicAt: Now.AddDays(10));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    private PresenceTracker Tracker(IPresenceStore? store = null)
        => new(store ?? new MemoryPresenceStore(), _hubContext, Substitute.For<IServiceScopeFactory>(), TimeProvider.System, NullLogger<PresenceTracker>.Instance);

    private (LiveHub Hub, IGroupManager Groups) Hub(params string[] roles)
    {
        var user = new HubUser();
        user.Set(new HubUserState(roles.Length == 0 ? null : 1, roles));
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn");
        context.Items.Returns(new Dictionary<object, object?>());
        var groups = Substitute.For<IGroupManager>();
        var visibility = new SeasonVisibility(Db);
        var hub = new LiveHub(new LiveTopicAccess(visibility, Db), user, Tracker(), new PresencePages(visibility, Db)) { Context = context, Groups = groups };
        return (hub, groups);
    }

    [Test]
    public async Task Watch_JoinsTheTopicsTheCallerMaySee_AndNamesTheRest()
    {
        var hidden = await HiddenSeason();
        var shown = await AddSeason("Shown");
        var (hub, groups) = Hub();

        var refused = await hub.Watch([LiveTopics.Seasons, LiveTopics.Season(hidden.SeasonID), LiveTopics.StaffUsers, LiveTopics.Season(shown.SeasonID)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(new[] { LiveTopics.Season(hidden.SeasonID), LiveTopics.StaffUsers }));
            await groups.Received(1).AddToGroupAsync("conn", LiveTopics.Seasons, Arg.Any<CancellationToken>());
            await groups.Received(1).AddToGroupAsync("conn", LiveTopics.Season(shown.SeasonID), Arg.Any<CancellationToken>());
            await groups.DidNotReceive().AddToGroupAsync("conn", LiveTopics.Season(hidden.SeasonID), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Watch_AHiddenSeason_IsStaffs()
    {
        var hidden = await HiddenSeason();
        var (hub, groups) = Hub(SiteRoles.Commissioner);

        var refused = await hub.Watch([LiveTopics.Draft(hidden.SeasonID), LiveTopics.StaffReschedules]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.Empty);
            await groups.Received(2).AddToGroupAsync("conn", Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Watch_JoinsATopicOnce_UpToTheMost_AndUnwatchLeavesOnlyWhatItWatched()
    {
        var (hub, groups) = Hub();
        var many = Enumerable.Range(1, LiveHub.MaxWatched + 1).Select(LiveTopics.Player).ToList();

        await hub.Watch([LiveTopics.Queue]);
        var refused = await hub.Watch([LiveTopics.Queue, .. many]);
        await hub.Unwatch([LiveTopics.Queue, LiveTopics.LateLeague]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(new[] { LiveTopics.Player(LiveHub.MaxWatched), LiveTopics.Player(LiveHub.MaxWatched + 1) }));
            await groups.Received(1).AddToGroupAsync("conn", LiveTopics.Queue, Arg.Any<CancellationToken>());
            await groups.Received(1).RemoveFromGroupAsync("conn", LiveTopics.Queue, Arg.Any<CancellationToken>());
            await groups.DidNotReceive().RemoveFromGroupAsync("conn", LiveTopics.LateLeague, Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task LiveUpdates_SendsATopicOnce_ToItsWatchers_AndAUsersToThemAlone()
    {
        var watchers = Substitute.For<ILiveHubClient>();
        var player = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Group(LiveTopics.Season(5)).Returns(watchers);
        _hubContext.Clients.User("7").Returns(player);
        var live = new LiveUpdates(_hubContext, NullLogger<LiveUpdates>.Instance);

        live.Publish([new LiveChange(LiveTopics.Season(5)), new LiveChange(LiveTopics.MyQueue, 7)]);
        live.Publish([new LiveChange(LiveTopics.Season(5))]);
        await live.SendPendingAsync();
        await live.SendPendingAsync();

        using (Assert.EnterMultipleScope())
        {
            await watchers.Received(1).Changed(LiveTopics.Season(5));
            await player.Received(1).Changed(LiveTopics.MyQueue);
        }
    }

    [Test]
    public async Task LiveUpdates_AFailedSend_IsLogged_AndTheRestGo()
    {
        var failing = Substitute.For<ILiveHubClient>();
        failing.Changed(Arg.Any<string>()).Returns(Task.FromException(new HubException("down")));
        var fine = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Group("seasons").Returns(failing);
        _hubContext.Clients.Group("queue").Returns(fine);
        var live = new LiveUpdates(_hubContext, NullLogger<LiveUpdates>.Instance);

        live.Publish([new LiveChange("seasons"), new LiveChange("queue")]);
        await live.SendPendingAsync();

        await fine.Received(1).Changed("queue");
    }

    [TestCase("/usermanagement/merges")]
    [TestCase("/seasonManager")]
    [TestCase("/commissioner-dashboard")]
    [TestCase("/season/1/playergrades")]
    [TestCase("/season/1/signups")]
    public async Task AStaffPage_IsTheStaffArea(string path)
    {
        await AddSeason("Shown");
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        Assert.That(await pages.Shown(path), Is.EqualTo(PresenceSnapshot.StaffArea));
    }

    [Test]
    public async Task AHiddenSeasonsPages_AndItsMatches_AreTheStaffArea_APublicOnesThemselves()
    {
        var hidden = await HiddenSeason();
        var match = await AddSeasonMatch(hidden, await AddTeam(hidden, "Home"), await AddTeam(hidden, "Away"));
        var shown = await AddSeason("Shown");
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await pages.Shown($"/season/{hidden.SeasonID}/draft"), Is.EqualTo(PresenceSnapshot.StaffArea));
            Assert.That(await pages.Shown($"/seasonmatch/{match.SeasonMatchID}"), Is.EqualTo(PresenceSnapshot.StaffArea));
            Assert.That(await pages.Shown("/seasonmatch/99999"), Is.EqualTo(PresenceSnapshot.StaffArea), "no such match");
            Assert.That(await pages.Shown($"/season/{shown.SeasonID}/draft"), Is.EqualTo($"/season/{shown.SeasonID}/draft"));
        }
    }

    [TestCase("/seasons?page=2#top", "/seasons")]
    [TestCase("/leaderboards/", "/leaderboards")]
    [TestCase("", "/")]
    [TestCase(null, "/")]
    [TestCase("seasons", "/")]
    [TestCase("/<script>", "/")]
    [TestCase("/profile/1 OR 1", "/")]
    public void Route_IsThePathAlone_OrHomeForAnythingElse(string? path, string expected)
        => Assert.That(PresencePages.Route(path), Is.EqualTo(expected));

    [Test]
    public void Route_PastItsLength_IsHome()
        => Assert.That(PresencePages.Route("/" + new string('a', PresencePages.MaxLength)), Is.EqualTo("/"));

    [Test]
    public async Task TheTracker_SendsWhoIsOn_OnlyWhenItChanged()
    {
        var everyone = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.All.Returns(everyone);
        var tracker = Tracker();

        await tracker.Viewing("a", null, "/seasons");
        await tracker.Viewing("b", null, "/seasons");
        await tracker.SendAsync();
        await tracker.SendAsync();
        tracker.Left("a");
        await tracker.SendAsync();

        using (Assert.EnterMultipleScope())
        {
            await everyone.Received(1).Presence(Arg.Is<PresenceSnapshot>(s => s.Online == 2 && s.Guests == 2));
            await everyone.Received(1).Presence(Arg.Is<PresenceSnapshot>(s => s.Online == 1));
            Assert.That(tracker.Current.Pages.Single().Path, Is.EqualTo("/seasons"));
        }
    }

    [Test]
    public async Task TheTracker_AStoreThatFails_IsLogged_NotThrown()
    {
        var everyone = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.All.Returns(everyone);
        var store = Substitute.For<IPresenceStore>();
        store.Exchange(Arg.Any<IReadOnlyList<PresenceTab>>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<PresenceTab>>(_ => throw new InvalidOperationException("down"));
        var tracker = Tracker(store);

        await tracker.Viewing("a", null, "/");
        await tracker.SendAsync();

        await everyone.DidNotReceive().Presence(Arg.Any<PresenceSnapshot>());
    }
}
