using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
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

    private async Task<MatchmakingSeason> SeedSeasonWithStoredStat(string gamertag, int userID)
    {
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
