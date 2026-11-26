using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Live;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private static readonly string[] BothTabs = ["a", "b"];
    private static readonly string[] OnlyB = ["b"];
    private static readonly string[] Reds = ["Reds"];
    private static readonly string[] OldShapeTopics = ["season:1", "queue:me"];

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
    public async Task ASignedInConnection_IsItsUsers_UntilItLeaves_AndAGuestsIsNobodys()
    {
        var signedIn = Hub(SiteRoles.Player);
        var guest = Hub();

        await signedIn.OnConnectedAsync();
        await guest.OnConnectedAsync();
        var whileThere = _watchers.OfUser(1);
        await signedIn.OnDisconnectedAsync(null);
        await guest.OnDisconnectedAsync(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileThere, Is.EqualTo(WatchingConnection));
            Assert.That(_watchers.OfUser(1), Is.Empty);
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
        // Seven's live connection, and their chat connection (another hub's: never sent LiveHub's messages)
        _watchers.Connected("seven", 7);
        var chat = Substitute.For<HubCallerContext>();
        chat.ConnectionId.Returns("seven-chat");
        _connections.Add(chat, new HubUserState(7, []));
        _hubContext.Clients.Clients(Arg.Is<IReadOnlyList<string>>(c => c.SequenceEqual(SevensConnection))).Returns(player);
        _hubContext.Clients.Clients(Arg.Is<IReadOnlyList<string>>(c => c.SequenceEqual(WatchingConnection))).Returns(watchers);
        _watchers.Watch(LiveTopics.Player(5), "conn", SeasonViewer.Public);
        var live = new LiveUpdates(NullLogger<LiveUpdates>.Instance, delivery: Delivery(Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!)));

        live.Publish([new LiveChange(LiveTopics.Player(5)), new LiveChange(LiveTopics.MyQueue, 7)]);
        live.Publish([new LiveChange(LiveTopics.Player(5))]);
        await live.SendPendingAsync();
        await live.SendPendingAsync();

        using (Assert.EnterMultipleScope())
        {
            await player.Received(1).Changed(LiveTopics.MyQueue);
            await watchers.Received(1).Changed(LiveTopics.Player(5));
            _ = _hubContext.Clients.DidNotReceive().Clients(Arg.Is<IReadOnlyList<string>>(c => c.Contains("seven-chat")));
        }
    }

    [TestCase(null, ExpectedResult = 0)]
    [TestCase("not json", ExpectedResult = 0)]
    [TestCase("null", ExpectedResult = 0)]
    [TestCase("{\"ChangeID\":\"5b3b2b1a-0000-0000-0000-000000000001\",\"Changes\":[{\"Topic\":\"season:1\",\"UserID\":null},{\"Topic\":\"queue:me\",\"UserID\":7}]}", ExpectedResult = 2)]
    public int TheRelaysMessage_IsTheChanges(string? message) => LiveUpdates.Parse(message).Count;

    [Test]
    public void ChangeIDOf_IsThisPublishsOwnID_TheSameOnEveryPod_AndDifferentFromAnotherPublishs()
    {
        var id = Guid.NewGuid();
        var message = LiveUpdates.Serialize(id, [new LiveChange(LiveTopics.Queue)]);
        var otherMessage = LiveUpdates.Serialize(Guid.NewGuid(), [new LiveChange(LiveTopics.Queue)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(LiveUpdates.ChangeIDOf(message), Is.EqualTo(id));
            Assert.That(LiveUpdates.ChangeIDOf(otherMessage), Is.Not.EqualTo(id));
            Assert.That(LiveUpdates.ChangeIDOf("not json"), Is.Null);
            Assert.That(LiveUpdates.Parse(message).Select(c => c.Topic), Is.EqualTo(new[] { LiveTopics.Queue }));
        }
    }

    [Test]
    public void ParseRelayed_AlsoAcceptsTheOlderPlainArrayShape_WithNoChangeIDToShareABuildBy()
    {
        // The shape before #540 (and what a worker still on an older image would publish mid-rollout): just the
        // changes, no envelope around them
        const string oldShapeMessage = "[{\"Topic\":\"season:1\",\"UserID\":null},{\"Topic\":\"queue:me\",\"UserID\":7}]";

        var relayed = LiveUpdates.ParseRelayed(oldShapeMessage);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(relayed, Is.Not.Null);
            Assert.That(relayed!.ChangeID, Is.Null, "no change ID: it always builds its own, never shares a cached build");
            Assert.That(relayed.Changes.Select(c => c.Topic), Is.EqualTo(OldShapeTopics));
        }
    }

    [TestCase("not json")]
    [TestCase("42")]
    [TestCase("true")]
    public void ParseRelayed_IsNull_ForAMessageThatIsNeitherShape(string garbled)
        => Assert.That(LiveUpdates.ParseRelayed(garbled), Is.Null);

    private LiveDelivery Delivery(LiveSnapshots snapshots)
    {
        var connectionString = Db.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddScoped(_ => snapshots);
        services.AddScoped<IApplicationDbContext>(_ => new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, connectionString).Options));
        return new LiveDelivery(_watchers, _hubContext, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<LiveDelivery>.Instance,
            new ConfigurationBuilder().Build());
    }

    private static LiveSnapshots NoSnapshots() => Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);

    /// <summary>The teams page's and team pages' data, on the app's context (their queries translated for SQLite as the app's are).</summary>
    private LiveSnapshots TeamSnapshots()
        => new(null!, new SeasonService(Db, new GrifballWebApp.Application.Stats.SeasonStatsService(Db), new SeasonVisibility(Db)), null!, null!, null!, null!, null!,
            new GrifballWebApp.Application.TeamPage.TeamPageService(Db));

    [Test]
    public async Task ATeamsPagesData_IsTheSeasonsBanner_AndItsTeams()
    {
        var season = await AddSeason("Shown");
        await AddTeam(season, "Reds");

        var teams = (TeamsLiveDto?)await TeamSnapshots().For(LiveTopics.Teams(season.SeasonID), SeasonViewer.Public);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams!.SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(teams.Summary, Is.Not.Null);
            Assert.That(teams.Teams!.Select(t => t.TeamName), Is.EqualTo(Reds));
        }
    }

    [Test]
    public async Task ATeamsData_IsItsPage_ItsMatches_AndItsPlayersStats_AndNoneOnceItIsGone()
    {
        var season = await AddSeason("Shown");
        var reds = await AddTeam(season, "Reds");
        var snapshots = TeamSnapshots();

        var team = (TeamLiveDto?)await snapshots.For(LiveTopics.Team(reds.TeamID), SeasonViewer.Public);
        var gone = (TeamLiveDto?)await snapshots.For(LiveTopics.Team(999), SeasonViewer.Public);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((team!.TeamID, team.Team!.TeamName), Is.EqualTo((reds.TeamID, "Reds")));
            Assert.That(team.Matches, Is.Empty);
            Assert.That(team.PlayerStats, Is.Empty);
            Assert.That(gone!.Team, Is.Null);
            Assert.That(gone.PlayerStats, Is.Empty);
        }
    }

    [Test]
    public async Task Delivery_AnXboxAccountsChange_TellsTheProfileWatchedHere_OfItsUser_AndNoneOfAnAccountNoUserHas()
    {
        var player = User.Seed(userName: "hundred", xboxUser: XboxUser.FromXbox(100, "Hundred"));
        var other = User.Seed(userName: "other", xboxUser: XboxUser.FromXbox(300, "Other"));
        Db.Users.AddRange(player, other);
        await Db.SaveChangesAsync();
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Player(player.Id), "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Player(other.Id), "b", SeasonViewer.Public);
        _watchers.Watch("player:x", "c", SeasonViewer.Public); // no user: never looked up

        await Delivery(NoSnapshots()).Deliver([new LiveChange(LiveTopics.Xbox(100)), new LiveChange(LiveTopics.Xbox(200)), new LiveChange("xbox:x")]);

        using (Assert.EnterMultipleScope())
        {
            await watchers.Received(1).Changed(LiveTopics.Player(player.Id));
            await watchers.DidNotReceive().Changed(LiveTopics.Player(other.Id));
        }
    }

    /// <summary>Counts the scopes made: a lookup makes one.</summary>
    private sealed class CountedScopes : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        public int Made { get; private set; }

        public IServiceScope CreateScope()
        {
            Made++;
            return _inner.CreateScope();
        }
    }

    [Test]
    public async Task Delivery_AnXboxAccountsChange_WithNoProfileWatchedHere_LooksNobodyUp()
    {
        var scopes = new CountedScopes();
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Xbox(100), "a", SeasonViewer.Public);

        await new LiveDelivery(_watchers, _hubContext, scopes, NullLogger<LiveDelivery>.Instance, new ConfigurationBuilder().Build()).Deliver([new LiveChange(LiveTopics.Xbox(100))]);

        using (Assert.EnterMultipleScope())
        {
            await watchers.Received(1).Changed(LiveTopics.Xbox(100));
            Assert.That(scopes.Made, Is.Zero, "no lookup made");
        }
    }

    [Test]
    public async Task Delivery_ALookupOfWhoseAccountsChangedThatFails_StillTellsTheAccountsWatchers()
    {
        // Scopes with no database: the lookup throws, and is logged
        var scopes = new CountedScopes();
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Xbox(100), "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Player(5), "b", SeasonViewer.Public);

        await new LiveDelivery(_watchers, _hubContext, scopes, NullLogger<LiveDelivery>.Instance, new ConfigurationBuilder().Build()).Deliver([new LiveChange(LiveTopics.Xbox(100))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scopes.Made, Is.EqualTo(1), "a lookup tried");
            await watchers.Received(1).Changed(LiveTopics.Xbox(100));
            await watchers.DidNotReceive().Changed(LiveTopics.Player(5));
        }
    }

    [Test]
    public async Task Delivery_BuildsATopicsDataOnce_ForAllItsWatchers_AndNothingForATopicNobodyWatches()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Queue, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(new QueueLiveDto(3));
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Is<IReadOnlyList<string>>(c => c.Count == 2)).Returns(watchers);
        _watchers.Watch(LiveTopics.Queue, "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Queue, "b", SeasonViewer.Staff);

        await Delivery(snapshots).Deliver([new LiveChange(LiveTopics.Queue), new LiveChange(LiveTopics.Draft(9))]);

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
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Seasons, Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns(new SeasonsLiveDto([]));
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(Substitute.For<ILiveHubClient>());
        _watchers.Watch(LiveTopics.Seasons, "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Seasons, "b", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Seasons, "c", SeasonViewer.Staff);

        await Delivery(snapshots).Deliver([new LiveChange(LiveTopics.Seasons)]);

        using (Assert.EnterMultipleScope())
        {
            await snapshots.Received(1).For(LiveTopics.Seasons, SeasonViewer.Public, Arg.Any<CancellationToken>());
            await snapshots.Received(1).For(LiveTopics.Seasons, SeasonViewer.Staff, Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Delivery_ANoticeTopic_OrDataThatFailsToBuild_TellsItsPagesToReadAgain()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Draft(4), Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns<object?>(_ => throw new InvalidOperationException("down"));
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Draft(4), "a", SeasonViewer.Public);
        _watchers.Watch(LiveTopics.Player(2), "a", SeasonViewer.Public);

        await Delivery(snapshots).Deliver([new LiveChange(LiveTopics.Draft(4)), new LiveChange(LiveTopics.Player(2))]);

        using (Assert.EnterMultipleScope())
        {
            await watchers.Received(1).Changed(LiveTopics.Draft(4));
            await watchers.Received(1).Changed(LiveTopics.Player(2));
            await watchers.DidNotReceive().Snapshot(Arg.Any<string>(), Arg.Any<string>());
        }
    }

    [Test]
    public async Task Delivery_DataThereIsNoneOf_TellsItsPagesToReadAgain()
    {
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Draft(4), Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns((object?)null);
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Draft(4), "a", SeasonViewer.Public);

        await Delivery(snapshots).Deliver([new LiveChange(LiveTopics.Draft(4))]);

        await watchers.Received(1).Changed(LiveTopics.Draft(4));
    }

    [Test]
    public async Task Delivery_Canceled_StopsBuilding_RatherThanSendingANotice()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        var snapshots = Substitute.For<LiveSnapshots>(null!, null!, null!, null!, null!, null!, null!, null!);
        snapshots.For(LiveTopics.Draft(4), Arg.Any<SeasonViewer>(), Arg.Any<CancellationToken>()).Returns<object?>(_ => throw new OperationCanceledException(canceled.Token));
        var watchers = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(watchers);
        _watchers.Watch(LiveTopics.Draft(4), "a", SeasonViewer.Public);

        Assert.That(async () => await Delivery(snapshots).Deliver([new LiveChange(LiveTopics.Draft(4))], ct: canceled.Token), Throws.InstanceOf<OperationCanceledException>());
        await watchers.DidNotReceive().Changed(Arg.Any<string>());
    }

    [TestCase("/usermanagement/merges")]
    [TestCase("/seasonManager")]
    [TestCase("/commissioner-dashboard")]
    [TestCase("/sysadmin")]
    [TestCase("/sysadmin/jobs")]
    [TestCase("/sysadmin/databasebackup")]
    [TestCase("/infiniteclient")]
    [TestCase("/season/1/playergrades")]
    [TestCase("/season/1/signups")]
    public async Task AStaffPage_IsTheStaffArea(string path)
    {
        await AddSeason("Shown");
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        Assert.That(await pages.Shown(path), Is.EqualTo(PresenceSnapshot.StaffArea));
    }

    [Test]
    public async Task TheOldBackupAddress_IsNoStaffPage_ItMovedUnderSysAdmin()
    {
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        Assert.That(await pages.Shown("/databasebackup"), Is.Not.EqualTo(PresenceSnapshot.StaffArea));
    }

    [Test]
    public async Task AHiddenSeasonsPages_AndItsMatches_AreTheStaffArea_APublicOnesThemselves()
    {
        var hidden = await HiddenSeason();
        var match = await AddSeasonMatch(hidden, await AddTeam(hidden, "Home"), await AddTeam(hidden, "Away"));
        var shown = await AddSeason("Shown");
        var shownMatch = await AddSeasonMatch(shown, await AddTeam(shown, "Home"), await AddTeam(shown, "Away"));
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await pages.Shown($"/season/{hidden.SeasonID}/draft"), Is.EqualTo(PresenceSnapshot.StaffArea));
            Assert.That(await pages.Shown($"/seasonmatch/{match.SeasonMatchID}"), Is.EqualTo(PresenceSnapshot.StaffArea));
            Assert.That(await pages.Shown("/seasonmatch/99999"), Is.EqualTo(PresenceSnapshot.StaffArea), "no such match");
            Assert.That(await pages.Shown($"/season/{shown.SeasonID}/draft"), Is.EqualTo($"/season/{shown.SeasonID}/draft"));
            Assert.That(await pages.Shown($"/seasonmatch/{shownMatch.SeasonMatchID}"), Is.EqualTo($"/seasonmatch/{shownMatch.SeasonMatchID}"));
            Assert.That(await pages.Shown("/leaderboards"), Is.EqualTo("/leaderboards"));
        }
    }

    [Test]
    public async Task Subject_NamesWhoseOrWhichPageItIs()
    {
        var season = await AddSeason("Season 5");
        var reds = await AddTeam(season, "Reds");
        var match = await AddSeasonMatch(season, reds, await AddTeam(season, "Blues"));
        var game = GrifballWebApp.Domain.Halo.Match.Seed(Guid.NewGuid(), startTime: Now);
        Db.Matches.Add(game);
        match.AddGame(game, 1);
        var user = await AddUser(41, "Grunt Padre");
        var lateLeague = GrifballWebApp.Domain.LateLeague.MatchmakingSeason.Seed("Late League 8");
        Db.MatchmakingSeasons.Add(lateLeague);
        await Db.SaveChangesAsync();
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await pages.Subject($"/profile/{user.Id}"), Is.EqualTo("Grunt Padre"));
            Assert.That(await pages.Subject("/xbox/41"), Is.EqualTo("Grunt Padre"));
            Assert.That(await pages.Subject($"/season/{season.SeasonID}"), Is.EqualTo("Season 5"));
            Assert.That(await pages.Subject($"/season/{season.SeasonID}/draft"), Is.EqualTo("Season 5"));
            Assert.That(await pages.Subject($"/seasonAvailability/{season.SeasonID}"), Is.EqualTo("Season 5"));
            Assert.That(await pages.Subject($"/season/{season.SeasonID}/team/{reds.TeamID}"), Is.EqualTo("Reds (Season 5)"));
            Assert.That(await pages.Subject($"/history/team/{reds.TeamID}"), Is.EqualTo("Reds (Season 5)"));
            Assert.That(await pages.Subject($"/seasonmatch/{match.SeasonMatchID}"), Is.EqualTo("Reds vs Blues (Season 5)"));
            Assert.That(await pages.Subject($"/reschedule-request/{match.SeasonMatchID}"), Is.EqualTo("Reds vs Blues (Season 5)"));
            Assert.That(await pages.Subject($"/match/{game.MatchID}"), Is.EqualTo("Reds vs Blues (Season 5)"));
            Assert.That(await pages.Subject($"/late-league/{lateLeague.MatchmakingSeasonID}"), Is.EqualTo("Late League 8"));
            Assert.That(await pages.Subject("/profile/99999"), Is.Null, "no such user");
            Assert.That(await pages.Subject($"/match/{Guid.NewGuid()}"), Is.Null, "a game of no season match");
            Assert.That(await pages.Subject("/leaderboards"), Is.Null);
            Assert.That(await pages.Subject(PresenceSnapshot.StaffArea), Is.Null);
        }
    }

    [Test]
    public async Task Subject_NamesNothingOfAHiddenSeason()
    {
        var hidden = await HiddenSeason();
        var team = await AddTeam(hidden, "Home");
        var match = await AddSeasonMatch(hidden, team, await AddTeam(hidden, "Away"));
        var game = GrifballWebApp.Domain.Halo.Match.Seed(Guid.NewGuid(), startTime: Now);
        Db.Matches.Add(game);
        match.AddGame(game, 1);
        await Db.SaveChangesAsync();
        var pages = new PresencePages(new SeasonVisibility(Db), Db);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await pages.Subject($"/history/team/{team.TeamID}"), Is.Null);
            Assert.That(await pages.Subject($"/match/{game.MatchID}"), Is.Null);
            Assert.That(await pages.Subject($"/season/{hidden.SeasonID}"), Is.Null);
            Assert.That(await pages.Subject($"/seasonmatch/{match.SeasonMatchID}"), Is.Null);
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
    public async Task TheTracker_SendsWhoIsOn_OnlyWhenItChanged_ToThisProcesssTabsAlone()
    {
        var tabs = Substitute.For<ILiveHubClient>();
        var sentTo = new List<string[]>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(call =>
        {
            sentTo.Add([.. call.Arg<IReadOnlyList<string>>().Order(StringComparer.Ordinal)]);
            return tabs;
        });
        var tracker = Tracker();

        await tracker.Viewing("a", null, "/seasons");
        await tracker.Viewing("b", null, "/seasons");
        await tracker.SendAsync();
        await tracker.SendAsync();
        tracker.Left("a");
        await tracker.SendAsync();
        tracker.Left("b");
        await tracker.SendAsync();

        using (Assert.EnterMultipleScope())
        {
            await tabs.Received(1).Presence(Arg.Is<PresenceSnapshot>(s => s.Online == 2 && s.Guests == 2));
            await tabs.Received(1).Presence(Arg.Is<PresenceSnapshot>(s => s.Online == 1));
            Assert.That(sentTo, Is.EqualTo(new[] { BothTabs, OnlyB }), "each process tells its own tabs (none when it has none), not every process's through the backplane");
            _ = _hubContext.Clients.DidNotReceive().All;
            Assert.That(tracker.Current.Online, Is.Zero);
        }
    }

    [Test]
    public async Task TheTracker_AStoreThatFails_IsLogged_NotThrown()
    {
        var everyone = Substitute.For<ILiveHubClient>();
        _hubContext.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(everyone);
        var store = Substitute.For<IPresenceStore>();
        store.Exchange(Arg.Any<IReadOnlyList<PresenceTab>>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<PresenceTab>>(_ => throw new InvalidOperationException("down"));
        var tracker = Tracker(store);

        await tracker.Viewing("a", null, "/");
        await tracker.SendAsync();

        await everyone.DidNotReceive().Presence(Arg.Any<PresenceSnapshot>());
    }
}
