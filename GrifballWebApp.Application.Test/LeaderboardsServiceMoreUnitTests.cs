using System.Text.Json;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// LeaderboardsService's cache (IDistributedCache, substituted) and the rings board in a player's rank, which
/// LeaderboardsServiceUnitTests leaves out: boards and careers served from the cache, a cache that can't be read or
/// written skipped, titles counted over every format and career stats over the format asked. Not covered: GetAsync
/// computing the boards (Compute), whose dynasties query needs the SQL APPLY operation SQLite lacks (see
/// LeaderboardsServiceUnitTests' note; Integration.Test covers it), and GetPlayerRankAsync dropping a matched alias
/// that is the player's own name (the toolbar search never labels a player's current name as a past one).
/// </summary>
[TestFixture]
internal sealed class LeaderboardsServiceMoreUnitTests : ApplicationDbContextTests
{
    private const int HaloInfiniteGameVersionID = 6; // GameVersionConfiguration seeds this through HasData
    private const string CareersKey = "leaderboards:careers:all";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] CachedKeys = [CareersKey, "leaderboards:rings:all"];

    private IDistributedCache _cache = null!;
    private LeaderboardsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = Substitute.For<IDistributedCache>();
        _service = new LeaderboardsService(Context, new SeasonStatsService(Context), new MatchmakingStatsService(Context), _cache, Substitute.For<ILogger<LeaderboardsService>>());
    }

    /// <summary>Alice (10 games, 100 kills, 15 goals) and Bob (5 games, 20 kills), one season's stored lines; Alice won its
    /// Pro title with the Hammers.</summary>
    private async Task SeedTwoPlayersAndATitle()
    {
        var season = Season.Seed(seasonName: "Career");
        Context.Seasons.Add(season);
        Context.Users.AddRange(User.Seed(userName: "alice", id: 1), User.Seed(userName: "bob", id: 2));
        await Context.SaveChangesAsync();
        var team = Team.Seed(teamName: "Hammers", seasonID: season.SeasonID);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Alice", GamesPlayed = 10, Wins = 8, Losses = 2, Kills = 100, Deaths = 20, Goals = 15, CarrierKills = 5 },
            seasonID: season.SeasonID, userID: 1, gameVersionID: HaloInfiniteGameVersionID));
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Bob", GamesPlayed = 5, Wins = 1, Losses = 4, Kills = 20, Deaths = 30, Goals = 3, CarrierKills = 1 },
            seasonID: season.SeasonID, userID: 2, gameVersionID: HaloInfiniteGameVersionID));
        Context.Set<PlayerChampionship>().Add(PlayerChampionship.Seed(seasonID: season.SeasonID, teamID: team.TeamID, userID: 1, tier: "Pro", gamertagUsed: "Alice"));
        await Context.SaveChangesAsync();
    }

    [TestCase(null, "all")]
    [TestCase(new LeaderboardFilter[0], "all")]
    [TestCase(new[] { LeaderboardFilter.LateLeague, LeaderboardFilter.LeagueSeason, LeaderboardFilter.Tournament }, "all")]
    [TestCase(new[] { LeaderboardFilter.Tournament }, "Tournament")]
    [TestCase(new[] { LeaderboardFilter.LateLeague, LeaderboardFilter.LeagueSeason, LeaderboardFilter.LateLeague }, "LeagueSeason+LateLeague")]
    [TestCase(new[] { (LeaderboardFilter)99, LeaderboardFilter.Tournament }, "Tournament")]
    public async Task GetAsync_CachedBoards_AreReadFromTheCache_KeyedByTheFormatsInOrder(LeaderboardFilter[]? formats, string formatKey)
    {
        var boards = new LeaderboardsDto
        {
            RingLeaders = [new RingLeaderDto { Rank = 1, UserID = 1, Gamertag = "Alice", RingsCount = 2, Titles = [new TitleDto { SeasonID = 3, SeasonName = "S3", Tier = "Pro", TeamID = 4 }] }],
            Dynasties = [new DynastyDto { Rank = 1, TeamName = "Hammers", LatestTeamID = 4, LatestSeasonID = 3, ChampionshipsCount = 2, SeasonsActive = 2 }],
            GoalLeaders = [new MetricLeaderDto { Rank = 1, UserID = 1, Gamertag = "Alice", GameVersionName = "Halo Infinite", StatValue = 12, GamesPlayed = 10, GamesDecidedByVote = 1, PerGame = 1.33m, SeasonsCount = 2, SecondaryStat = "10 GP" }],
            KillLeaders = [new MetricLeaderDto { Rank = 1, UserID = 2, Gamertag = "Bob", StatValue = 100 }],
            GamesPlayedLeaders = [new MetricLeaderDto { Rank = 1, UserID = 1, Gamertag = "Alice", StatValue = 10 }],
            CarrierStopLeaders = [new MetricLeaderDto { Rank = 2, UserID = 2, Gamertag = "Bob", StatValue = 4 }],
        };
        var cached = JsonSerializer.SerializeToUtf8Bytes(boards, WebJson);
        _cache.GetAsync($"leaderboards:boards:{formatKey}", Arg.Any<CancellationToken>()).Returns(cached);

        var result = await _service.GetAsync(formats);

        var ringLeader = result.RingLeaders.Single();
        var dynasty = result.Dynasties.Single();
        var goals = result.GoalLeaders.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((ringLeader.Gamertag, ringLeader.RingsCount, ringLeader.Titles.Single().Tier, ringLeader.Titles.Single().TeamID), Is.EqualTo(("Alice", 2, "Pro", 4)));
            Assert.That((dynasty.TeamName, dynasty.LatestTeamID, dynasty.LatestSeasonID, dynasty.ChampionshipsCount, dynasty.SeasonsActive), Is.EqualTo(("Hammers", 4, 3, 2, 2)));
            Assert.That((goals.GameVersionName, goals.StatValue, goals.GamesDecidedByVote, goals.PerGame, goals.SeasonsCount, goals.SecondaryStat),
                Is.EqualTo(("Halo Infinite", 12L, 1, (decimal?)1.33m, 2, "10 GP")));
            Assert.That(result.KillLeaders.Single().Gamertag, Is.EqualTo("Bob"));
            Assert.That(result.GamesPlayedLeaders.Single().StatValue, Is.EqualTo(10));
            Assert.That(result.CarrierStopLeaders.Single().Rank, Is.EqualTo(2));
        }
        await _cache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPlayerRankAsync_RanksTitlesAndStatsOfTheFormatsAsked()
    {
        await SeedTwoPlayersAndATitle();

        var alice = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: null);
        var bob = await _service.GetPlayerRankAsync(query: null, userId: 2, formats: [LeaderboardFilter.LeagueSeason]);
        var aliceInTournaments = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: [LeaderboardFilter.Tournament]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((alice!.Rings.Rank, alice.Rings.TotalRanked, alice.Rings.StatValue, alice.Rings.SecondaryText), Is.EqualTo(((int?)1, 1, 1L, (string?)null)));
            Assert.That((bob!.Rings.Rank, bob.Rings.TotalRanked, bob.Rings.StatValue), Is.EqualTo(((int?)null, 1, 0L)), "no title: not on the board");
            Assert.That((aliceInTournaments!.Rings.Rank, aliceInTournaments.Rings.TotalRanked, aliceInTournaments.Rings.StatValue), Is.EqualTo(((int?)null, 0, 0L)), "no tournament titles");
            Assert.That((aliceInTournaments.Goals.Rank, aliceInTournaments.Goals.StatValue), Is.EqualTo(((int?)null, 0L)), "no tournament games");
            Assert.That((alice.GamesPlayed.StatValue, alice.GamesPlayed.SecondaryText), Is.EqualTo((10L, "1 Season")));
            Assert.That((bob.GamesPlayed.Rank, bob.GamesPlayed.TotalRanked), Is.EqualTo(((int?)2, 2)));
            Assert.That((alice.Goals.Rank, alice.Goals.StatValue), Is.EqualTo(((int?)1, 15L)));
            Assert.That(alice.Goals.SecondaryText, Is.EqualTo("1.5 / G"), "goals summed across games, no game named");
            Assert.That((bob.CarrierStops.Rank, bob.CarrierStops.StatValue), Is.EqualTo(((int?)2, 1L)));
        }
    }

    [Test]
    public async Task GetPlayerRankAsync_CacheCantBeRead_ComputesAndCachesAnyway()
    {
        await SeedTwoPlayersAndATitle();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Redis is down"));

        var rank = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: null);

        Assert.That((rank!.Kills.Rank, rank.Kills.StatValue, rank.Rings.StatValue), Is.EqualTo(((int?)1, 100L, 1L)));
        await _cache.Received(1).SetAsync(CareersKey, Arg.Any<byte[]>(), Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(5)), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPlayerRankAsync_CacheCantBeWritten_ReturnsTheRankAnyway()
    {
        await SeedTwoPlayersAndATitle();
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis is down"));

        var rank = await _service.GetPlayerRankAsync(query: null, userId: 2, formats: null);

        Assert.That((rank!.Kills.Rank, rank.Kills.StatValue, rank.GamesPlayed.Rank), Is.EqualTo(((int?)2, 20L, (int?)2)));
    }

    [Test]
    public async Task GetPlayerRankAsync_CareersAndTitlesCached_AreReadBackFromTheCache()
    {
        await SeedTwoPlayersAndATitle();
        // A cache that keeps what is written to it, as Redis would
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        _cache.When(c => c.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>()))
            .Do(call => stored[call.ArgAt<string>(0)] = call.ArgAt<byte[]>(1));
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => stored.GetValueOrDefault(call.ArgAt<string>(0)));
        var computed = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: null);

        // The database's stats and titles gone: only the cache has them now
        await Context.PlayerSeasonStats.ExecuteDeleteAsync();
        await Context.Set<PlayerChampionship>().ExecuteDeleteAsync();
        var cached = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Keys, Is.EquivalentTo(CachedKeys));
            Assert.That((cached!.Kills.Rank, cached.Kills.TotalRanked, cached.Kills.StatValue), Is.EqualTo((computed!.Kills.Rank, computed.Kills.TotalRanked, 100L)));
            Assert.That((cached.Goals.StatValue, cached.Goals.SecondaryText), Is.EqualTo((computed.Goals.StatValue, computed.Goals.SecondaryText)));
            Assert.That((cached.GamesPlayed.StatValue, cached.GamesPlayed.TotalRanked), Is.EqualTo((10L, 2)));
            Assert.That((cached.Rings.Rank, cached.Rings.StatValue), Is.EqualTo(((int?)1, 1L)));
        }
    }

    /// <summary>Alice's 8 Late League games (40 kills, 3 goals), stored totals of a matchmaking season.</summary>
    private async Task SeedAliceInLateLeague()
    {
        var season = GrifballWebApp.Domain.LateLeague.MatchmakingSeason.Seed("MM Season 1", gameVersionID: HaloInfiniteGameVersionID);
        Context.MatchmakingSeasons.Add(season);
        await Context.SaveChangesAsync();
        Context.Add(GrifballWebApp.Domain.LateLeague.MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Alice", GamesPlayed = 8, Wins = 5, Losses = 3, Kills = 40, Deaths = 20, Goals = 3 },
            matchmakingSeasonID: season.MatchmakingSeasonID, userID: 1, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetPlayerRankAsync_EveryFormat_CountsLateLeague_AndLateLeagueAloneHasNoTitles()
    {
        await SeedTwoPlayersAndATitle();
        await SeedAliceInLateLeague();

        var every = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: null);
        var lateLeague = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: [LeaderboardFilter.LateLeague]);
        var seasons = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: [LeaderboardFilter.LeagueSeason, LeaderboardFilter.Tournament]);
        var leagueAndLateLeague = await _service.GetPlayerRankAsync(query: null, userId: 1, formats: [LeaderboardFilter.LateLeague, LeaderboardFilter.LeagueSeason]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((every!.GamesPlayed.StatValue, every.Kills.StatValue, every.Goals.StatValue, every.Rings.StatValue), Is.EqualTo((18L, 140L, 18L, 1L)), "the season and Late League");
            Assert.That((lateLeague!.GamesPlayed.StatValue, lateLeague.Kills.StatValue, lateLeague.Goals.StatValue), Is.EqualTo((8L, 40L, 3L)));
            Assert.That((lateLeague.Rings.StatValue, lateLeague.Rings.TotalRanked), Is.EqualTo((0L, 0)), "Late League has no titles");
            Assert.That((seasons!.GamesPlayed.StatValue, seasons.Rings.StatValue), Is.EqualTo((10L, 1L)), "the seasons alone");
            Assert.That((leagueAndLateLeague!.GamesPlayed.StatValue, leagueAndLateLeague.Rings.StatValue), Is.EqualTo((18L, 1L)));
        }
    }
}
