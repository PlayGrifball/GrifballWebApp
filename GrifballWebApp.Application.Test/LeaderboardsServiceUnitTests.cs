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
/// LeaderboardsService against the fake IApplicationDbContext: the all-time boards and a player's rank on them.
/// IDistributedCache is substituted (a miss every time, as BrowseCommandsHandlerUnitTests.DeepHandler does for the
/// same service in GrifballWebApp.Server.Test) so every call computes; one test checks what it writes back.
/// </summary>
[TestFixture]
internal sealed class LeaderboardsServiceUnitTests : ApplicationDbContextTests
{
    private IDistributedCache _cache = null!;
    private LeaderboardsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = Substitute.For<IDistributedCache>();
        _service = new LeaderboardsService(Context, new SeasonStatsService(Context), new MatchmakingStatsService(Context), _cache, Substitute.For<ILogger<LeaderboardsService>>());
    }

    private async Task<(User A, User B)> SeedTwoPlayersWithCareerStats()
    {
        var season = Season.Seed(seasonName: "Career");
        Context.Seasons.Add(season);
        var gv = GameVersion.Seed("Halo Infinite", "infinite", gameVersionID: 1);
        Context.GameVersions.Add(gv);
        await Context.SaveChangesAsync();
        var userA = User.Seed(userName: "alice", id: 1);
        var userB = User.Seed(userName: "bob", id: 2);
        Context.Users.AddRange(userA, userB);
        await Context.SaveChangesAsync();
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Alice", GamesPlayed = 10, Wins = 8, Losses = 2, Kills = 100, Deaths = 20, Goals = 15, CarrierKills = 5 },
            seasonID: season.SeasonID, userID: userA.Id, gameVersionID: gv.GameVesionID));
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Bob", GamesPlayed = 5, Wins = 1, Losses = 4, Kills = 20, Deaths = 30, Goals = 3, CarrierKills = 1 },
            seasonID: season.SeasonID, userID: userB.Id, gameVersionID: gv.GameVesionID));
        await Context.SaveChangesAsync();
        return (userA, userB);
    }

    [Test]
    public async Task GetAsync_NoData_ReturnsEmptyBoards()
    {
        var result = await _service.GetAsync(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.RingLeaders, Is.Empty);
            Assert.That(result.Dynasties, Is.Empty);
            Assert.That(result.GoalLeaders, Is.Empty);
            Assert.That(result.KillLeaders, Is.Empty);
            Assert.That(result.GamesPlayedLeaders, Is.Empty);
            Assert.That(result.CarrierStopLeaders, Is.Empty);
        }
    }

    [Test]
    public async Task GetAsync_RingLeaders_RanksByChampionshipsCountDescending()
    {
        var season1 = Season.Seed(seasonName: "S1");
        var season2 = Season.Seed(seasonName: "S2");
        Context.Seasons.AddRange(season1, season2);
        await Context.SaveChangesAsync();
        var teamA1 = Team.Seed(seasonID: season1.SeasonID, teamName: "A1");
        var teamA2 = Team.Seed(seasonID: season2.SeasonID, teamName: "A2");
        var teamB1 = Team.Seed(seasonID: season1.SeasonID, teamName: "B1");
        Context.Teams.AddRange(teamA1, teamA2, teamB1);
        var userA = User.Seed(userName: "alice", id: 1);
        var userB = User.Seed(userName: "bob", id: 2);
        Context.Users.AddRange(userA, userB);
        await Context.SaveChangesAsync();
        Context.Add(PlayerChampionship.Seed(seasonID: season1.SeasonID, teamID: teamA1.TeamID, userID: userA.Id, tier: Season.DefaultChampionTier, gamertagUsed: "alice"));
        Context.Add(PlayerChampionship.Seed(seasonID: season2.SeasonID, teamID: teamA2.TeamID, userID: userA.Id, tier: Season.DefaultChampionTier, gamertagUsed: "alice"));
        Context.Add(PlayerChampionship.Seed(seasonID: season1.SeasonID, teamID: teamB1.TeamID, userID: userB.Id, tier: Season.DefaultChampionTier, gamertagUsed: "bob"));
        await Context.SaveChangesAsync();

        var result = await _service.GetAsync(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.RingLeaders, Has.Count.EqualTo(2));
            Assert.That(result.RingLeaders[0].UserID, Is.EqualTo(userA.Id));
            Assert.That(result.RingLeaders[0].RingsCount, Is.EqualTo(2));
            Assert.That(result.RingLeaders[0].Rank, Is.EqualTo(1));
            Assert.That(result.RingLeaders[0].Titles, Has.Count.EqualTo(2));
            Assert.That(result.RingLeaders[1].UserID, Is.EqualTo(userB.Id));
            Assert.That(result.RingLeaders[1].RingsCount, Is.EqualTo(1));
            Assert.That(result.RingLeaders[1].Rank, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task GetAsync_Dynasties_GroupsTeamsByFranchiseAcrossSeasons()
    {
        var seasonX = Season.Seed(seasonName: "X");
        var seasonY = Season.Seed(seasonName: "Y");
        Context.Seasons.AddRange(seasonX, seasonY);
        await Context.SaveChangesAsync();
        var teamX = Team.Seed(seasonID: seasonX.SeasonID, teamName: "Dynasty");
        var teamY = Team.Seed(seasonID: seasonY.SeasonID, teamName: "Dynasty");
        Context.Teams.AddRange(teamX, teamY);
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        Context.Add(PlayerChampionship.Seed(seasonID: seasonX.SeasonID, teamID: teamX.TeamID, userID: user.Id, tier: Season.DefaultChampionTier, gamertagUsed: "alice"));
        Context.Add(PlayerChampionship.Seed(seasonID: seasonY.SeasonID, teamID: teamY.TeamID, userID: user.Id, tier: Season.DefaultChampionTier, gamertagUsed: "alice"));
        await Context.SaveChangesAsync();

        var result = await _service.GetAsync(null);

        var dynasty = result.Dynasties.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dynasty.TeamName, Is.EqualTo("Dynasty"));
            Assert.That(dynasty.ChampionshipsCount, Is.EqualTo(2));
            Assert.That(dynasty.SeasonsActive, Is.EqualTo(2));
            Assert.That(dynasty.Rank, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetAsync_CareerLeaderboards_RankPlayersFromStoredSeasonTotals()
    {
        var (userA, _) = await SeedTwoPlayersWithCareerStats();

        var result = await _service.GetAsync(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.KillLeaders[0].UserID, Is.EqualTo(userA.Id));
            Assert.That(result.KillLeaders[0].StatValue, Is.EqualTo(100));
            Assert.That(result.KillLeaders[0].Rank, Is.EqualTo(1));
            Assert.That(result.GamesPlayedLeaders[0].UserID, Is.EqualTo(userA.Id));
            Assert.That(result.GamesPlayedLeaders[0].StatValue, Is.EqualTo(10));
            Assert.That(result.GoalLeaders[0].UserID, Is.EqualTo(userA.Id));
            Assert.That(result.GoalLeaders[0].GameVersionName, Is.EqualTo("Halo Infinite"));
            Assert.That(result.CarrierStopLeaders[0].UserID, Is.EqualTo(userA.Id));
            Assert.That(result.CarrierStopLeaders[0].StatValue, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task GetAsync_CachesComputedResult_WritesToDistributedCache()
    {
        await _service.GetAsync(null);

        await _cache.Received(1).SetAsync(
            "leaderboards:boards:all",
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
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
