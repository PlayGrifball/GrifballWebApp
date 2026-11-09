using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Models.HaloInfinite.Medals;
using GruntMatchType = Surprenant.Grunt.Models.HaloInfinite.MatchType;
using Match = GrifballWebApp.Domain.Models.Match;
using Medal = GrifballWebApp.Domain.Models.Medal;
using Team = Surprenant.Grunt.Models.HaloInfinite.Team;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DataPullServiceBranchTests
{
    private GrifballContext _context;
    private RecordingLogger<DataPullService> _logger;
    private IHaloInfiniteClientFactory _factory;
    private IGetsertXboxUserService _getsert;
    private DataPullService _service;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _logger = new RecordingLogger<DataPullService>();
        _factory = Substitute.For<IHaloInfiniteClientFactory>();
        _getsert = Substitute.For<IGetsertXboxUserService>();
        _getsert.GetsertXboxUserByXuid(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => XboxUser.FromXbox(ci.Arg<long>(), $"gt{ci.Arg<long>()}"));
        _service = new DataPullService(_logger, _factory, _context, _getsert);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static CoreStats Core(int score = 1) => new()
    {
        Score = score,
        AverageLifeDuration = TimeSpan.FromSeconds(10),
        Medals = [],
    };

    private static Player Player(string playerId, int lastTeam, params (int Team, int Score)[] teamStats) => new()
    {
        PlayerId = playerId,
        LastTeamId = lastTeam,
        PlayerTeamStats = teamStats.Select(t => new PlayerTeamStat { TeamId = t.Team, Stats = new Stats { CoreStats = Core(t.Score) } }).ToArray(),
        ParticipationInfo = new ParticipationInfo
        {
            FirstJoinedTime = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
            LastLeaveTime = new DateTimeOffset(2024, 1, 1, 10, 30, 0, TimeSpan.FromHours(-5)),
            LeftInProgress = true,
            TimePlayed = TimeSpan.FromMinutes(30),
        },
    };

    private static MatchStats Stats(Guid id, IEnumerable<(int TeamId, int Outcome)> teams, params Player[] players) => new()
    {
        MatchId = id,
        MatchInfo = new MatchInfo
        {
            StartTime = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
            EndTime = new DateTimeOffset(2024, 1, 1, 10, 30, 0, TimeSpan.FromHours(-5)),
            Duration = TimeSpan.FromMinutes(30),
        },
        Teams = teams.Select(t => new Team { TeamId = t.TeamId, Outcome = t.Outcome, Stats = new Stats { CoreStats = Core(t.TeamId * 10) } }).ToArray(),
        Players = players,
    };

    private static HaloApiResultContainer<T, HaloApiErrorContainer> Ok<T>(T value) => new(value, null);

    [Test]
    public async Task SaveMatchStats_MapsOutcomesAndConvertsTimesToUtc()
    {
        var id = Guid.NewGuid();

        await _service.SaveMatchStats(Stats(id, [(0, 1), (1, 2), (2, 3), (3, 4)]));

        var match = await _context.Matches.Include(x => x.MatchTeams).AsNoTracking().SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(match.MatchID, Is.EqualTo(id));
            Assert.That(match.StartTime, Is.EqualTo(new DateTime(2024, 1, 1, 15, 0, 0)));
            Assert.That(match.EndTime, Is.EqualTo(new DateTime(2024, 1, 1, 15, 30, 0)));
            Assert.That(match.MatchTeams.OrderBy(x => x.TeamID).Select(x => x.Outcome),
                Is.EqualTo(new[] { Outcomes.Tie, Outcomes.Won, Outcomes.Lost, Outcomes.DidNotFinish }));
            Assert.That(match.MatchTeams.OrderBy(x => x.TeamID).Select(x => x.Score), Is.EqualTo(new[] { 0, 10, 20, 30 }));
        });
    }

    [Test]
    public async Task SaveMatchStats_UnknownOutcome_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SaveMatchStats(Stats(Guid.NewGuid(), [(0, 7)])));
        Assert.That(ex!.ActualValue, Is.EqualTo(7));
    }

    [Test]
    public async Task SaveMatchStats_NoTeams_IsIgnored()
    {
        await _service.SaveMatchStats(Stats(Guid.NewGuid(), []));

        Assert.That(await _context.Matches.AnyAsync(), Is.False);
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Debug));
    }

    [Test]
    public async Task SaveMatchStats_NonNumericXuid_Throws()
    {
        var stats = Stats(Guid.NewGuid(), [(0, 2)], Player("xuid(abc)", 0, (0, 1)));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.SaveMatchStats(stats));

        Assert.That(ex!.Message, Is.EqualTo("XUID not long"));
        Assert.That(_logger.Entries.Single(x => x.Level == LogLevel.Error).Message, Is.EqualTo("Could not parse abc, not long"));
    }

    [Test]
    public async Task SaveMatchStats_PlayerWithoutLastTeamStats_Throws()
    {
        var stats = Stats(Guid.NewGuid(), [(0, 2), (1, 3)], Player("xuid(5)", 1, (0, 1)));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.SaveMatchStats(stats));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find last team stats"));
    }

    [Test]
    public async Task SaveMatchStats_AGamertagLookupThatFails_LeavesNothingTracked_SoTheGameCanBeSavedAgain()
    {
        // Issue #222: the lookup failed after the game's teams were added, so every retry on the context failed too
        var stats = Stats(Guid.NewGuid(), [(0, 2), (1, 3)], Player("xuid(5)", 0, (0, 1)), Player("xuid(6)", 1, (1, 1)));
        _getsert.GetsertXboxUserByXuid(6, Arg.Any<CancellationToken>())
            .Returns<XboxUser>(_ => throw new Exception("Failed to resolve gamertag: an HTML error page (Service unavailable)"));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.SaveMatchStats(stats));

        Assert.That(ex!.Message, Does.StartWith("Failed to resolve gamertag"));
        Assert.That(_context.ChangeTracker.Entries<MatchTeam>(), Is.Empty);
        Assert.That(_context.ChangeTracker.Entries<Match>(), Is.Empty);

        _getsert.GetsertXboxUserByXuid(6, Arg.Any<CancellationToken>()).Returns(XboxUser.FromXbox(6, "gt6"));
        await _service.SaveMatchStats(stats);

        Assert.That(await _context.MatchTeams.AsNoTracking().CountAsync(), Is.EqualTo(2));
        Assert.That(await _context.MatchParticipants.AsNoTracking().CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task SaveMatchStats_AGamertagLookupThatSaves_DoesNotStoreTheGameBeforeItsPlayers()
    {
        // A lookup saves the player it found; the game must not go with it while a later lookup can still fail
        var stats = Stats(Guid.NewGuid(), [(0, 2), (1, 3)], Player("xuid(5)", 0, (0, 1)), Player("xuid(6)", 1, (1, 1)));
        _getsert.GetsertXboxUserByXuid(5, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            var user = XboxUser.FromXbox(5, "gt5");
            _context.XboxUsers.Add(user);
            await _context.SaveChangesAsync();
            return user;
        });
        _getsert.GetsertXboxUserByXuid(6, Arg.Any<CancellationToken>())
            .Returns<XboxUser>(_ => throw new Exception("Failed to resolve gamertag: down"));

        await Assert.ThrowsAsync<Exception>(() => _service.SaveMatchStats(stats));

        Assert.That(await _context.Matches.AsNoTracking().AnyAsync(), Is.False);
        Assert.That(await _context.XboxUsers.AsNoTracking().Select(x => x.XboxUserID).ToListAsync(), Is.EqualTo(new long[] { 5 }));
    }

    [Test]
    public async Task SaveMatchStats_ASaveThatFails_LetsGoOfOnlyTheGame_SoTheCallersChangesStayAndTheGameCanBeSavedAgain()
    {
        // The caller's own pending change: a failed save must not take it out of the change tracker
        var pending = XboxUser.FromXbox(99, "pending");
        _context.XboxUsers.Add(pending);
        var player = Player("xuid(5)", 0, (0, 1));
        player.PlayerTeamStats.Single().Stats.CoreStats.Medals = [new() { NameId = 404, Count = 1 }];
        var stats = Stats(Guid.NewGuid(), [(0, 2), (1, 3)], player);

        // No medal 404: the save fails on its foreign key
        await Assert.ThrowsAsync<DbUpdateException>(() => _service.SaveMatchStats(stats));

        Assert.Multiple(() =>
        {
            Assert.That(_context.Entry(pending).State, Is.EqualTo(EntityState.Added));
            Assert.That(_context.ChangeTracker.Entries<Match>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<MatchTeam>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<MatchParticipant>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<MedalEarned>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<XboxUser>().Select(e => e.Entity.XboxUserID), Is.EqualTo(new long[] { 99 }));
        });

        _context.MedalTypes.Add(MedalType.Imported(1, "t"));
        _context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "d"));
        _context.Medals.Add(Medal.Imported(404, "Found", "x", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        await _service.SaveMatchStats(stats);

        Assert.That(await _context.MatchParticipants.AsNoTracking().CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.XboxUsers.AsNoTracking().OrderBy(x => x.XboxUserID).Select(x => x.XboxUserID).ToListAsync(), Is.EqualTo(new long[] { 5, 99 }));
    }

    [Test]
    public async Task SaveMatchStats_TeamSwitcherBotsAndMedals()
    {
        _context.MedalTypes.Add(MedalType.Imported(1, "t"));
        _context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "d"));
        _context.Medals.Add(Medal.Imported(77, "Grifball", "x", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        await _context.SaveChangesAsync();

        var id = Guid.NewGuid();
        var switcher = Player("xuid(11)", 1, (0, 3), (1, 7));
        switcher.PlayerTeamStats.Single(x => x.TeamId == 1).Stats.CoreStats.Medals = [new() { NameId = 77, Count = 2, TotalPersonalScoreAwarded = 50 }];
        var bot = Player("bid(1.0)", 0, (0, 1));
        bot.BotAttributes = new();

        await _service.SaveMatchStats(Stats(id, [(0, 2), (1, 3)], switcher, bot));

        var participants = await _context.MatchParticipants.Include(x => x.MatchTeam).Include(x => x.MedalEarned).AsNoTracking().ToListAsync();
        var p = participants.Single();
        var warning = _logger.Entries.Single(x => x.Level == LogLevel.Warning);
        Assert.Multiple(() =>
        {
            Assert.That(p.XboxUserID, Is.EqualTo(11), "Bots are skipped");
            Assert.That(p.MatchTeam.TeamID, Is.EqualTo(1), "Participant is placed on their last team");
            Assert.That(p.Score, Is.EqualTo(7), "Stats are taken from the last team");
            Assert.That(p.LastLeaveTime, Is.EqualTo(new DateTime(2024, 1, 1, 15, 30, 0)));
            Assert.That(p.LeftInProgress, Is.True);
            Assert.That(p.MedalEarned.Single().MedalID, Is.EqualTo(77));
            Assert.That(p.MedalEarned.Single().Count, Is.EqualTo(2));
            Assert.That(p.MedalEarned.Single().TotalPersonalScoreAwarded, Is.EqualTo(50));
            Assert.That(warning.Message, Is.EqualTo($"Player xuid(11) played on multiple teams in match {id}"));
        });
        await _getsert.DidNotReceive().GetsertXboxUserByXuid(Arg.Is<long>(x => x != 11), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadMedals_NullResult_Throws()
    {
        _factory.Medals().Returns(new HaloApiResultContainer<MedalMetadataResponse, HaloApiErrorContainer>(null, null));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.DownloadMedals());

        Assert.That(ex!.Message, Is.EqualTo("Result null. Failed to get medals"));
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_EmptyList_DoesNothing()
    {
        await _service.DownloadRecentMatchesForPlayers([]);

        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.DownloadRecentMatchesForPlayers([1], ct: cts.Token));
        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_HandlesNullEmptyAndFilteredHistory()
    {
        var newMatch = Guid.NewGuid();
        var alreadyDownloaded = Guid.NewGuid();
        var missingStats = Guid.NewGuid();
        var otherMode = Guid.NewGuid();
        _context.Matches.Add(Match.Seed(alreadyDownloaded));
        await _context.SaveChangesAsync();

        PlayerMatchHistoryRecord Record(string id, int category) => new() { MatchId = id, MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)category } };

        // Player 1: API failure. Player 2: matches. Player 3: no matches at all.
        _factory.StatsGetMatchHistory("xuid(1)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(new HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>(null, null));
        // Both pages of player 2 return the same records.
        _factory.StatsGetMatchHistory("xuid(2)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Ok(new MatchHistoryResponse
            {
                Results =
                [
                    Record(newMatch.ToString(), 41),
                    Record(alreadyDownloaded.ToString(), 41),
                    Record(missingStats.ToString(), 41),
                    Record(otherMode.ToString(), 6),
                    Record("not-a-guid", 41),
                ],
            }));
        _factory.StatsGetMatchHistory("xuid(3)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Ok(new MatchHistoryResponse { Results = [] }));
        _factory.StatsGetMatchStats(newMatch).Returns(Ok(Stats(newMatch, [(0, 2), (1, 3)], Player("xuid(2)", 0, (0, 5)), Player("xuid(9)", 1, (1, 2)))));
        _factory.StatsGetMatchStats(missingStats).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null, null));
        _context.XboxUsers.Add(XboxUser.FromXbox(2, "existing"));
        await _context.SaveChangesAsync();
        _getsert.GetsertXboxUserByXuid(2, Arg.Any<CancellationToken>()).Returns(_ => _context.XboxUsers.Single(x => x.XboxUserID == 2));

        var result = await _service.DownloadRecentMatchesForPlayers([1, 2, 2, 3], startPage: 0, endPage: 1, perPage: 5);

        var saved = await _context.Matches.Select(x => x.MatchID).ToListAsync();
        var warnings = _logger.Entries.Where(x => x.Level == LogLevel.Warning).Select(x => x.Message).ToList();
        var information = _logger.Entries.Where(x => x.Level == LogLevel.Information).Select(x => x.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.EquivalentTo(new[] { alreadyDownloaded, newMatch }));
            Assert.That(warnings, Has.Some.EqualTo("Could not get page 0 of the match history of xuid(1): no error message"));
            Assert.That(information, Has.Some.EqualTo("No recent custom games for xuid(3)"), "Not an error: nothing to pull");
            Assert.That(warnings, Has.Some.EqualTo("Could not parse Guid not-a-guid from player history for player xuid(2)"));
            Assert.That(warnings, Has.Some.EqualTo($"Could not get match {missingStats}: no error message"));
            Assert.That((result.Players, result.MatchesFound, result.MatchesAlreadySaved, result.MatchesFetched), Is.EqualTo((3, 3, 1, 1)));
            Assert.That(result.FailedHistories, Is.EqualTo(new[] { "xuid(1)" }));
            Assert.That(result.FailedMatches, Is.EqualTo(new[] { missingStats }));
            Assert.That(result.Complete, Is.False);
        });
        await _factory.DidNotReceive().StatsGetMatchStats(alreadyDownloaded);
        await _factory.DidNotReceive().StatsGetMatchStats(otherMode);
        // Duplicated xbox ids are only queried once per page
        await _factory.Received(1).StatsGetMatchHistory("xuid(2)", 0, 5, Arg.Any<GruntMatchType>());
        // Page 1 starts at index page * perPage - 1
        await _factory.Received(1).StatsGetMatchHistory("xuid(2)", 4, 5, Arg.Any<GruntMatchType>());
        // Only users that are not in the database yet are bulk fetched
        await _getsert.Received(1).GetsertXboxUsersByXuid(Arg.Is<long[]>(x => x.SequenceEqual(new long[] { 9 })), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A player's pages are read two at a time. An empty page 1 answering before page 0 used to cancel page 0, its games
    /// thrown away; now it only stops the pages after it.
    /// </summary>
    [Test]
    public async Task DownloadRecentMatchesForPlayers_AnEmptyPage_KeepsTheEarlierPages_AndSkipsTheLaterOnes()
    {
        var onPage0 = Guid.NewGuid();
        PlayerMatchHistoryRecord Record(Guid id) => new() { MatchId = id.ToString(), MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41 } };
        var page1Answered = new TaskCompletionSource();
        _factory.StatsGetMatchHistory("xuid(5)", 0, 5, Arg.Any<GruntMatchType>()).Returns(async _ =>
        {
            await page1Answered.Task;
            await Task.Delay(50);
            return Ok(new MatchHistoryResponse { Results = [Record(onPage0)] });
        });
        _factory.StatsGetMatchHistory("xuid(5)", 4, 5, Arg.Any<GruntMatchType>()).Returns(_ =>
        {
            page1Answered.SetResult();
            return Ok(new MatchHistoryResponse { Results = [] });
        });
        _factory.StatsGetMatchStats(onPage0).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null, null));

        var result = await _service.DownloadRecentMatchesForPlayers([5], startPage: 0, endPage: 3, perPage: 5);

        Assert.That(result.MatchesFound, Is.EqualTo(1), "Page 0's game, answered after page 1 was found empty");
        await _factory.DidNotReceive().StatsGetMatchHistory("xuid(5)", 9, 5, Arg.Any<GruntMatchType>());
        await _factory.DidNotReceive().StatsGetMatchHistory("xuid(5)", 14, 5, Arg.Any<GruntMatchType>());
        Assert.That(_logger.Entries.Select(x => x.Message), Has.None.Contains("No recent custom games"), "It has some");
    }

    /// <summary>
    /// No answer from Halo (the gateway unreachable, a timeout) for a page or a game: that page or game is missing, the rest
    /// of the pull saved. A game fetched but not a team game: not saved, nor counted as saved. The caller's context keeps
    /// noticing its own changes after.
    /// </summary>
    [Test]
    public async Task DownloadRecentMatchesForPlayers_NoAnswer_LosesOnlyThatPageOrGame_AndCountsWhatItSaved()
    {
        var saved = Guid.NewGuid();
        var unanswered = Guid.NewGuid();
        var notTeams = Guid.NewGuid();
        PlayerMatchHistoryRecord Record(Guid id) => new() { MatchId = id.ToString(), MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41 } };
        _factory.StatsGetMatchHistory("xuid(6)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Task.FromException<HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>>(new HttpRequestException("gateway unreachable")));
        _factory.StatsGetMatchHistory("xuid(7)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Ok(new MatchHistoryResponse { Results = [Record(saved), Record(unanswered), Record(notTeams)] }));
        _factory.StatsGetMatchStats(saved).Returns(Ok(Stats(saved, [(0, 2), (1, 3)], Player("xuid(7)", 0, (0, 5)))));
        _factory.StatsGetMatchStats(unanswered).Returns(Task.FromException<HaloApiResultContainer<MatchStats, HaloApiErrorContainer>>(new TaskCanceledException("timed out")));
        _factory.StatsGetMatchStats(notTeams).Returns(Ok(Stats(notTeams, [], Player("xuid(7)", 0, (0, 5)))));
        // The caller's own entity, changed before the pull and saved after it
        var mine = XboxUser.FromXbox(70, "before");
        _context.XboxUsers.Add(mine);
        await _context.SaveChangesAsync();
        mine.Renamed("after", DateTime.UtcNow);

        var result = await _service.DownloadRecentMatchesForPlayers([6, 7], startPage: 0, endPage: 0, perPage: 5);
        await _context.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Matches.Select(x => x.MatchID).ToListAsync(), Is.EqualTo(new[] { saved }));
            Assert.That((result.MatchesFound, result.MatchesFetched, result.MatchesNotTeamGames), Is.EqualTo((3, 1, 1)));
            Assert.That(result.FailedHistories, Is.EqualTo(new[] { "xuid(6)" }));
            Assert.That(result.FailedMatches, Is.EqualTo(new[] { unanswered }));
            Assert.That(result.Summary, Does.EndWith("; 1 not team games, not saved"));
            Assert.That(_context.ChangeTracker.AutoDetectChangesEnabled, Is.True, "Back on for the caller");
            Assert.That(await _context.XboxUsers.AsNoTracking().Where(x => x.XboxUserID == 70).Select(x => x.Gamertag).SingleAsync(), Is.EqualTo("after"));
        });
    }

    [Test]
    public async Task GetMatch_HalosError_IsLoggedReadably()
    {
        var id = Guid.NewGuid();
        _factory.StatsGetMatchStats(id).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 503, Message = "<html><head><title>Service unavailable</title></head></html>" }));

        Assert.That(await _service.GetMatch(id), Is.Null);
        Assert.That(_logger.Entries.Select(x => x.Message), Has.Some.EqualTo($"Could not get match {id}: an HTML error page (Service unavailable)"));
    }

    /// <summary>
    /// The pull saves with change detection off (DownloadRecentMatchesForPlayers): its games, teams and players are added, and
    /// the audit interceptor still stamps who and when on each of them.
    /// </summary>
    [Test]
    public async Task DownloadRecentMatchesForPlayers_SavedGames_StillGetTheirAuditFields()
    {
        var currentUser = Substitute.For<GrifballWebApp.Domain.Services.ICurrentUserService>();
        currentUser.GetCurrentUserId().Returns(9001);
        var audited = await SetUpFixture.NewGrifballContext([new GrifballWebApp.Domain.Interceptors.AuditInterceptor(currentUser)]);
        try
        {
            var id = Guid.NewGuid();
            _factory.StatsGetMatchHistory("xuid(8)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
                .Returns(Ok(new MatchHistoryResponse { Results = [new() { MatchId = id.ToString(), MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41 } }] }));
            _factory.StatsGetMatchStats(id).Returns(Ok(Stats(id, [(0, 2), (1, 3)], Player("xuid(8)", 0, (0, 5)), Player("xuid(9)", 1, (1, 2)))));
            var started = DateTime.UtcNow.AddSeconds(-5);

            var result = await new DataPullService(_logger, _factory, audited, _getsert).DownloadRecentMatchesForPlayers([8], startPage: 0, endPage: 0, perPage: 5);

            Assert.That(result.MatchesFetched, Is.EqualTo(1));
            var match = await audited.Matches.AsNoTracking().SingleAsync(x => x.MatchID == id);
            var teams = await audited.MatchTeams.AsNoTracking().Where(x => x.MatchID == id).ToListAsync();
            var players = await audited.MatchParticipants.AsNoTracking().Where(x => x.MatchTeam.MatchID == id).ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That((match.CreatedByID, match.ModifiedByID), Is.EqualTo(((int?)9001, (int?)9001)));
                Assert.That(match.CreatedAt, Is.GreaterThan(started));
                Assert.That(teams, Has.Count.EqualTo(2).And.All.Matches<MatchTeam>(t => t.CreatedByID == 9001 && t.CreatedAt > started));
                Assert.That(players, Has.Count.EqualTo(2).And.All.Matches<MatchParticipant>(p => p.CreatedByID == 9001 && p.CreatedAt > started));
            });
        }
        finally
        {
            await audited.DropDatabaseAndDispose();
        }
    }

    /// <summary>
    /// Between two dates: a player's season is found under a long history without reading it from the newest page (pages 1,
    /// 2, 4, 8… then halving), and only the pages that reach into it are read for games.
    /// </summary>
    [Test]
    public async Task DownloadMatchesBetween_FindsTheSeasonsPagesInAFewCalls_AndReadsOnlyThem()
    {
        // 2,000 games, one an hour back from now: game i (0 the newest) at newest - i hours
        var newest = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var games = Enumerable.Range(0, 2000).Select(i => (ID: Guid.NewGuid(), Start: newest.AddHours(-i))).ToList();
        _factory.StatsGetMatchHistory("xuid(6)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>()).Returns(call => Ok(new MatchHistoryResponse
        {
            Results = games.Skip(call.ArgAt<int>(1)).Take(call.ArgAt<int>(2)).Select(g => new PlayerMatchHistoryRecord
            {
                MatchId = g.ID.ToString(),
                MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41, StartTime = new DateTimeOffset(g.Start) },
            }).ToArray(),
        }));
        _factory.StatsGetMatchHistory("xuid(7)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>()).Returns(Ok(new MatchHistoryResponse { Results = [] }));
        _factory.StatsGetMatchStats(Arg.Any<Guid>()).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, null!));
        // The season: games 1150 to 1210, on pages 46 (games 1150-1174) to 48 (1200-1224)
        var from = newest.AddHours(-1210);
        var to = newest.AddHours(-1150);

        var result = await _service.DownloadMatchesBetween([6, 7], from, to);
        var capped = await _service.DownloadMatchesBetween([6], from, to, maxCalls: 3);

        var calls = _factory.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "StatsGetMatchHistory" && (string)c.GetArguments()[0]! == "xuid(6)");
        Assert.Multiple(async () =>
        {
            Assert.That(result.MatchesFound, Is.EqualTo(75), "Pages 46 to 48: the season's games and the rest of its last page");
            Assert.That(calls - 3, Is.LessThan(20), "Found in a few calls, not 49 pages read (the 3 are the capped run's)");
            await _factory.DidNotReceive().StatsGetMatchHistory("xuid(6)", 10 * DataPullService.HistoryPageSize, Arg.Any<int>(), Arg.Any<GruntMatchType>());
            await _factory.Received(1).StatsGetMatchHistory("xuid(6)", 47 * DataPullService.HistoryPageSize, DataPullService.HistoryPageSize, Arg.Any<GruntMatchType>());
            Assert.That(_logger.Entries.Select(x => x.Message), Has.Some.EqualTo("No recent custom games for xuid(7)"));
            Assert.That(result.FailedHistories, Is.Empty);
            Assert.That(result.CappedHistories, Is.Empty);
            Assert.That(capped.CappedHistories, Is.EqualTo(new[] { "xuid(6)" }), "Ran out of calls before the season");
            Assert.That(capped.FailedHistories, Is.Empty, "Capped, not failed");
        });
    }

    /// <summary>
    /// Between two dates, when Halo doesn't give a page (the newest, one while searching, or one of the season's) that
    /// player's history is incomplete; a history ending before the season has no games in it, and nothing failed.
    /// </summary>
    [Test]
    public async Task DownloadMatchesBetween_APageHaloDoesntGive_LeavesThatHistoryIncomplete()
    {
        var newest = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var from = newest.AddHours(-1210);
        var to = newest.AddHours(-1150);
        // A player's history of `count` games one an hour back, Halo not giving the page starting at `fails`
        void History(string player, int count, int? fails = null) =>
            _factory.StatsGetMatchHistory(player, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>()).Returns(call =>
                call.ArgAt<int>(1) == fails
                    ? new HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 503, Message = "unavailable" })
                    : Ok(new MatchHistoryResponse
                    {
                        Results = Enumerable.Range(call.ArgAt<int>(1), Math.Max(0, Math.Min(call.ArgAt<int>(2), count - call.ArgAt<int>(1)))).Select(i => new PlayerMatchHistoryRecord
                        {
                            MatchId = Guid.NewGuid().ToString(),
                            MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41, StartTime = new DateTimeOffset(newest.AddHours(-i)) },
                        }).ToArray(),
                    }));
        const int page = DataPullService.HistoryPageSize;
        History("xuid(10)", 2000, fails: 0);           // the newest page
        History("xuid(11)", 2000, fails: 4 * page);    // a page while searching
        History("xuid(12)", 2000, fails: 47 * page);   // one of the season's pages (46 to 48)
        History("xuid(13)", 30);                       // ends long before the season
        _factory.StatsGetMatchStats(Arg.Any<Guid>()).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, null!));

        var result = await _service.DownloadMatchesBetween([10, 11, 12, 13], from, to);

        Assert.Multiple(() =>
        {
            Assert.That(result.FailedHistories, Is.EquivalentTo(new[] { "xuid(10)", "xuid(11)", "xuid(12)" }));
            Assert.That(result.CappedHistories, Is.Empty);
            Assert.That(result.MatchesFound, Is.EqualTo(page), "xuid(12)'s first season page, before the one Halo didn't give");
        });
    }
}
