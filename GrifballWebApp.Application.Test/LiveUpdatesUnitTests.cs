using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The live updates' rules: which topics a domain event changes (LiveChanges), who may watch a topic (LiveTopicAccess:
/// a season not announced yet is staff's alone), and the dispatcher that runs the events' handlers.
/// </summary>
[TestFixture]
internal sealed class LiveUpdatesUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] NoRoles = [];
    private static readonly string[] Commissioner = [SiteRoles.Commissioner];
    private static readonly string[] PlayerRole = [SiteRoles.Player];
    private static readonly string[] ADraftPick = ["season:5", "draft:5", "player:7"];
    private static readonly string[] AResult = ["seasons", "season:5", "seasonmatch:11"];
    private static readonly string[] ASpot = ["queue", "queue:me@7"];
    private static readonly string[] HandlersInOrder = ["failing", "own", "every", "failing", "own", "every"];
    private static readonly string[] MembersByName = ["amy", "Zed"];
    private static readonly string[] ATeam = ["season:5", "draft:5"];
    private static readonly string[] OnlySeasons = ["seasons"];
    private static readonly string[] PushedTopics = ["seasons", "season:1", "draft:1", "queue"];
    private FakeTimeProvider _time = null!;

    [SetUp]
    public void SetUp() => _time = new FakeTimeProvider(Now);

    private LiveTopicAccess Access() => new(new SeasonVisibility(Context, _time), Context);

    /// <summary>A season that has started (public), or one starting in 40 days and not announced until 10 days from now.</summary>
    private async Task<Season> AddSeason(int id, bool announced)
    {
        var start = announced ? Now.AddDays(-5) : Now.AddDays(40);
        var season = Season.Seed(seasonName: $"Season {id}", seasonID: id);
        season.Schedule(start.AddDays(-30), start.AddDays(-20), start.AddDays(-10), start, start.AddDays(30));
        season.AnnounceAt(announced ? null : Now.AddDays(10));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season;
    }

    private static string[] Topics(IDomainEvent domainEvent) => [.. LiveChanges.Of(domainEvent).Select(c => c.UserID is int u ? $"{c.Topic}@{u}" : c.Topic)];

    [Test]
    public void ADraftPick_ChangesTheSeason_ItsDraft_AndThePlayersProfile()
    {
        var team = Team.Seed("Reds", seasonID: 5, teamID: 3);

        Assert.That(Topics(new TeamRosterChanged(team, [7])), Is.EquivalentTo(ADraftPick));
    }

    [Test]
    public void AResult_ChangesTheSeasons_TheSeason_AndTheMatchsPage()
    {
        var match = SeasonMatch.Seed(5, 1, 2);
        match.SeasonMatchID = 11;

        Assert.That(Topics(new SeasonMatchResultChanged(match)), Is.EquivalentTo(AResult));
    }

    [Test]
    public void AQueueSpot_ChangesTheQueuesSize_ForEveryone_AndThePlayersOwnPlace_ForThemAlone()
    {
        var spot = QueuedPlayer.Join(7, hasGamertag: true, inMatch: false, inQueue: false, Now).Spot!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Topics(new PlayerQueued(spot)), Is.EquivalentTo(ASpot));
            Assert.That(Topics(new PlayerLeftQueue(spot)), Is.EquivalentTo(ASpot));
        }
    }

    [Test]
    public void AnEventNoPageShows_ChangesNothing()
        => Assert.That(LiveChanges.Of(Substitute.For<IDomainEvent>()), Is.Empty);

    [TestCase("seasons")]
    [TestCase("lateleague")]
    [TestCase("queue")]
    [TestCase("player:7")]
    [TestCase("xbox:2533274800000000")]
    [TestCase("match:0f8fad5b-d9cb-469f-a165-70867728950e")]
    public async Task APublicTopic_IsAnyonesToWatch(string topic)
        => Assert.That(await Access().MayWatch(topic, NoRoles), Is.True);

    [TestCase("staff:users")]
    [TestCase("staff:reschedules")]
    public async Task AStaffTopic_IsStaffsAlone(string topic)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Access().MayWatch(topic, NoRoles), Is.False);
            Assert.That(await Access().MayWatch(topic, PlayerRole), Is.False);
            Assert.That(await Access().MayWatch(topic, Commissioner), Is.True);
        }
    }

    [Test]
    public async Task ASeasonNotAnnouncedYet_ItsTopicsAndMatches_AreStaffsAlone()
    {
        var hidden = await AddSeason(1, announced: false);
        var shown = await AddSeason(2, announced: true);
        var match = SeasonMatch.Seed(hidden.SeasonID, null, null);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var access = Access();

        using (Assert.EnterMultipleScope())
        {
            foreach (var topic in new[] { LiveTopics.Season(1), LiveTopics.Draft(1), LiveTopics.Signups(1), LiveTopics.SeasonMatch(match.SeasonMatchID) })
            {
                Assert.That(await access.MayWatch(topic, NoRoles), Is.False, topic);
                Assert.That(await access.MayWatch(topic, Commissioner), Is.True, topic);
            }
            Assert.That(await access.MayWatch(LiveTopics.Season(shown.SeasonID), NoRoles), Is.True);
            Assert.That(await access.MayWatch(LiveTopics.Season(99), Commissioner), Is.False, "no such season");
            Assert.That(await access.MayWatch(LiveTopics.SeasonMatch(999), Commissioner), Is.False, "no such match");
        }
    }

    [TestCase("")]
    [TestCase("season:02")]
    [TestCase("season:-2")]
    [TestCase("season")]
    [TestCase("player:x")]
    [TestCase("queue:me")]
    [TestCase("match:0F8FAD5B-D9CB-469F-A165-70867728950E")]
    [TestCase("whatever:1")]
    public async Task ATopicNotSpeltAsTheAppSpellsIt_IsNoTopic(string topic)
    {
        await AddSeason(2, announced: true);

        Assert.That(await Access().MayWatch(topic, Commissioner), Is.False);
    }

    [Test]
    public async Task ATopicPastItsLength_IsNoTopic()
        => Assert.That(await Access().MayWatch("player:" + new string('1', LiveTopics.MaxLength), NoRoles), Is.False);

    private sealed record Happened(int Value) : IDomainEvent;

    private sealed class Recorder<T>(List<string> log, string name, bool fail = false) : IDomainEventHandler<T> where T : IDomainEvent
    {
        public Task Handle(T domainEvent, CancellationToken cancellationToken)
        {
            log.Add(name);
            if (fail)
                throw new InvalidOperationException(name);
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task TheDispatcher_RunsTheEventsHandlers_ThenEveryEventsOnes_AFailureLoggedAndTheRestRun()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<Happened>>(_ => new Recorder<Happened>(log, "failing", fail: true));
        services.AddScoped<IDomainEventHandler<Happened>>(_ => new Recorder<Happened>(log, "own"));
        services.AddScoped<IDomainEventHandler<IDomainEvent>>(_ => new Recorder<IDomainEvent>(log, "every"));
        await using var provider = services.BuildServiceProvider();
        var logger = new FakeLogger<DomainEventDispatcher>();

        await new DomainEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), logger).Dispatch([new Happened(1), new Happened(2)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(log, Is.EqualTo(HandlersInOrder));
            Assert.That(logger.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Error), Is.EqualTo(2));
        }
    }

    [Test]
    public void Presence_CountsAMemberOnce_AGuestATab_ThePagesBusiestFirst_TheirMembersByName()
    {
        var snapshot = PresenceSnapshot.Of(
        [
            new PresenceTab("a", 1, "Zed", "/seasons"),
            new PresenceTab("b", 1, "Zed", "/seasons"),
            new PresenceTab("c", 2, "amy", "/seasons"),
            new PresenceTab("d", 1, "Zed", "/leaderboards"),
            new PresenceTab("e", null, null, "/leaderboards"),
            new PresenceTab("f", null, null, "/"),
            new PresenceTab("g", null, null, "/"),
            new PresenceTab("h", null, null, "/"),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((snapshot.Online, snapshot.Members, snapshot.Guests), Is.EqualTo((6, 2, 4)));
            Assert.That(snapshot.Pages.Select(p => (p.Path, p.Guests, p.Members.Count)), Is.EqualTo(new[] { ("/", 3, 0), ("/leaderboards", 1, 1), ("/seasons", 0, 2) }));
            Assert.That(snapshot.Pages[2].Members.Select(m => m.Name), Is.EqualTo(MembersByName));
            Assert.That((PresenceSnapshot.Of([]).Online, PresenceSnapshot.Of([]).Pages.Count), Is.EqualTo((0, 0)));
        }
    }

    [TestCase("draft:5", ExpectedResult = true)]
    [TestCase("season:5", ExpectedResult = true)]
    [TestCase("seasons", ExpectedResult = true)]
    [TestCase("queue", ExpectedResult = true)]
    [TestCase("queue:me", ExpectedResult = false)]
    [TestCase("player:5", ExpectedResult = false)]
    [TestCase("seasonmatch:5", ExpectedResult = false)]
    [TestCase("staff:users", ExpectedResult = false)]
    public bool TheTopicsManyWatch_ComeWithTheirData(string topic) => LiveSnapshots.Pushes(topic);

    [Test]
    public void OnlyTheHomePagesData_DependsOnTheViewer()
        => Assert.That(PushedTopics.Where(LiveSnapshots.DependsOnViewer), Is.EqualTo(OnlySeasons));

    [Test]
    public async Task TheQueuesData_IsItsSize_ANoticeTopicHasNone()
    {
        var queue = Substitute.For<GrifballWebApp.Application.Matchmaking.IQueueRepository>();
        queue.Count(Arg.Any<CancellationToken>()).Returns(4);
        var snapshots = new LiveSnapshots(null!, null!, null!, null!, null!, queue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await snapshots.For(LiveTopics.Queue, SeasonViewer.Public), Is.EqualTo(new QueueLiveDto(4)));
            Assert.That(await snapshots.For(LiveTopics.Player(1), SeasonViewer.Public), Is.Null);
            Assert.That(await snapshots.For("draft:x", SeasonViewer.Public), Is.Null);
        }
    }

    [Test]
    public async Task TheHandler_PublishesTheEventsTopics()
    {
        var live = Substitute.For<ILiveUpdates>();
        var team = Team.Seed("Reds", seasonID: 5, teamID: 3);

        await new LiveUpdatesHandler(live, Context).Handle(new TeamRenamed(team), CancellationToken.None);

        live.Received(1).Publish(Arg.Is<IEnumerable<LiveChange>>(c => c.Select(x => x.Topic).SequenceEqual(ATeam)));
    }
}
