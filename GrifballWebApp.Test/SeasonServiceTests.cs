using Microsoft.Extensions.Time.Testing;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Seasons;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonServiceTests
{
    private GrifballContext _context;
    private SeasonService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SeasonService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnZero_When_NoCurrentSeason()
    {
        // Arrange - no seasons in database

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnSeasonID_When_CurrentSeasonExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var currentSeason = Season.Seed(seasonName: "Current Season").WithDates(seasonStart: now.AddDays(-10), seasonEnd: now.AddDays(10));

        await _context.Seasons.AddAsync(currentSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        Assert.That(result, Is.EqualTo(currentSeason.SeasonID));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnZero_When_SeasonsExistButNoneAreCurrent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastSeason = Season.Seed(seasonName: "Past Season").WithDates(seasonStart: now.AddDays(-30), seasonEnd: now.AddDays(-10));
        var futureSeason = Season.Seed(seasonName: "Future Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(30));

        await _context.Seasons.AddRangeAsync(pastSeason, futureSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnFirstMatch_When_MultipleSeasonsOverlap()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season1 = Season.Seed(seasonName: "Season 1").WithDates(seasonStart: now.AddDays(-20), seasonEnd: now.AddDays(5));
        var season2 = Season.Seed(seasonName: "Season 2").WithDates(seasonStart: now.AddDays(-5), seasonEnd: now.AddDays(20));

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        // Should return the first one found (FirstOrDefaultAsync behavior)
        Assert.That(result, Is.AnyOf(season1.SeasonID, season2.SeasonID));
        Assert.That(result, Is.Not.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_HandleExactBoundaryDates()
    {
        // Arrange: a season is running from its start, included, up to its end, not included (Season.RunningAt)
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new SeasonService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), new FakeTimeProvider(now));
        var exactStartSeason = Season.Seed(seasonName: "Exact Start Season").WithDates(seasonStart: now, seasonEnd: now.AddDays(10));
        var exactEndSeason = Season.Seed(seasonName: "Exact End Season").WithDates(seasonStart: now.AddDays(-10), seasonEnd: now);

        await _context.Seasons.AddRangeAsync(exactStartSeason, exactEndSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetCurrentSeasonID();

        // Assert: the one starting now, not the one ending now
        Assert.That(result, Is.EqualTo(exactStartSeason.SeasonID));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_RespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.CatchAsync<OperationCanceledException>(() => _service.GetCurrentSeasonID(cts.Token));
    }

    [Test]
    public async Task GetSeasonName_Should_ReturnNull_When_SeasonDoesNotExist()
    {
        // Arrange
        const int nonExistentSeasonId = 999;

        // Act
        var result = await _service.GetSeasonName(nonExistentSeasonId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSeasonName_Should_ReturnSeasonName_When_SeasonExists()
    {
        // Arrange
        const string expectedSeasonName = "Test Season";
        var season = Season.Seed(seasonName: expectedSeasonName).WithDates(seasonStart: DateTime.UtcNow.AddDays(-10), seasonEnd: DateTime.UtcNow.AddDays(10));

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonName(season.SeasonID);

        // Assert
        Assert.That(result, Is.EqualTo(expectedSeasonName));
    }

    [Test]
    public async Task GetSeasonName_Should_ReturnCorrectName_When_MultipleSeasons()
    {
        // Arrange
        var season1 = Season.Seed(seasonName: "Season One").WithDates(seasonStart: DateTime.UtcNow.AddDays(-20), seasonEnd: DateTime.UtcNow.AddDays(-10));
        var season2 = Season.Seed(seasonName: "Season Two").WithDates(seasonStart: DateTime.UtcNow.AddDays(-5), seasonEnd: DateTime.UtcNow.AddDays(5));

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        // Act & Assert
        Assert.Multiple(async () =>
        {
            var result1 = await _service.GetSeasonName(season1.SeasonID);
            var result2 = await _service.GetSeasonName(season2.SeasonID);
            
            Assert.That(result1, Is.EqualTo("Season One"));
            Assert.That(result2, Is.EqualTo("Season Two"));
        });
    }

    [Test]
    public async Task GetSeasonName_Should_HandleEmptySeasonName()
    {
        // Arrange
        var season = Season.Seed(seasonName: "").WithDates(seasonStart: DateTime.UtcNow.AddDays(-10), seasonEnd: DateTime.UtcNow.AddDays(10));

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonName(season.SeasonID);

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public async Task GetSeasonName_Should_RespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.CatchAsync<OperationCanceledException>(() => _service.GetSeasonName(1, cts.Token));
    }



    [Test]
    public async Task GetSeasonTeams_Should_ReturnNull_When_SeasonDoesNotExist()
    {
        var result = await _service.GetSeasonTeams(99999);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSeasonTeams_Should_ReturnEmptyList_When_SeasonHasNoTeams()
    {
        var season = Season.Seed(seasonName: "Empty Season").WithDates(seasonStart: DateTime.UtcNow.AddDays(-10), seasonEnd: DateTime.UtcNow.AddDays(10));
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var result = await _service.GetSeasonTeams(season.SeasonID);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSeasonTeams_Should_ReturnTeams_WithRosterAndTitles_OrderedByChampionshipAndRecord()
    {
        var season = Season.Seed(seasonName: "Championship Season").WithDates(seasonStart: DateTime.UtcNow.AddDays(-30), seasonEnd: DateTime.UtcNow.AddDays(-1));
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var player1 = new User { UserName = "Captain1", DisplayName = "Alpha Captain", IsDummyUser = false };
        var player2 = new User { UserName = "Player2", DisplayName = "Alpha Member", IsDummyUser = false };
        var player3 = new User { UserName = "historical:BetaCaptain", IsDummyUser = true };
        await _context.Users.AddRangeAsync(player1, player2, player3);
        await _context.SaveChangesAsync();

        var team1 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Alpha Team",
            RecordedWins = 8,
            RecordedLosses = 2,
        };
        var team2 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Beta Champions",
            RecordedWins = 7,
            RecordedLosses = 3,
        };
        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var tp1 = new TeamPlayer { TeamID = team1.TeamID, UserID = player1.Id, DraftCaptainOrder = 1 };
        var tp2 = new TeamPlayer { TeamID = team1.TeamID, UserID = player2.Id, DraftRound = 1, DraftPick = 2 };
        var tp3 = new TeamPlayer { TeamID = team2.TeamID, UserID = player3.Id, DraftCaptainOrder = 2 };
        await _context.TeamPlayers.AddRangeAsync(tp1, tp2, tp3);
        await _context.SaveChangesAsync();

        team1.CaptainID = tp1.TeamPlayerID;
        team2.CaptainID = tp3.TeamPlayerID;

        var title = PlayerChampionship.Seed(seasonID: season.SeasonID, teamID: team2.TeamID, userID: player3.Id, tier: "Pro", gamertagUsed: "BetaCaptain", isCaptain: true);
        await _context.PlayerChampionships.AddAsync(title);
        await _context.SaveChangesAsync();

        var result = await _service.GetSeasonTeams(season.SeasonID);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));

        // Team 2 won the Pro title, so it sorts first despite fewer wins
        Assert.That(result[0].TeamID, Is.EqualTo(team2.TeamID));
        Assert.That(result[0].Titles, Does.Contain("Pro"));
        Assert.That(result[0].RecordFromSource, Is.True);
        Assert.That(result[0].Wins, Is.EqualTo(7));
        Assert.That(result[0].Players[0].Gamertag, Is.EqualTo("BetaCaptain"));
        Assert.That(result[0].Players[0].IsCaptain, Is.True);

        // Team 1 is second
        Assert.That(result[1].TeamID, Is.EqualTo(team1.TeamID));
        Assert.That(result[1].Wins, Is.EqualTo(8));
        Assert.That(result[1].Losses, Is.EqualTo(2));
        Assert.That(result[1].Players.Count, Is.EqualTo(2));
        Assert.That(result[1].Players[0].Gamertag, Is.EqualTo("Alpha Captain"));
        Assert.That(result[1].Players[0].IsCaptain, Is.True);
        Assert.That(result[1].Players[1].Gamertag, Is.EqualTo("Alpha Member"));
        Assert.That(result[1].Players[1].IsCaptain, Is.False);
    }

    [Test]
    public async Task GetSeasonTeams_Should_CountRecords_AsTheStandingsDo()
    {
        var season = await _context.AddSeason("Counted Season");
        var a = await _context.AddTeam(season, "Alpha", await _context.AddUser("ca"), 1);
        var b = await _context.AddTeam(season, "Bravo", await _context.AddUser("cb"), 2);
        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(season.SeasonID, a.TeamID, b.TeamID, bestOf: 3).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(season.SeasonID, b.TeamID, a.TeamID, bestOf: 3).Forfeited(Forfeiter.Home),
            // A BYE, no team on its side: Alpha faces it, its win not given yet
            SeasonMatch.Seed(season.SeasonID, null, a.TeamID, bestOf: 3).WithBye(SeasonMatchSide.Home),
            SeasonMatch.Seed(season.SeasonID, b.TeamID, a.TeamID, bestOf: 3));
        await _context.SaveChangesAsync();

        var result = await _service.GetSeasonTeams(season.SeasonID);
        var standings = await new GrifballWebApp.Server.TeamStandings.TeamStandingsService(_context).GetTeamStandings(season.SeasonID);

        // A BYE is neither a win nor a loss; an undecided match is still to be played
        Assert.That(result!.Select(t => (t.TeamName, t.Wins, t.Losses, t.TBD, t.RecordFromSource)),
            Is.EqualTo(new[] { ("Alpha", 2, 0, 1, false), ("Bravo", 0, 2, 1, false) }));
        Assert.That(result.Select(t => (t.TeamName, t.Wins, t.Losses)),
            Is.EqualTo(standings.Select(s => (s.TeamName, s.Wins, s.Losses))));
    }
}
