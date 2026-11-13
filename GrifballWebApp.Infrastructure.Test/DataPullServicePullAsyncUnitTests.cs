using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using GruntMatchType = Surprenant.Grunt.Models.HaloInfinite.MatchType;
using Match = GrifballWebApp.Domain.Halo.Match;
using Team = Surprenant.Grunt.Models.HaloInfinite.Team;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DataPullService.PullAsync, reached through DownloadRecentMatchesForPlayers and DownloadMatchesBetween: the paging
/// loop, the already-downloaded and not-a-team-game skips, a page or a match Halo didn't answer, and the binary search
/// for a season's pages between two dates. On a real (temp-file SQLite) GrifballContext with a substituted
/// IHaloInfiniteClient: no database server, host or network. GrifballWebApp.Integration.Test keeps the same scenarios
/// (and the log-message assertions, which need its RecordingLogger) for its own layer's coverage.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DataPullServicePullAsyncUnitTests
{
    private string _path = null!;
    private GrifballContext _context = null!;
    private IHaloInfiniteClient _halo = null!;
    private IGetsertXboxUserService _getsert = null!;
    private DataPullService _service = null!;
    private static readonly string[] expectedArray = ["xuid(1)"];
    private static readonly string[] expectedArray0 = ["xuid(6)"];
    private static readonly string[] expectedArray1 = ["xuid(10)", "xuid(11)", "xuid(12)"];

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-pullasync-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        await _context.Database.EnsureCreatedAsync();
        _halo = Substitute.For<IHaloInfiniteClient>();
        _getsert = Substitute.For<IGetsertXboxUserService>();
        _getsert.GetsertXboxUserByXuid(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => XboxUser.FromXbox(ci.Arg<long>(), $"gt{ci.Arg<long>()}"));
        _service = new DataPullService(NullLogger<DataPullService>.Instance, _halo, _context, _getsert);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        // Surprenant.Grunt.Models.HaloInfinite has its own File type: qualified against System.IO's
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (System.IO.File.Exists(file))
                System.IO.File.Delete(file);
    }

    private static CoreStats Core(int score = 1) => new()
    {
        Score = score,
        AverageLifeDuration = TimeSpan.FromSeconds(10),
        Medals = [],
    };

    private static Player PlayerOf(string playerId, int lastTeam, params (int Team, int Score)[] teamStats) => new()
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

    private static MatchStats StatsOf(Guid id, IEnumerable<(int TeamId, int Outcome)> teams, params Player[] players) => new()
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

    private static HaloApiResultContainer<T, HaloApiErrorContainer> Ok<T>(T value) => new(value, null!);
    private static PlayerMatchHistoryRecord Record(string id, int category) => new() { MatchId = id, MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)category } };

    [Test]
    public async Task DownloadRecentMatchesForPlayers_HandlesNullEmptyAndFilteredHistory()
    {
        var newMatch = Guid.NewGuid();
        var alreadyDownloaded = Guid.NewGuid();
        var missingStats = Guid.NewGuid();
        var otherMode = Guid.NewGuid();
        _context.Matches.Add(Match.Seed(alreadyDownloaded));
        await _context.SaveChangesAsync();

        // Player 1: API failure. Player 2: matches. Player 3: no matches at all.
        _halo.StatsGetMatchHistory("xuid(1)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(new HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>(null!, null!));
        // Both pages of player 2 return the same records.
        _halo.StatsGetMatchHistory("xuid(2)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
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
        _halo.StatsGetMatchHistory("xuid(3)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Ok(new MatchHistoryResponse { Results = [] }));
        _halo.StatsGetMatchStats(newMatch).Returns(Ok(StatsOf(newMatch, [(0, 2), (1, 3)], PlayerOf("xuid(2)", 0, (0, 5)), PlayerOf("xuid(9)", 1, (1, 2)))));
        _halo.StatsGetMatchStats(missingStats).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, null!));
        _context.XboxUsers.Add(XboxUser.FromXbox(2, "existing"));
        await _context.SaveChangesAsync();
        _getsert.GetsertXboxUserByXuid(2, Arg.Any<CancellationToken>()).Returns(_ => _context.XboxUsers.Single(x => x.XboxUserID == 2));

        var result = await _service.DownloadRecentMatchesForPlayers([1, 2, 2, 3], startPage: 0, endPage: 1, perPage: 5);

        var saved = await _context.Matches.Select(x => x.MatchID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Is.EquivalentTo(new[] { alreadyDownloaded, newMatch }));
            Assert.That((result.Players, result.MatchesFound, result.MatchesAlreadySaved, result.MatchesFetched), Is.EqualTo((3, 3, 1, 1)));
            Assert.That(result.FailedHistories, Is.EqualTo(expectedArray));
            Assert.That(result.FailedMatches, Is.EqualTo(new[] { missingStats }));
            Assert.That(result.Complete, Is.False);
        }
        await _halo.DidNotReceive().StatsGetMatchStats(alreadyDownloaded);
        await _halo.DidNotReceive().StatsGetMatchStats(otherMode);
        // Duplicated xbox ids are only queried once per page
        await _halo.Received(1).StatsGetMatchHistory("xuid(2)", 0, 5, Arg.Any<GruntMatchType>());
        // Page 1 starts at index page * perPage - 1
        await _halo.Received(1).StatsGetMatchHistory("xuid(2)", 4, 5, Arg.Any<GruntMatchType>());
        // Only users that are not in the database yet are bulk fetched
        await _getsert.Received(1).GetsertXboxUsersByXuid(Arg.Is<long[]>(x => x.SequenceEqual(new long[] { 9 })), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A player's pages are read two at a time. An empty page 1 answering before page 0 only stops the pages after it,
    /// not page 0's own game.
    /// </summary>
    [Test]
    public async Task DownloadRecentMatchesForPlayers_AnEmptyPage_KeepsTheEarlierPages_AndSkipsTheLaterOnes()
    {
        var onPage0 = Guid.NewGuid();
        PlayerMatchHistoryRecord RecordOf(Guid id) => new() { MatchId = id.ToString(), MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41 } };
        var page1Answered = new TaskCompletionSource();
        _halo.StatsGetMatchHistory("xuid(5)", 0, 5, Arg.Any<GruntMatchType>()).Returns(async _ =>
        {
            await page1Answered.Task;
            await Task.Delay(50);
            return Ok(new MatchHistoryResponse { Results = [RecordOf(onPage0)] });
        });
        _halo.StatsGetMatchHistory("xuid(5)", 4, 5, Arg.Any<GruntMatchType>()).Returns(_ =>
        {
            page1Answered.SetResult();
            return Ok(new MatchHistoryResponse { Results = [] });
        });
        _halo.StatsGetMatchStats(onPage0).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, null!));

        var result = await _service.DownloadRecentMatchesForPlayers([5], startPage: 0, endPage: 3, perPage: 5);

        Assert.That(result.MatchesFound, Is.EqualTo(1), "Page 0's game, answered after page 1 was found empty");
        await _halo.DidNotReceive().StatsGetMatchHistory("xuid(5)", 9, 5, Arg.Any<GruntMatchType>());
        await _halo.DidNotReceive().StatsGetMatchHistory("xuid(5)", 14, 5, Arg.Any<GruntMatchType>());
    }

    /// <summary>
    /// No answer from Halo (the gateway unreachable, a timeout) for a page or a game: that page or game is missing, the
    /// rest of the pull saved. A game fetched but not a team game: not saved, nor counted as saved. The caller's context
    /// keeps noticing its own changes after.
    /// </summary>
    [Test]
    public async Task DownloadRecentMatchesForPlayers_NoAnswer_LosesOnlyThatPageOrGame_AndCountsWhatItSaved()
    {
        var saved = Guid.NewGuid();
        var unanswered = Guid.NewGuid();
        var notTeams = Guid.NewGuid();
        PlayerMatchHistoryRecord RecordOf(Guid id) => new() { MatchId = id.ToString(), MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41 } };
        _halo.StatsGetMatchHistory("xuid(6)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Task.FromException<HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>>(new HttpRequestException("gateway unreachable")));
        _halo.StatsGetMatchHistory("xuid(7)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>())
            .Returns(Ok(new MatchHistoryResponse { Results = [RecordOf(saved), RecordOf(unanswered), RecordOf(notTeams)] }));
        _halo.StatsGetMatchStats(saved).Returns(Ok(StatsOf(saved, [(0, 2), (1, 3)], PlayerOf("xuid(7)", 0, (0, 5)))));
        _halo.StatsGetMatchStats(unanswered).Returns(Task.FromException<HaloApiResultContainer<MatchStats, HaloApiErrorContainer>>(new TaskCanceledException("timed out")));
        _halo.StatsGetMatchStats(notTeams).Returns(Ok(StatsOf(notTeams, [], PlayerOf("xuid(7)", 0, (0, 5)))));
        // The caller's own entity, changed before the pull and saved after it
        var mine = XboxUser.FromXbox(70, "before");
        _context.XboxUsers.Add(mine);
        await _context.SaveChangesAsync();
        mine.Renamed("after", DateTime.UtcNow);

        var result = await _service.DownloadRecentMatchesForPlayers([6, 7], startPage: 0, endPage: 0, perPage: 5);
        await _context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Matches.Select(x => x.MatchID).ToListAsync(), Is.EqualTo(new[] { saved }));
            Assert.That((result.MatchesFound, result.MatchesFetched, result.MatchesNotTeamGames), Is.EqualTo((3, 1, 1)));
            Assert.That(result.FailedHistories, Is.EqualTo(expectedArray0));
            Assert.That(result.FailedMatches, Is.EqualTo(new[] { unanswered }));
            Assert.That(result.Summary, Does.EndWith("; 1 not team games, not saved"));
            Assert.That(_context.ChangeTracker.AutoDetectChangesEnabled, Is.True, "Back on for the caller");
            Assert.That(await _context.XboxUsers.AsNoTracking().Where(x => x.XboxUserID == 70).Select(x => x.Gamertag).SingleAsync(), Is.EqualTo("after"));
        }
    }

    /// <summary>
    /// Between two dates: a player's season is found under a long history without reading it from the newest page
    /// (pages 1, 2, 4, 8… then halving), and only the pages that reach into it are read for games.
    /// </summary>
    [Test]
    public async Task DownloadMatchesBetween_FindsTheSeasonsPagesInAFewCalls_AndReadsOnlyThem()
    {
        // 2,000 games, one an hour back from now: game i (0 the newest) at newest - i hours
        var newest = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var games = Enumerable.Range(0, 2000).Select(i => (ID: Guid.NewGuid(), Start: newest.AddHours(-i))).ToList();
        _halo.StatsGetMatchHistory("xuid(6)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>()).Returns(call => Ok(new MatchHistoryResponse
        {
            Results = games.Skip(call.ArgAt<int>(1)).Take(call.ArgAt<int>(2)).Select(g => new PlayerMatchHistoryRecord
            {
                MatchId = g.ID.ToString(),
                MatchInfo = new MatchInfo { GameVariantCategory = (GameVariantCategory)41, StartTime = new DateTimeOffset(g.Start) },
            }).ToArray(),
        }));
        _halo.StatsGetMatchHistory("xuid(7)", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>()).Returns(Ok(new MatchHistoryResponse { Results = [] }));
        _halo.StatsGetMatchStats(Arg.Any<Guid>()).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, null!));
        // The season: games 1150 to 1210, on pages 46 (games 1150-1174) to 48 (1200-1224)
        var from = newest.AddHours(-1210);
        var to = newest.AddHours(-1150);

        var result = await _service.DownloadMatchesBetween([6, 7], from, to);
        var capped = await _service.DownloadMatchesBetween([6], from, to, maxCalls: 3);

        var calls = _halo.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "StatsGetMatchHistory" && (string)c.GetArguments()[0]! == "xuid(6)");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MatchesFound, Is.EqualTo(75), "Pages 46 to 48: the season's games and the rest of its last page");
            Assert.That(calls - 3, Is.LessThan(20), "Found in a few calls, not 49 pages read (the 3 are the capped run's)");
            await _halo.DidNotReceive().StatsGetMatchHistory("xuid(6)", 10 * DataPullService.HistoryPageSize, Arg.Any<int>(), Arg.Any<GruntMatchType>());
            await _halo.Received(1).StatsGetMatchHistory("xuid(6)", 47 * DataPullService.HistoryPageSize, DataPullService.HistoryPageSize, Arg.Any<GruntMatchType>());
            Assert.That(result.FailedHistories, Is.Empty);
            Assert.That(result.CappedHistories, Is.Empty);
            Assert.That(capped.CappedHistories, Is.EqualTo(expectedArray0), "Ran out of calls before the season");
            Assert.That(capped.FailedHistories, Is.Empty, "Capped, not failed");
        }
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
            _halo.StatsGetMatchHistory(player, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GruntMatchType>()).Returns(call =>
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
        _halo.StatsGetMatchStats(Arg.Any<Guid>()).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, null!));

        var result = await _service.DownloadMatchesBetween([10, 11, 12, 13], from, to);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FailedHistories, Is.EquivalentTo(expectedArray1));
            Assert.That(result.CappedHistories, Is.Empty);
            Assert.That(result.MatchesFound, Is.EqualTo(page), "xuid(12)'s first season page, before the one Halo didn't give");
        }
    }
}
