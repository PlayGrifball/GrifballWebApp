using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// SeasonStatsService.GetAsync's games path (FromMatchesAsync over PlayerGameStatsQuery.Query), which
/// SeasonStatsServiceUnitTests leaves out: a recorded game of a season outside any series, totalled per player, phase and
/// team, and the stored rows its games cover (the same phase, or a combined row) left out. A season match's Waypoint games
/// need a whole Halo match seeded and are GradesServiceUnitTests' and Integration.Test's.
/// </summary>
[TestFixture]
internal sealed class SeasonStatsServiceMoreUnitTests : ApplicationDbContextTests
{
    private const int Halo3GameVersionID = 1; // GameVersionConfiguration seeds this through HasData
    private SeasonStatsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new SeasonStatsService(Context, TimeProvider.System);
    }

    /// <summary>Season "S1" with a recorded regular-season game: user 1 (team Hammers) won it, user 2 (no team) lost it.</summary>
    private async Task<(Season Season, Team Team)> SeedRecordedGame()
    {
        var season = Season.Seed(seasonName: "S1");
        Context.Seasons.Add(season);
        Context.Users.AddRange(User.Seed(userName: "one", id: 1), User.Seed(userName: "two", id: 2));
        await Context.SaveChangesAsync();
        var team = Team.Seed(teamName: "Hammers", seasonID: season.SeasonID);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        var game = RecordedGame.Seed("s1/game-1", gameVersionID: Halo3GameVersionID, seasonID: season.SeasonID);
        var home = game.SeedSide(0, teamID: team.TeamID, outcome: Outcomes.Won);
        var away = game.SeedSide(1, outcome: Outcomes.Lost);
        game.SeedPlayer(new RecordedPlayerLine(1, 0, "One", 12, 3, 1) { Goals = 4, HammerKills = 5 }, home);
        game.SeedPlayer(new RecordedPlayerLine(2, 1, "Two", 3, 12, 0) { Goals = 1, HammerKills = 1 }, away);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        return (season, team);
    }

    [Test]
    public async Task GetAsync_ARecordedGameOfTheSeason_TotalsItPerPlayerAndTeam()
    {
        var (season, team) = await SeedRecordedGame();

        var lines = await _service.GetAsync();

        Assert.That(lines, Has.Count.EqualTo(2));
        var one = lines[0];
        var two = lines[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((one.UserID, one.Gamertag, one.FromMatches, one.IsPlayoffs), Is.EqualTo((1, "One", true, false)), "most goals first");
            Assert.That((one.SeasonID, one.Season.SeasonName), Is.EqualTo((season.SeasonID, "S1")), "the season loaded over the stub");
            Assert.That((one.TeamID, one.TeamName, one.StintIndex), Is.EqualTo(((int?)team.TeamID, "Hammers", 1)));
            Assert.That((one.GameVersionID, one.GameVersionName), Is.EqualTo(((int?)Halo3GameVersionID, "Halo 3")));
            Assert.That((one.GamesPlayed, one.Wins, one.Losses, one.Kills, one.Deaths, one.Goals), Is.EqualTo((1, 1, 0, 12, 3, 4L)));
            Assert.That(one.HammerKills, Is.EqualTo(5));
            Assert.That(one.SwordKills, Is.Null, "no game records it");
            Assert.That((two.UserID, two.TeamID, two.TeamName), Is.EqualTo((2, (int?)null, (string?)null)));
            Assert.That((two.Wins, two.Losses, two.Goals), Is.EqualTo((0, 1, 1L)));
        }
    }

    [Test]
    public async Task GetAsync_ByUser_TotalsOnlyTheirGames()
    {
        await SeedRecordedGame();

        var lines = await _service.GetAsync(userID: 2);

        var line = lines.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((line.UserID, line.Gamertag, line.FromMatches), Is.EqualTo((2, "Two", true)));
            Assert.That(line.Kills, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task GetAsync_StoredRowsTheGamesCover_AreLeftOut()
    {
        var (season, _) = await SeedRecordedGame();
        Context.PlayerSeasonStats.AddRange(
            // The same phase as user 1's games: theirs stand
            PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "One (sheet)", GamesPlayed = 9 }, seasonID: season.SeasonID, userID: 1, gameVersionID: Halo3GameVersionID),
            // Playoffs, which no game of user 1's is: kept
            PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "One (playoffs)", GamesPlayed = 2 }, seasonID: season.SeasonID, userID: 1, gameVersionID: Halo3GameVersionID, isPlayoffs: true),
            // Both phases together, and user 2 has regular-season games: left out
            PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Two (combined)", GamesPlayed = 7 }, seasonID: season.SeasonID, userID: 2, gameVersionID: Halo3GameVersionID,
                isPlayoffs: true, isCombined: true));
        await Context.SaveChangesAsync();

        var lines = await _service.GetAsync(seasonID: season.SeasonID);

        Assert.That(lines.Select(l => (l.UserID, l.IsPlayoffs, l.FromMatches, l.Gamertag)),
            Is.EqualTo(new[] { (1, false, true, "One"), (2, false, true, "Two"), (1, true, false, "One (playoffs)") }));
    }
}
