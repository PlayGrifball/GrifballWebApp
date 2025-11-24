using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchServiceTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private IBracketService _bracketService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _bracketService = Substitute.For<IBracketService>();
        _service = new SeasonMatchService(_context, _dataPullService, _bracketService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_ReturnNull_When_SeasonMatchDoesNotExist()
    {
        // Arrange
        const int nonExistentSeasonMatchId = 999;

        // Act
        var result = await _service.GetSeasonMatchPage(nonExistentSeasonMatchId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_ReturnSeasonMatchPage_When_SeasonMatchExists()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3,
            ScheduledTime = DateTime.UtcNow.AddDays(1)
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(result.SeasonName, Is.EqualTo("Test Season"));
            Assert.That(result.HomeTeamName, Is.EqualTo("Team 1"));
            Assert.That(result.AwayTeamName, Is.EqualTo("Team 2"));
            Assert.That(result.BestOf, Is.EqualTo(3));
            Assert.That(result.IsPlayoff, Is.False);
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_MapResults_When_ResultsAreSet()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3,
            HomeTeamResult = SeasonMatchResult.Won,
            AwayTeamResult = SeasonMatchResult.Loss,
            HomeTeamScore = 2,
            AwayTeamScore = 1
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.HomeTeamResult, Is.EqualTo(SeasonMatchResultDto.Won));
            Assert.That(result.AwayTeamResult, Is.EqualTo(SeasonMatchResultDto.Loss));
            Assert.That(result.HomeTeamScore, Is.EqualTo(2));
            Assert.That(result.AwayTeamScore, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetPossibleMatches_Should_ReturnEmptyList_When_HomeTeamHasNoPlayers()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPossibleMatches(seasonMatch.SeasonMatchID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task HomeForfeit_Should_ThrowException_When_SeasonMatchDoesNotExist()
    {
        // Arrange
        const int nonExistentSeasonMatchId = 999;

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(nonExistentSeasonMatchId, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task HomeForfeit_Should_ThrowException_When_HomeTeamIsNull()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = null,
            AwayTeamID = null,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("does not have both teams assigned"));
    }

    [Test]
    public async Task HomeForfeit_Should_ThrowException_When_ResultsAlreadyDecided()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3,
            HomeTeamResult = SeasonMatchResult.Won,
            AwayTeamResult = SeasonMatchResult.Loss
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
    }

    [Test]
    public async Task HomeForfeit_Should_SetCorrectResults_When_ValidSeasonMatch()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act
        await _service.HomeForfeit(seasonMatch.SeasonMatchID, CancellationToken.None);

        // Assert
        var updatedMatch = await _context.SeasonMatches.FindAsync(seasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(updatedMatch!.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(updatedMatch.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Won));
        });
    }

    [Test]
    public async Task AwayForfeit_Should_ThrowException_When_SeasonMatchDoesNotExist()
    {
        // Arrange
        const int nonExistentSeasonMatchId = 999;

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(nonExistentSeasonMatchId, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task AwayForfeit_Should_ThrowException_When_AwayTeamIsNull()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = null,
            AwayTeamID = null,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("does not have both teams assigned"));
    }

    [Test]
    public async Task AwayForfeit_Should_ThrowException_When_ResultsAlreadyDecided()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3,
            HomeTeamResult = SeasonMatchResult.Won,
            AwayTeamResult = SeasonMatchResult.Loss
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
    }

    [Test]
    public async Task AwayForfeit_Should_SetCorrectResults_When_ValidSeasonMatch()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        // Act
        await _service.AwayForfeit(seasonMatch.SeasonMatchID, CancellationToken.None);

        // Assert
        var updatedMatch = await _context.SeasonMatches.FindAsync(seasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(updatedMatch!.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
            Assert.That(updatedMatch.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_IncludeReportedGames_When_MatchLinksExist()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        var matchId1 = Guid.NewGuid();
        var matchId2 = Guid.NewGuid();

        // Create Match entities first
        var match1 = new Database.Models.Match
        {
            MatchID = matchId1,
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(10)
        };

        var match2 = new Database.Models.Match
        {
            MatchID = matchId2,
            StartTime = DateTime.UtcNow.AddMinutes(15),
            Duration = TimeSpan.FromMinutes(10)
        };

        await _context.Matches.AddRangeAsync(match1, match2);
        await _context.SaveChangesAsync();

        var matchLink1 = new MatchLink
        {
            SeasonMatchID = seasonMatch.SeasonMatchID,
            MatchID = matchId1,
            MatchNumber = 1
        };

        var matchLink2 = new MatchLink
        {
            SeasonMatchID = seasonMatch.SeasonMatchID,
            MatchID = matchId2,
            MatchNumber = 2
        };

        await _context.MatchLinks.AddRangeAsync(matchLink1, matchLink2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ReportedGames, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.ReportedGames.Any(g => g.MatchID == matchId1 && g.MatchNumber == 1), Is.True);
            Assert.That(result.ReportedGames.Any(g => g.MatchID == matchId2 && g.MatchNumber == 2), Is.True);
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_MapAllResultTypes_Correctly()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Test TBD result
        var matchTBD = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3,
            HomeTeamResult = null,
            AwayTeamResult = null
        };

        await _context.SeasonMatches.AddAsync(matchTBD);
        await _context.SaveChangesAsync();

        var resultTBD = await _service.GetSeasonMatchPage(matchTBD.SeasonMatchID, CancellationToken.None);

        // Test Forfeit result
        var matchForfeit = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3,
            HomeTeamResult = SeasonMatchResult.Forfeit,
            AwayTeamResult = SeasonMatchResult.Won
        };

        await _context.SeasonMatches.AddAsync(matchForfeit);
        await _context.SaveChangesAsync();

        var resultForfeit = await _service.GetSeasonMatchPage(matchForfeit.SeasonMatchID, CancellationToken.None);

        // Test Bye result
        var matchBye = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = null,
            BestOf = 3,
            HomeTeamResult = SeasonMatchResult.Bye,
            AwayTeamResult = null
        };

        await _context.SeasonMatches.AddAsync(matchBye);
        await _context.SaveChangesAsync();

        var resultBye = await _service.GetSeasonMatchPage(matchBye.SeasonMatchID, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(resultTBD!.HomeTeamResult, Is.EqualTo(SeasonMatchResultDto.TBD));
            Assert.That(resultTBD.AwayTeamResult, Is.EqualTo(SeasonMatchResultDto.TBD));
            Assert.That(resultForfeit!.HomeTeamResult, Is.EqualTo(SeasonMatchResultDto.Forfeit));
            Assert.That(resultForfeit.AwayTeamResult, Is.EqualTo(SeasonMatchResultDto.Won));
            Assert.That(resultBye!.HomeTeamResult, Is.EqualTo(SeasonMatchResultDto.Bye));
        });
    }
}
