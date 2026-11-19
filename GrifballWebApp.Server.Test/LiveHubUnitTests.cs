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
    private static readonly string[] PublicConnection = ["conn:Public"];
    private static readonly string[] StaffConnection = ["conn:Staff"];
    private static readonly string[] SevensConnection = ["seven"];
    private static readonly string[] WatchingConnection = ["conn"];

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

    private readonly LiveWatchers _watchers = new();
    private readonly HubConnections _connections = new();

    private LiveHub Hub(params string[] roles)
    {
        var user = new HubUser();
        user.Set(new HubUserState(roles.Length == 0 ? null : 1, roles));
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn");
        context.Items.Returns(new Dictionary<object, object?>());
        var visibility = new SeasonVisibility(Db);
        return new LiveHub(new LiveTopicAccess(visibility, Db), user, _watchers, Tracker(), new PresencePages(visibility, Db)) { Context = context };
    }

    private string[] Watching(string topic) => [.. _watchers.Of(topic).Select(w => $"{w.ConnectionID}:{w.Viewer}")];

    [Test]
    public async Task Watch_WatchesTheTopicsTheCallerMaySee_AndNamesTheRest()
    {
        var hidden = await HiddenSeason();
        var shown = await AddSeason("Shown");
        var hub = Hub();

        var refused = await hub.Watch([LiveTopics.Seasons, LiveTopics.Season(hidden.SeasonID), LiveTopics.StaffUsers, LiveTopics.Season(shown.SeasonID)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(new[] { LiveTopics.Season(hidden.SeasonID), LiveTopics.StaffUsers }));
            Assert.That(Watching(LiveTopics.Seasons), Is.EqualTo(PublicConnection));
            Assert.That(Watching(LiveTopics.Season(shown.SeasonID)), Is.EqualTo(PublicConnection));
            Assert.That(Watching(LiveTopics.Season(hidden.SeasonID)), Is.Empty);
        }
    }

    [Test]
    public async Task Watch_AHiddenSeason_IsStaffs_WatchingAsStaff()
    {
        var hidden = await HiddenSeason();
        var hub = Hub(SiteRoles.Commissioner);

        var refused = await hub.Watch([LiveTopics.Draft(hidden.SeasonID), LiveTopics.StaffReschedules]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.Empty);
            Assert.That(Watching(LiveTopics.Draft(hidden.SeasonID)), Is.EqualTo(StaffConnection));
        }
    }

    [Test]
    public async Task Watch_UpToTheMost_AndUnwatchAndLeaving_StopWatching()
    {
        var hub = Hub();
        var many = Enumerable.Range(1, LiveHub.MaxWatched + 1).Select(LiveTopics.Player).ToList();

        await hub.Watch([LiveTopics.Queue]);
        var refused = await hub.Watch([LiveTopics.Queue, .. many]);
        await hub.Unwatch([LiveTopics.Queue, LiveTopics.LateLeague]);
        var afterUnwatch = Watching(LiveTopics.Queue);
        await hub.OnDisconnectedAsync(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(new[] { LiveTopics.Player(LiveHub.MaxWatched), LiveTopics.Player(LiveHub.MaxWatched + 1) }));
            Assert.That(afterUnwatch, Is.Empty);
            Assert.That(Watching(LiveTopics.Player(1)), Is.Empty, "gone with the connection");
        }
    }

    [Test]
    public async Task LiveUpdates_SendsAUsersToTheirConnectionsHere_AndTheTopicsOnceToThisProcesssPages()
    {
        var player = Substitute.For<ILiveHubClient>();
        var watchers = Substitute.For<ILiveHubClient>();
        var seven = Substitute.For<HubCallerContext>();
        seven.ConnectionId.Returns("seven");
        _connections.Add(seven, new HubUserState(7, []));
        _hubContext.Clients.Clients(Arg.Is<IReadOnlyList<string>>(c => c.SequenceEqual(SevensConnection))).Returns(player);
        _hubContext.Clients.Clients(Arg.Is<IReadOnlyList<string>>(c => c.SequenceEqual(WatchingConnection))).Returns(watchers);
        _watchers.Watch(LiveTopics.Player(5), "conn", SeasonViewer.Public);
        var live = new LiveUpdates(NullLogger<LiveUpdates>.Instance, delivery: Delivery(Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!)));

        live.Publish([new LiveChange(LiveTopics.Player(5)), new LiveChange(LiveTopics.MyQueue, 7)]);
        live.Publish([new LiveChange(LiveTopics.Player(5))]);
        await live.SendPendingAsync();
        await live.SendPendingAsync();

        using (Assert.EnterMultipleScope())
        {
            await player.Received(1).Changed(LiveTopics.MyQueue);
            await watchers.Received(1).Changed(LiveTopics.Player(5));
        }
    }

    [TestCase(null, ExpectedResult = 0)]
    [TestCase("not json", ExpectedResult = 0)]
    [TestCase("[{\"Topic\":\"season:1\",\"UserID\":null},{\"Topic\":\"queue:me\",\"UserID\":7}]", ExpectedResult = 2)]
    public int TheRelaysMessage_IsTheChanges(string? message) => LiveUpdates.Parse(message).Count;

    private LiveDelivery Delivery(LiveSnapshots snapshots)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => snapshots);
        return new LiveDelivery(_watchers, _connections, _hubContext, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<LiveDelivery>.Instance);
    }

    [Test]
    public async Task Delivery_BuildsATopicsDataOnce_ForAllItsWatchers_AndNothingForATopicNobodyWatches()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(new QueueLiveDto(3));
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Is<IReadOnlyList<string>>(c => c.Count == 2)).Returns(watchers);
        _watchers.Watch(LiveTopics.Queue, "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Queue, "b", SeasonViewer.Staff);

        await Delivery(snapshots).Deliver([LiveTopics.Queue, LiveTopics.Draft(9)]);

        using (Assert.EnterMultipleScope())
        {
            await watchers.Received(1).Snapshot(LiveTopics.Queue, "{\"count\":3}");
            await snapshots.Received(1).For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>());
            await snapshots.DidNotReceive().For(LiveTopics.Draft(9), Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Delivery_BuildsTheHomePagesData_OnceAViewer()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Seasons, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(new SeasonsLiveDto([]));
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(Substitute.For<ILiveHubClient>());
        _watchers.Watch(LiveTopics.Seasons, "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Seasons, "b", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Seasons, "c", SeasonViewer.Staff);

        await Delivery(snapshots).Deliver([LiveTopics.Seasons]);

        using (Assert.EnterMultipleScope())
        {
            await snapshots.Received(1).For(LiveTopics.Seasons, SeasonViewer.Public, Arg.Any<CancellationToken>());
            await snapshots.Received(1).For(LiveTopics.Seasons, SeasonViewer.Staff, Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Delivery_ANoticeTopic_OrDataThatFailsToBuild_TellsItsPagesToReadAgain()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Draft(4), Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns<object?>(_ => throw new InvalidOperationException("down"));
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Draft(4), "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Player(2), "a", SeasonViewer.Public);

        await Delivery(snapshots).Deliver([LiveTopics.Draft(4), LiveTopics.Player(2)]);

        using (Assert.EnterMultipleScope())
        {
            await watchers.Received(1).Changed(LiveTopics.Draft(4));
            await watchers.Received(1).Changed(LiveTopics.Player(2));
            await watchers.DidNotReceive().Snapshot(Arg.Any<string>(), Arg.Any<string>());
        }
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
