using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// SeasonStatsService.GetAsync against the fake IApplicationDbContext, through its stored-totals path only
/// (PlayerSeasonStat, a historical sheet's): FromMatchesAsync's PlayerGameStatsQuery is GradesService-adjacent plumbing
/// GrifballWebApp.Integration.Test already covers against the real providers.
/// </summary>
[TestFixture]
internal sealed class SeasonStatsServiceUnitTests : ApplicationDbContextTests
{
    private const int HaloInfiniteGameVersionID = 6; // GameVersionConfiguration seeds this through HasData
    private SeasonStatsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new SeasonStatsService(Context, TimeProvider.System);
    }

    /// <summary>The player a stored row belongs to (PlayerSeasonStats.UserID is a foreign key to Users).</summary>
    private void SeedUser(int userID) => Context.Users.Add(User.Seed(userName: $"user{userID}", id: userID));

    private async Task<Season> SeedSeasonWithStoredStat(int userID, string gamertag, int? teamID = null, bool isPlayoffs = false, bool isCombined = false)
    {
        SeedUser(userID);
        var season = Season.Seed(seasonName: "S1");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        if (teamID is not null)
            Context.Teams.Add(Team.Seed(seasonID: season.SeasonID, teamName: "Team", teamID: teamID.Value));
        await Context.SaveChangesAsync();
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = gamertag, GamesPlayed = 10, Wins = 7, Losses = 3, Kills = 50, Deaths = 20 },
            seasonID: season.SeasonID, userID: userID, gameVersionID: HaloInfiniteGameVersionID, teamID: teamID, isPlayoffs: isPlayoffs, isCombined: isCombined));
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
    public async Task GetAsync_StoredRow_ReturnsItWithSeasonAndTeamInfo()
    {
        var season = await SeedSeasonWithStoredStat(userID: 1, gamertag: "Chief117", teamID: 10);

        var lines = await _service.GetAsync();

        var line = lines.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(line.Gamertag, Is.EqualTo("Chief117"));
            Assert.That(line.TeamID, Is.EqualTo(10));
            Assert.That(line.TeamName, Is.EqualTo("Team"));
            Assert.That(line.GamesPlayed, Is.EqualTo(10));
            Assert.That(line.FromMatches, Is.False);
        }
    }

    [Test]
    public async Task GetAsync_FiltersBySeasonID()
    {
        var season1 = await SeedSeasonWithStoredStat(userID: 1, gamertag: "A");
        var season2 = Season.Seed(seasonName: "S2");
        Context.Seasons.Add(season2);
        SeedUser(2);
        await Context.SaveChangesAsync();
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "B", GamesPlayed = 5 }, seasonID: season2.SeasonID, userID: 2, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();

        var lines = await _service.GetAsync(seasonID: season1.SeasonID);

        Assert.That(lines.Single().Gamertag, Is.EqualTo("A"));
    }

    [Test]
    public async Task GetAsync_FiltersByUserID()
    {
        var season = Season.Seed(seasonName: "S1");
        Context.Seasons.Add(season);
        SeedUser(1);
        SeedUser(2);
        await Context.SaveChangesAsync();
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "A", GamesPlayed = 5 }, seasonID: season.SeasonID, userID: 1, gameVersionID: HaloInfiniteGameVersionID));
        Context.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "B", GamesPlayed = 5 }, seasonID: season.SeasonID, userID: 2, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();

        var lines = await _service.GetAsync(userID: 2);

        Assert.That(lines.Single().Gamertag, Is.EqualTo("B"));
    }
}
