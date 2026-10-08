using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Models.HaloInfinite.Medals;
using GruntMatchType = Surprenant.Grunt.Models.HaloInfinite.MatchType;
using Match = GrifballWebApp.Database.Models.Match;
using Medal = GrifballWebApp.Database.Models.Medal;
using Team = Surprenant.Grunt.Models.HaloInfinite.Team;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
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
            .Returns(ci => new XboxUser { XboxUserID = ci.Arg<long>(), Gamertag = $"gt{ci.Arg<long>()}" });
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
    public void SaveMatchStats_UnknownOutcome_Throws()
    {
        var ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SaveMatchStats(Stats(Guid.NewGuid(), [(0, 7)])));
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
    public void SaveMatchStats_NonNumericXuid_Throws()
    {
        var stats = Stats(Guid.NewGuid(), [(0, 2)], Player("xuid(abc)", 0, (0, 1)));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.SaveMatchStats(stats));

        Assert.That(ex!.Message, Is.EqualTo("XUID not long"));
        Assert.That(_logger.Entries.Single(x => x.Level == LogLevel.Error).Message, Is.EqualTo("Could not parse abc, not long"));
    }

    [Test]
    public void SaveMatchStats_PlayerWithoutLastTeamStats_Throws()
    {
        var stats = Stats(Guid.NewGuid(), [(0, 2), (1, 3)], Player("xuid(5)", 1, (0, 1)));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.SaveMatchStats(stats));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find last team stats"));
    }

    [Test]
    public async Task SaveMatchStats_TeamSwitcherBotsAndMedals()
    {
        _context.MedalTypes.Add(new MedalType { MedalTypeID = 1, MedalTypeName = "t" });
        _context.MedalDifficulties.Add(new MedalDifficulty { MedalDifficultyID = 1, MedalDifficultyName = "d" });
        _context.Medals.Add(new Medal { MedalID = 77, MedalName = "Grifball", Description = "x", MedalTypeID = 1, MedalDifficultyID = 1 });
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
    public void DownloadMedals_NullResult_Throws()
    {
        _factory.Medals().Returns(new HaloApiResultContainer<MedalMetadataResponse, HaloApiErrorContainer>(null, null));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.DownloadMedals());

        Assert.That(ex!.Message, Is.EqualTo("Result null. Failed to get medals"));
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_EmptyList_DoesNothing()
    {
        await _service.DownloadRecentMatchesForPlayers([]);

        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public void DownloadRecentMatchesForPlayers_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => _service.DownloadRecentMatchesForPlayers([1], ct: cts.Token));
        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_HandlesNullEmptyAndFilteredHistory()
    {
        var newMatch = Guid.NewGuid();
        var alreadyDownloaded = Guid.NewGuid();
        var missingStats = Guid.NewGuid();
        var otherMode = Guid.NewGuid();
        _context.Matches.Add(new Match { MatchID = alreadyDownloaded });
        await _context.SaveChangesAsync();

        PlayerMatchHistoryRecord Record(string id, int category) => new() { MatchId = id, MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)category } };

        // Player 1: API failure. Player 2: matches. Player 3: no matches at all.
        _factory.StatsGetMatchHistory("xuid(1)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(new HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>(null, null));
        // Both pages of player 2 return the same records (a page with no results would cancel the other page and race).
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
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 2, Gamertag = "existing" });
        await _context.SaveChangesAsync();
        _getsert.GetsertXboxUserByXuid(2, Arg.Any<CancellationToken>()).Returns(_ => _context.XboxUsers.Single(x => x.XboxUserID == 2));

        await _service.DownloadRecentMatchesForPlayers([1, 2, 2, 3], startPage: 0, endPage: 1, perPage: 5);

        var saved = await _context.Matches.Select(x => x.MatchID).ToListAsync();
        var warnings = _logger.Entries.Where(x => x.Level == LogLevel.Warning).Select(x => x.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.EquivalentTo(new[] { alreadyDownloaded, newMatch }));
            Assert.That(warnings, Has.Some.EqualTo("Failed to get match history for user xuid(1)"));
            Assert.That(warnings, Has.Some.EqualTo("Detected 0 matches for user xuid(3) bailing out"));
            Assert.That(warnings, Has.Some.EqualTo("Could not parse Guid not-a-guid from player history for player xuid(2)"));
            Assert.That(warnings, Has.Some.EqualTo($"Match {missingStats} not found"));
            Assert.That(warnings, Has.Some.EqualTo($"Failed to get match {missingStats}"));
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
}
