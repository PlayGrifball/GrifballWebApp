using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// LeaderboardsService against the fake IApplicationDbContext: a player's rank and the search it can be found by.
/// IDistributedCache is substituted (a miss every time, as BrowseCommandsHandlerUnitTests.DeepHandler does for the
/// same service in GrifballWebApp.Server.Test).
/// </summary>
[TestFixture]
internal sealed class LeaderboardsServiceUnitTests : ApplicationDbContextTests
{
    // GameVersionConfiguration seeds "Halo Infinite" through HasData (gameVersionID 6); EnsureCreated() applies
    // it, so a test reuses the existing row rather than inserting its own (which would violate its unique key and
    // name/slug indexes), as UserManagementServiceUnitTests' WestNorthAmericaRegionID does for Regions.
    private const int HaloInfiniteGameVersionID = 6;

    private LeaderboardsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var cache = Substitute.For<IDistributedCache>();
        _service = new LeaderboardsService(Context, new SeasonStatsService(Context), new MatchmakingStatsService(Context), cache, Substitute.For<ILogger<LeaderboardsService>>());
    }

    // GetAsync isn't covered here. Its Compute reads a team's titles as a nested collection (Tiers, for the
    // Dynasties board) from a query already filtered by that same collection (t.PlayerChampionships.Any()): on SQL
    // Server and Postgres this is a plain correlated subquery, but SQLite has no APPLY/LATERAL, so EF Core needs a
    // service only Infrastructure's DbContextOptionsBuilder wires up (DatabaseProviderExtensions.UseGrifballDatabase
    // replaces IQueryTranslationPostprocessorFactory with SqliteQueryTranslationPostprocessorFactory for exactly
    // this). FakeApplicationDbContext, built on plain UseSqlite() so this layer's tests stay out of Infrastructure,
    // doesn't have it, so GetAsync throws "Translating this query requires the SQL APPLY operation, which is not
    // supported on SQLite" here, whatever data is seeded (the translation fails before any row is read). See
    // UserManagementServiceUnitTests' GetUsers/GetUser note for the same gap. GrifballWebApp.Integration.Test
    // covers GetAsync (rings, dynasties, the career boards and the cache write) against the real providers.

    private async Task<(User A, User B)> SeedTwoPlayersWithCareerStats()
    {
        var season = Season.Seed(seasonName: "Career");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var userA = User.Seed(userName: "alice", id: 1);
        var userB = User.Seed(userName: "bob", id: 2);
        Context.Users.AddRange(userA, userB);
        await Context.SaveChangesAsync();
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Alice", GamesPlayed = 10, Wins = 8, Losses = 2, Kills = 100, Deaths = 20, Goals = 15, CarrierKills = 5 },
            seasonID: season.SeasonID, userID: userA.Id, gameVersionID: HaloInfiniteGameVersionID));
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Bob", GamesPlayed = 5, Wins = 1, Losses = 4, Kills = 20, Deaths = 30, Goals = 3, CarrierKills = 1 },
            seasonID: season.SeasonID, userID: userB.Id, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();
        return (userA, userB);
    }

    [Test]
    public async Task GetPlayerRankAsync_ByUserID_ReturnsRankDetails()
    {
        var (userA, _) = await SeedTwoPlayersWithCareerStats();

        var rank = await _service.GetPlayerRankAsync(query: null, userId: userA.Id, format: null);

        Assert.That(rank, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rank!.UserID, Is.EqualTo(userA.Id));
            Assert.That(rank.Kills.Rank, Is.EqualTo(1));
            Assert.That(rank.Kills.StatValue, Is.EqualTo(100));
            Assert.That(rank.GamesPlayed.Rank, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetPlayerRankAsync_UnknownUserID_ReturnsNull()
    {
        var rank = await _service.GetPlayerRankAsync(query: null, userId: 999, format: null);

        Assert.That(rank, Is.Null);
    }

    [Test]
    public async Task GetPlayerRankAsync_NoUserIdAndBlankQuery_ReturnsNull()
    {
        var rank = await _service.GetPlayerRankAsync(query: "   ", userId: null, format: null);

        Assert.That(rank, Is.Null);
    }

    [Test]
    public async Task GetPlayerRankAsync_ByGamertagQuery_FindsPlayer()
    {
        var xbox = XboxUser.FromXbox(1, "Chief117");
        Context.XboxUsers.Add(xbox);
        var user = User.Seed(userName: "chief", xboxUser: xbox, id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var rank = await _service.GetPlayerRankAsync(query: "Chief117", userId: null, format: null);

        Assert.That(rank, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rank!.UserID, Is.EqualTo(1));
            Assert.That(rank.Gamertag, Is.EqualTo("Chief117"));
        }
    }

    [Test]
    public async Task GetPlayerRankAsync_QueryMatchesNobody_ReturnsNull()
    {
        var rank = await _service.GetPlayerRankAsync(query: "nobody-here", userId: null, format: null);

        Assert.That(rank, Is.Null);
    }

    [Test]
    public async Task SearchPlayersAsync_DelegatesToQuickSearchService()
    {
        var xbox = XboxUser.FromXbox(1, "Chief117");
        Context.XboxUsers.Add(xbox);
        Context.Users.Add(User.Seed(userName: "chief", xboxUser: xbox, id: 1));
        await Context.SaveChangesAsync();

        var result = await _service.SearchPlayersAsync("Chief");

        Assert.That(result.Single().UserID, Is.EqualTo(1));
    }
}
