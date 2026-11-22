using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Live;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Microsoft.Extensions.Time.Testing;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

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
    private static readonly string[] ADraftPick = ["season:5", "teams:5", "team:3", "bracket:5", "draft:5", "player:7"];
    private static readonly string[] AResult = ["seasons", "season:5", "bracket:5", "teams:5", "seasonmatch:11", "team:1", "team:2"];
    private static readonly string[] ASpot = ["queue", "queue:me@7"];
    private static readonly string[] HandlersInOrder = ["failing", "own", "every", "failing", "own", "every"];
    private static readonly string[] MembersByName = ["amy", "Zed"];
    private static readonly int[] UnnamedFirst = [3, 4];
    private static readonly string[] ATeam = ["season:5", "teams:5", "team:3", "bracket:5", "draft:5"];
    private static readonly string[] OnlySeasons = ["seasons"];
    private static readonly string[] PushedTopics = ["seasons", "season:1", "bracket:1", "teams:1", "team:1", "draft:1", "queue"];
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
    public void ADraftPick_ChangesTheSeason_ItsTeamsPages_Bracket_AndDraft_AndThePlayersProfile()
    {
        var team = Team.Seed("Reds", seasonID: 5, teamID: 3);

        Assert.That(Topics(new TeamRosterChanged(team, [7])), Is.EquivalentTo(ADraftPick));
    }

    [Test]
    public void AResult_ChangesTheSeasons_TheSeason_ItsBracketAndTeamsPages_TheMatchsPage_AndItsTeams()
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
        var team = Team.Seed("Hidden team", seasonID: hidden.SeasonID);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        var access = Access();

        using (Assert.EnterMultipleScope())
        {
            foreach (var topic in new[] { LiveTopics.Season(1), LiveTopics.Bracket(1), LiveTopics.Teams(1), LiveTopics.Draft(1), LiveTopics.Signups(1),
                LiveTopics.SeasonMatch(match.SeasonMatchID), LiveTopics.Team(team.TeamID) })
            {
                Assert.That(await access.MayWatch(topic, NoRoles), Is.False, topic);
                Assert.That(await access.MayWatch(topic, Commissioner), Is.True, topic);
            }
            Assert.That(await access.MayWatch(LiveTopics.Season(shown.SeasonID), NoRoles), Is.True);
            Assert.That(await access.MayWatch(LiveTopics.Season(99), Commissioner), Is.False, "no such season");
            Assert.That(await access.MayWatch(LiveTopics.SeasonMatch(999), Commissioner), Is.False, "no such match");
            Assert.That(await access.MayWatch(LiveTopics.Team(999), Commissioner), Is.False, "no such team");
        }
    }

    [TestCase("")]
    [TestCase("season:02")]
    [TestCase("season:-2")]
    [TestCase("season")]
    [TestCase("player:x")]
    [TestCase("queue:me")]
    [TestCase("match:0F8FAD5B-D9CB-469F-A165-70867728950E")]
    [TestCase("seasonmatch:x")]
    [TestCase("seasonmatch:07")]
    [TestCase("team:x")]
    [TestCase("team:07")]
    [TestCase("bracket:07")]
    [TestCase("xbox:x")]
    [TestCase("xbox:07")]
    [TestCase("match:x")]
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
    public void NoLiveUpdates_TakesChanges_AndTellsNobody()
        => Assert.That(() => new NoLiveUpdates().Publish([new LiveChange(LiveTopics.Player(1))]), Throws.Nothing);

    /// <summary>Scopes that can't be made: a dispatch that makes one fails.</summary>
    private sealed class NoScopes : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("no scope expected");
    }

    [Test]
    public void TheDispatcher_GivenNoEvents_MakesNoScope()
        => Assert.That(async () => await new DomainEventDispatcher(new NoScopes(), new FakeLogger<DomainEventDispatcher>()).Dispatch([]), Throws.Nothing);

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
            Assert.That(PresenceSnapshot.Of([new PresenceTab("a", 4, "Bo", "/"), new PresenceTab("b", 3, null, "/")]).Pages[0].Members.Select(m => m.UserID),
                Is.EqualTo(UnnamedFirst), "a member with no name shown first");
        }
    }

    [TestCase("draft:5", ExpectedResult = true)]
    [TestCase("season:5", ExpectedResult = true)]
    [TestCase("bracket:5", ExpectedResult = true)]
    [TestCase("teams:5", ExpectedResult = true)]
    [TestCase("team:5", ExpectedResult = true)]
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
        var snapshots = new LiveSnapshots(null!, null!, null!, null!, null!, queue, null!, null!);

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

        await new LiveUpdatesHandler(live).Handle(new TeamRenamed(team), CancellationToken.None);

        live.Received(1).Publish(Arg.Is<IEnumerable<LiveChange>>(c => c.Select(x => x.Topic).SequenceEqual(ATeam)));
    }

    private static readonly Guid HaloMatchID = new("0f8fad5b-d9cb-469f-a165-70867728950e");

    /// <summary>A Halo game of xbox:100 against xbox:200 and xbox:300.</summary>
    private static HaloMatch HaloGame()
    {
        var match = HaloMatch.Seed(HaloMatchID);
        match.AddTeam(0, 5, Outcomes.Won);
        match.AddTeam(1, 3, Outcomes.Lost);
        match.AddParticipant(0, 100, new MatchParticipantStats());
        match.AddParticipant(1, 200, new MatchParticipantStats());
        match.AddParticipant(1, 300, new MatchParticipantStats());
        return match;
    }

    /// <summary>A recorded game of season 5: team 3 on one side, no team on the other.</summary>
    private static RecordedGame TeamsGame()
    {
        var game = RecordedGame.Seed("game-3", seasonID: 5);
        game.SeedSide(0, teamID: 3);
        game.SeedSide(1);
        return game;
    }

    /// <summary>
    /// Every domain event the Domain raises, with the topics it changes (space-separated; "topic@user" for a user's own).
    /// The season is 5, its match 11, the player 7.
    /// </summary>
    private static IEnumerable<TestCaseData> EveryEvent()
    {
        var season = Season.Seed(seasonName: "S5", seasonID: 5);
        var team = Team.Seed("Reds", seasonID: 5, teamID: 3);
        var other = Team.Seed("Blues", seasonID: 5, teamID: 4);
        var match = SeasonMatch.Seed(5, 3, 4);
        match.SeasonMatchID = 11;
        var signup = SeasonSignup.Seed(5, 7);
        var reschedule = MatchReschedule.Request(11, null, Now, "Busy", 7, Now);
        var player = User.Seed(userName: "player7", id: 7);
        var spot = QueuedPlayer.Seed(7, Now);
        var lateLeague = MatchedMatch.Start([7], [8], Now);
        var totals = new StatSheetTotals { Gamertag = "Seven" };

        TestCaseData Case(IDomainEvent domainEvent, string topics, string? name = null)
            => new TestCaseData(domainEvent, topics).SetArgDisplayNames(name ?? domainEvent.GetType().Name);

        // League: the season
        yield return Case(new SeasonCreated(season), "seasons season:5");
        yield return Case(new SeasonEdited(season), "seasons season:5 teams:5 draft:5");
        yield return Case(new SeasonScheduled(season), "seasons season:5 teams:5 draft:5 signups:5");
        yield return Case(new SeasonAnnounced(season), "seasons season:5 teams:5");
        yield return Case(new SeasonTimeslotsChanged(season), "season:5 signups:5");
        yield return Case(new CaptainsLockChanged(season), "draft:5");
        yield return Case(new SeasonCopiedFrom(season), "season:5 teams:5 draft:5 signups:5");
        yield return Case(new SeasonChampionsChanged(season), "seasons season:5 teams:5");
        yield return Case(new SeasonAwardsChanged(season), "season:5 teams:5");

        // Its teams
        yield return Case(new TeamFormed(team), "season:5 teams:5 team:3 bracket:5 draft:5");
        yield return Case(new TeamDisbanded(team, [7, 8]), "season:5 teams:5 team:3 bracket:5 draft:5 player:7 player:8");
        yield return Case(new TeamRenamed(team), "season:5 teams:5 team:3 bracket:5 draft:5");
        yield return Case(new TeamCaptainChanged(team), "season:5 teams:5 team:3 bracket:5 draft:5");
        yield return Case(new TeamRosterChanged(team, [7, 7]), "season:5 teams:5 team:3 bracket:5 draft:5 player:7");
        yield return Case(new PlayerDrafted(team, 7, 1, 2), "season:5 teams:5 team:3 bracket:5 draft:5 player:7");
        yield return Case(new PlayerUndrafted(team, 7, 2), "season:5 teams:5 team:3 bracket:5 draft:5 player:7");
        yield return Case(new PickMoved(team, other, 7, 1, 2, 3), "season:5 teams:5 team:3 bracket:5 draft:5 player:7 team:4");
        yield return Case(new TeamTimeslotsChanged(team), "season:5 team:3");
        yield return Case(new TeamArchiveRecorded(team), "season:5 teams:5 team:3");

        // Its matches
        yield return Case(new SeasonMatchCreated(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeasonMatchCanceled(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeasonMatchScheduled(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeasonMatchTeamsChanged(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeasonMatchTeamsChanged(match, ReplacedTeamID: 9), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4 team:9", "SeasonMatchTeamsChanged replacing a team");
        yield return Case(new SeasonMatchResultChanged(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeasonMatchDecided(match, SeasonMatchDecision.Games), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeasonMatchReopened(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");
        yield return Case(new SeriesGamesChanged(match), "seasons season:5 bracket:5 teams:5 seasonmatch:11 team:3 team:4");

        // Its signups, reschedules, VODs, stats and schedules
        yield return Case(new PlayerSignedUp(signup), "signups:5 draft:5 season:5 player:7");
        yield return Case(new SignupChanged(signup), "signups:5 draft:5 season:5 player:7");
        yield return Case(new SignupWithdrawn(signup), "signups:5 draft:5 season:5 player:7");
        yield return Case(new RescheduleRequested(reschedule), "seasonmatch:11 staff:reschedules");
        yield return Case(new RescheduleChanged(reschedule), "seasonmatch:11 staff:reschedules");
        yield return Case(new VodChanged(Vod.Seed("v1", "Reds v Blues", Now, VodMatchStatus.Confirmed, seasonID: 5, seasonMatchID: 11)), "staff:vods season:5 seasonmatch:11", "VodChanged of a match");
        yield return Case(new VodChanged(Vod.Seed("v2", "Something else", Now, VodMatchStatus.NeedsReview)), "staff:vods", "VodChanged of no match");
        yield return Case(new PlayerSeasonStatsChanged(PlayerSeasonStat.Seed(totals, seasonID: 5, userID: 7)), "season:5 player:7");
        yield return Case(new PlayerSeasonStatsChanged(PlayerSeasonStat.Seed(totals, seasonID: 5, userID: 7, teamID: 3)), "season:5 player:7 team:3", "PlayerSeasonStatsChanged of a team");
        yield return Case(new SchedulingRulesChanged(SeasonSchedulingRules.Defaults(5)), "season:5");
        yield return Case(new ScheduleRecommendationChanged(ScheduleRecommendation.Propose(5, null, 0.5, 1, [])), "season:5");

        // Late League
        yield return Case(new PlayerQueued(spot), "queue queue:me@7");
        yield return Case(new PlayerLeftQueue(spot), "queue queue:me@7");
        yield return Case(new LateLeagueMatchStarted(lateLeague, [7, 8]), "queue lateleague queue:me@7 queue:me@8");
        yield return Case(new LateLeagueMatchDecided(lateLeague, [7, 8]), "queue lateleague queue:me@7 queue:me@8");
        yield return Case(new LateLeagueMatchCanceled(lateLeague, [7], Unannounced: true), "queue lateleague queue:me@7");
        yield return Case(new LateLeagueGameRecorded(player, Won: true, MmrBefore: 1000), "player:7");
        yield return Case(new LateLeaguePlayerKicked(lateLeague, 7), "lateleague queue:me@7");
        yield return Case(new LateLeagueMatchChanged(lateLeague), "lateleague");
        yield return Case(new MatchmakingSeasonChanged(MatchmakingSeason.Seed("Late League 1")), "lateleague");
        yield return Case(new MatchmakingPlayerStatsChanged(MatchmakingPlayerStat.Seed(totals, userID: 7)), "lateleague player:7");
        yield return Case(new LadderRankChanged(MatchmakingLadderRank.Seed(new LadderStanding { Gamertag = "Seven" }, userID: 7)), "lateleague player:7");

        // Halo
        yield return Case(new HaloMatchChanged(HaloGame()), $"match:{HaloMatchID:D} xbox:100 xbox:200 xbox:300");

        // Players
        yield return Case(new UserChanged(player), "player:7 staff:users");
        yield return Case(new XboxUserChanged(XboxUser.FromXbox(100, "Hundred")), "xbox:100 staff:users");
        yield return Case(new DiscordUserChanged(DiscordUser.FromDiscord(1, "disc")), "staff:users");
        // Its topics name nothing of the merge: none is needed
        yield return Case(new UserMergeChanged(null!), "staff:merges staff:users");
        yield return Case(new DuplicateDismissed(DuplicateDismissal.Dismiss(1, 2, Now, null)), "staff:merges");

        // Archive
        yield return Case(new GamertagClaimChanged(GamertagClaim.ClaimedByUser(7, "Seven", "seven")), "staff:claims player:7", "GamertagClaimChanged of a claimant");
        yield return Case(new GamertagClaimChanged(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 100, "Seven", "seven", 9)), "staff:claims", "GamertagClaimChanged suggested");
        yield return Case(new HistoricalImportJobChanged(HistoricalImportJob.Refresh()), "staff:imports");
        // Its topics name nothing of the source: none is needed
        yield return Case(new HistoricalSourceChanged(null!), "staff:imports");
        yield return Case(new RecordedGameChanged(RecordedGame.Seed("game-1", seasonID: 5)), "season:5", "RecordedGameChanged of a season");
        yield return Case(new RecordedGameChanged(RecordedGame.Seed("game-2")), "lateleague", "RecordedGameChanged of Late League");
        yield return Case(new RecordedGameChanged(TeamsGame()), "season:5 team:3", "RecordedGameChanged of a season's team");
        yield return Case(new StatFlagFound(StatFlag.Found(StatFlagRule.NegativeStat, null, 41, null, "reason", Now)), "staff:statflags");
        yield return Case(new StatFlagDismissed(StatFlag.Found(StatFlagRule.NegativeStat, null, 41, null, "reason", Now)), "staff:statflags");
        yield return Case(new StatFlagReopened(StatFlag.Found(StatFlagRule.NegativeStat, null, 41, null, "reason", Now)), "staff:statflags");
        yield return Case(new StatFlagCleared(StatFlag.Found(StatFlagRule.NegativeStat, null, 41, null, "reason", Now)), "staff:statflags");
    }

    [TestCaseSource(nameof(EveryEvent))]
    public void EveryDomainEvent_ChangesTheTopicsOfThePagesThatShowIt(IDomainEvent domainEvent, string topics)
        => Assert.That(Topics(domainEvent), Is.EquivalentTo(topics.Split(' ')));

    [Test]
    public void EveryDomainEvent_HasItsTopics_InTheseTests()
    {
        var raised = typeof(Season).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(t))
            .Select(t => t.Name);
        var tested = EveryEvent().Select(c => c.Arguments[0]!.GetType().Name).Distinct();

        Assert.That(tested, Is.EquivalentTo(raised), "A new event needs its arm in LiveChanges, and its case here");
    }

    [Test]
    public async Task TheHandler_AHaloGame_PublishesItsAccounts_LookingNobodyUp_OnTheSavesPath()
    {
        var published = new List<LiveChange>();
        var live = Substitute.For<ILiveUpdates>();
        live.When(l => l.Publish(Arg.Any<IEnumerable<LiveChange>>())).Do(c => published.AddRange(c.Arg<IEnumerable<LiveChange>>()));
        var game = new HaloMatchChanged(HaloGame());

        await new LiveUpdatesHandler(live).Handle(game, CancellationToken.None);

        // The profiles watching those accounts' users are told where they are watched (LiveDelivery), once a batch
        Assert.That(published, Is.EqualTo(LiveChanges.Of(game)));
    }

    private LiveSnapshots Snapshots() => new(new GrifballWebApp.Application.Teams.TeamService(Context, Substitute.For<GrifballWebApp.Application.Database.IDatabaseLocks>()),
        new SeasonService(Context, new GrifballWebApp.Application.Stats.SeasonStatsService(Context, _time), new SeasonVisibility(Context, _time), _time),
        new GrifballWebApp.Application.TeamStandings.TeamStandingsService(Context), new GrifballWebApp.Application.MatchPlanner.MatchPlannerService(Context),
        new GrifballWebApp.Application.Home.HomeEventsService(new SeasonVisibility(Context, _time), _time), Substitute.For<GrifballWebApp.Application.Matchmaking.IQueueRepository>(),
        new GrifballWebApp.Application.Brackets.BracketService(Context, new GrifballWebApp.Application.TeamStandings.TeamStandingsService(Context),
            new GrifballWebApp.Application.Championships.ChampionshipService(Context)),
        new GrifballWebApp.Application.TeamPage.TeamPageService(Context, _time));

    [Test]
    public async Task TheDraftsData_IsItsTeams_ItsPool_AndItsLock()
    {
        await AddSeason(5, announced: true);
        Context.Users.Add(User.Seed(userName: "seven", displayName: "Seven", id: 7));
        await Context.SaveChangesAsync();
        Context.SeasonSignups.Add(SeasonSignup.Seed(5, 7));
        await Context.SaveChangesAsync();

        var draft = (DraftLiveDto?)await Snapshots().For(LiveTopics.Draft(5), SeasonViewer.Public);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft!.SeasonID, Is.EqualTo(5));
            Assert.That(draft.Teams, Is.Empty);
            Assert.That(draft.Pool, Has.Count.EqualTo(1));
            Assert.That(draft.CaptainsLocked, Is.False);
        }
    }

    [Test]
    public async Task ASeasonsData_IsWhatItsPageShows()
    {
        await AddSeason(5, announced: true);

        var season = (SeasonLiveDto?)await Snapshots().For(LiveTopics.Season(5), SeasonViewer.Public);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season!.SeasonID, Is.EqualTo(5));
            Assert.That(season.Summary, Is.Not.Null);
            Assert.That(season.Standings, Is.Empty);
            Assert.That(season.Scheduled, Is.Empty);
            Assert.That(season.Unscheduled, Is.Empty);
            Assert.That(season.PlayerStats, Is.Empty);
        }
    }

    [Test]
    public async Task ABracketsData_IsWhatBracketsViewerDraws_AndNoneForASeasonThatIsGone()
    {
        await AddSeason(5, announced: true);
        var snapshots = Snapshots();

        var bracket = (BracketLiveDto?)await snapshots.For(LiveTopics.Bracket(5), SeasonViewer.Public);
        var gone = (BracketLiveDto?)await snapshots.For(LiveTopics.Bracket(99), SeasonViewer.Public);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracket!.SeasonID, Is.EqualTo(5));
            Assert.That(bracket.ViewerData, Is.Not.Null);
            Assert.That(gone!.ViewerData, Is.Null);
        }
    }

    [Test]
    public async Task ATopicWithNoNumber_HasNoData()
        => Assert.That(await Snapshots().For("team:x", SeasonViewer.Public), Is.Null);

    [Test]
    public async Task TheHomePagesData_IsTheEventsItsViewerMaySee()
    {
        await AddSeason(1, announced: false);
        var snapshots = Snapshots();

        var forEveryone = (SeasonsLiveDto?)await snapshots.For(LiveTopics.Seasons, SeasonViewer.Public);
        var forStaff = (SeasonsLiveDto?)await snapshots.For(LiveTopics.Seasons, SeasonViewer.Staff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forEveryone!.Events, Is.Empty, "a season not announced yet");
            Assert.That(forStaff!.Events, Is.Not.Empty);
        }
    }
}
