using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// MatchmakingStatsService against the fake IApplicationDbContext, through its stored-totals path (a historical
/// sheet's). GetAsync's "fromGames" path (PlayerGameStatsQuery.Matchmaking: the queue's reported and vote-decided
/// matches) is GrifballWebApp.Integration.Test's.
/// </summary>
[TestFixture]
internal sealed class MatchmakingStatsServiceUnitTests : ApplicationDbContextTests
{
    private const int HaloInfiniteGameVersionID = 6; // GameVersionConfiguration seeds this through HasData
    private MatchmakingStatsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new MatchmakingStatsService(Context);
    }

    /// <summary>The player a stored row belongs to (MatchmakingPlayerStats.UserID is a foreign key to Users).</summary>
    private void SeedUser(int userID) => Context.Users.Add(User.Seed(userName: $"user{userID}", id: userID));

    private async Task<MatchmakingSeason> SeedSeasonWithStoredStat(string gamertag, int userID)
    {
        SeedUser(userID);
        var season = MatchmakingSeason.Seed("MM Season 1", gameVersionID: HaloInfiniteGameVersionID);
        Context.MatchmakingSeasons.Add(season);
        await Context.SaveChangesAsync();
        Context.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = gamertag, GamesPlayed = 8, Wins = 5, Losses = 3, Kills = 40, Deaths = 20 },
            matchmakingSeasonID: season.MatchmakingSeasonID, userID: userID, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task GetAsync_NoData_ReturnsEmptyList()
    {
        var lines = await _service.GetAsync();

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public async Task GetAsync_StoredRow_ReturnsItWithSeasonInfo()
    {
        var season = await SeedSeasonWithStoredStat("Chief117", userID: 1);

        var lines = await _service.GetAsync();

        var line = lines.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.MatchmakingSeasonID, Is.EqualTo(season.MatchmakingSeasonID));
            Assert.That(line.MatchmakingSeasonName, Is.EqualTo("MM Season 1"));
            Assert.That(line.Gamertag, Is.EqualTo("Chief117"));
            Assert.That(line.GamesPlayed, Is.EqualTo(8));
            Assert.That(line.FromMatches, Is.False);
        }
    }

    [Test]
    public async Task GetAsync_FiltersByMatchmakingSeasonID()
    {
        var season1 = await SeedSeasonWithStoredStat("A", userID: 1);
        var season2 = MatchmakingSeason.Seed("MM Season 2", gameVersionID: HaloInfiniteGameVersionID);
        Context.MatchmakingSeasons.Add(season2);
        SeedUser(2);
        await Context.SaveChangesAsync();
        Context.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "B", GamesPlayed = 3 }, matchmakingSeasonID: season2.MatchmakingSeasonID, userID: 2, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();

        var lines = await _service.GetAsync(matchmakingSeasonID: season1.MatchmakingSeasonID);

        Assert.That(lines.Single().Gamertag, Is.EqualTo("A"));
    }

    [Test]
    public async Task GetSeasonsAsync_ReturnsSeasonsWithPlayerCount()
    {
        await SeedSeasonWithStoredStat("Chief117", userID: 1);

        var seasons = await _service.GetSeasonsAsync();

        var season = seasons.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.Name, Is.EqualTo("MM Season 1"));
            Assert.That(season.PlayerCount, Is.EqualTo(1));
            Assert.That(season.GameVersionName, Is.EqualTo("Halo Infinite"));
        }
    }

    [Test]
    public async Task GetSeasonAsync_NoSuchSeason_ReturnsNull()
    {
        var result = await _service.GetSeasonAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSeasonAsync_Found_ReturnsSeasonWithItsPlayers()
    {
        var season = await SeedSeasonWithStoredStat("Chief117", userID: 1);

        var result = await _service.GetSeasonAsync(season.MatchmakingSeasonID);

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Season.MatchmakingSeasonID, Is.EqualTo(season.MatchmakingSeasonID));
            Assert.That(result.Season.PlayerCount, Is.EqualTo(1));
            Assert.That(result.Players.Single().Gamertag, Is.EqualTo("Chief117"));
        }
    }

    private static readonly string[] LadderTopFirst = ["Chief117", "Second", "No Place High", "No Place High Too", "No Place Low"];

    [Test]
    public async Task GetSeasonAsync_ReturnsItsLadder_TopFirst_AndTheSeasonsCountIt()
    {
        var season = await SeedSeasonWithStoredStat("Chief117", userID: 1);
        foreach (var id in new[] { 2, 3, 4, 5 })
            SeedUser(id);
        await Context.SaveChangesAsync();
        // Out of order: a place first, then (no place) by MMR, then LP, then gamertag
        Context.MatchmakingLadderRanks.AddRange(
            MatchmakingLadderRank.FromSheet(season.MatchmakingSeasonID, 2, new LadderStanding { Gamertag = "No Place Low", Mmr = 100 }),
            MatchmakingLadderRank.FromSheet(season.MatchmakingSeasonID, 3, new LadderStanding { Gamertag = "Second", Position = 2, League = "Gold 1", Mmr = 500 }),
            MatchmakingLadderRank.FromSheet(season.MatchmakingSeasonID, 4, new LadderStanding { Gamertag = "No Place High", Mmr = 900, LeaguePoints = 3 }),
            MatchmakingLadderRank.FromSheet(season.MatchmakingSeasonID, 1, new LadderStanding { Gamertag = "Chief117", Position = 1, League = "Masters 3", Rank = 3, LeaguePoints = 56, Streak = -2, Mmr = 850, Wins = 15, Losses = 9, Division = "260" }),
            MatchmakingLadderRank.FromSheet(season.MatchmakingSeasonID, 5, new LadderStanding { Gamertag = "No Place High Too", Mmr = 900, LeaguePoints = 3 }));
        await Context.SaveChangesAsync();

        var result = await _service.GetSeasonAsync(season.MatchmakingSeasonID);
        var seasons = await _service.GetSeasonsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Ladder.Select(r => r.Gamertag), Is.EqualTo(LadderTopFirst));
            var top = result.Ladder[0];
            Assert.That((top.UserID, top.Position, top.Division, top.League, top.Rank, top.LeaguePoints, top.Streak, top.Mmr, top.Wins, top.Losses),
                Is.EqualTo((1, (int?)1, "260", "Masters 3", (int?)3, (int?)56, (int?)-2, (int?)850, (int?)15, (int?)9)));
            Assert.That((seasons.Single().LadderCount, seasons.Single().PlayerCount), Is.EqualTo((5, 1)));
        }
    }

    [Test]
    public async Task GetQueueAsync_EmptyQueue_ReturnsZeroCounts()
    {
        var result = await _service.GetQueueAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GameCount, Is.Zero);
            Assert.That(result.VoteDecidedGameCount, Is.Zero);
            Assert.That(result.Players, Is.Empty);
        }
    }
}
