using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        _service = new SeasonMatchService(_context, _dataPullService, _bracketService, new GrifballWebApp.Server.Championships.ChampionshipService(_context));
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(nonExistentSeasonMatchId, CancellationToken.None));
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
    }

    [Test]
    public async Task HomeForfeit_DisposesTransaction_When_Throws()
    {
        // Act & Assert
        const int nonExistentSeasonMatchId = 999;
        await Assert.ThrowsAsync<Exception>(() => _service.HomeForfeit(nonExistentSeasonMatchId, CancellationToken.None));

        Assert.That(_context.Database.CurrentTransaction, Is.Null);
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(nonExistentSeasonMatchId, CancellationToken.None));
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(seasonMatch.SeasonMatchID, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
    }

    [Test]
    public async Task AwayForfeit_DisposesTransaction_When_Throws()
    {
        // Act & Assert
        const int nonExistentSeasonMatchId = 999;
        await Assert.ThrowsAsync<Exception>(() => _service.AwayForfeit(nonExistentSeasonMatchId, CancellationToken.None));

        Assert.That(_context.Database.CurrentTransaction, Is.Null);
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
    public async Task GetSeasonMatchPage_Should_IncludeReportedGames_When_SeriesGamesExist()
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

        var matchLink1 = new SeriesGame
        {
            SeasonMatchID = seasonMatch.SeasonMatchID,
            MatchID = matchId1,
            GameNumber = 1
        };

        var matchLink2 = new SeriesGame
        {
            SeasonMatchID = seasonMatch.SeasonMatchID,
            MatchID = matchId2,
            GameNumber = 2
        };

        await _context.SeriesGames.AddRangeAsync(matchLink1, matchLink2);
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
    public async Task GetSeasonMatchPage_Should_IncludePlayerStatsAndSeriesTotals_ForWaypointGames()
    {
        var season = new Season { SeasonName = "Stats Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var userHome = new User { UserName = "Chief", NormalizedUserName = "CHIEF" };
        var userAway = new User { UserName = "Arbiter", NormalizedUserName = "ARBITER" };
        await _context.Users.AddRangeAsync(userHome, userAway);
        await _context.SaveChangesAsync();

        var xboxHome = new XboxUser { XboxUserID = 1111, Gamertag = "MasterChief", User = userHome };
        var xboxAway = new XboxUser { XboxUserID = 2222, Gamertag = "TheArbiter", User = userAway };
        await _context.XboxUsers.AddRangeAsync(xboxHome, xboxAway);
        await _context.SaveChangesAsync();

        var teamHome = new Team { SeasonID = season.SeasonID, TeamName = "Red Team" };
        var teamAway = new Team { SeasonID = season.SeasonID, TeamName = "Blue Team" };
        await _context.Teams.AddRangeAsync(teamHome, teamAway);
        await _context.SaveChangesAsync();

        await _context.TeamPlayers.AddRangeAsync(
            new TeamPlayer { TeamID = teamHome.TeamID, UserID = userHome.Id },
            new TeamPlayer { TeamID = teamAway.TeamID, UserID = userAway.Id }
        );
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = teamHome.TeamID,
            AwayTeamID = teamAway.TeamID,
            BestOf = 3
        };
        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        var matchId = Guid.NewGuid();
        var match = new Database.Models.Match { MatchID = matchId, StartTime = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(8) };
        var homeMatchTeam = new MatchTeam { MatchID = matchId, TeamID = 1, Score = 5, Outcome = Outcomes.Won };
        var awayMatchTeam = new MatchTeam { MatchID = matchId, TeamID = 2, Score = 2, Outcome = Outcomes.Lost };

        var homeParticipant = new MatchParticipant
        {
            MatchID = matchId,
            TeamID = 1,
            XboxUserID = xboxHome.XboxUserID,
            Kills = 25,
            Deaths = 10,
            Assists = 5,
            Score = 3,
            PersonalScore = 300,
            ObjectivesCompleted = 3
        };
        var awayParticipant = new MatchParticipant
        {
            MatchID = matchId,
            TeamID = 2,
            XboxUserID = xboxAway.XboxUserID,
            Kills = 15,
            Deaths = 20,
            Assists = 2,
            Score = 2,
            PersonalScore = 200,
            ObjectivesCompleted = 2
        };
        homeMatchTeam.MatchParticipants.Add(homeParticipant);
        awayMatchTeam.MatchParticipants.Add(awayParticipant);
        match.MatchTeams.Add(homeMatchTeam);
        match.MatchTeams.Add(awayMatchTeam);
        await _context.Matches.AddAsync(match);

        var seriesGame = new SeriesGame
        {
            SeasonMatchID = seasonMatch.SeasonMatchID,
            MatchID = matchId,
            GameNumber = 1
        };
        await _context.SeriesGames.AddAsync(seriesGame);
        await _context.SaveChangesAsync();

        var result = await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ReportedGames, Has.Length.EqualTo(1));
        var game = result.ReportedGames[0];
        Assert.Multiple(() =>
        {
            Assert.That(game.HomeScore, Is.EqualTo(5));
            Assert.That(game.AwayScore, Is.EqualTo(2));
            Assert.That(game.HomePlayers, Has.Count.EqualTo(1));
            Assert.That(game.AwayPlayers, Has.Count.EqualTo(1));
            Assert.That(game.HomePlayers[0].Gamertag, Is.EqualTo("MasterChief"));
            Assert.That(game.HomePlayers[0].Kills, Is.EqualTo(25));
            Assert.That(game.HomePlayers[0].BombScores, Is.EqualTo(3));
            Assert.That(result.SeriesTotals, Is.Not.Null);
            Assert.That(result.SeriesTotals!.HomePlayers[0].Kills, Is.EqualTo(25));
            Assert.That(result.SeriesTotals.HomePlayers[0].BombScores, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_SanitizeLargeScores_ForInfiniteCustomGametype()
    {
        var season = new Season { SeasonName = "Custom Mode Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user = new User { UserName = "Spartan", NormalizedUserName = "SPARTAN" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var xbox = new XboxUser { XboxUserID = 9999, Gamertag = "SpartanForge", User = user };
        await _context.XboxUsers.AddAsync(xbox);
        await _context.SaveChangesAsync();

        var team = new Team { SeasonID = season.SeasonID, TeamName = "Alpha Team" };
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

        await _context.TeamPlayers.AddAsync(new TeamPlayer { TeamID = team.TeamID, UserID = user.Id });
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = team.TeamID, BestOf = 1 };
        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        var matchId = Guid.NewGuid();
        var match = new Database.Models.Match { MatchID = matchId, StartTime = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(8) };
        var matchTeam1 = new MatchTeam { MatchID = matchId, TeamID = 1, Score = 5, Outcome = Outcomes.Won };
        var matchTeam2 = new MatchTeam { MatchID = matchId, TeamID = 2, Score = 0, Outcome = Outcomes.Lost };

        var participant = new MatchParticipant
        {
            MatchID = matchId,
            TeamID = 1,
            XboxUserID = xbox.XboxUserID,
            Kills = 10,
            Deaths = 5,
            Score = 504_010_048,
            PersonalScore = 504_010_048,
            ObjectivesCompleted = 4
        };
        matchTeam1.MatchParticipants.Add(participant);
        match.MatchTeams.Add(matchTeam1);
        match.MatchTeams.Add(matchTeam2);
        await _context.Matches.AddAsync(match);

        await _context.SeriesGames.AddAsync(new SeriesGame { SeasonMatchID = seasonMatch.SeasonMatchID, MatchID = matchId, GameNumber = 1 });
        await _context.SaveChangesAsync();

        var result = await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        var p = result!.ReportedGames[0].HomePlayers[0];
        Assert.Multiple(() =>
        {
            Assert.That(p.BombScores, Is.EqualTo(4), "Should fall back to ObjectivesCompleted rather than 504M score");
            Assert.That(p.Score, Is.Null, "Packed integer score should be omitted");
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_IncludePlayerStats_ForRecordedGames()
    {
        var season = new Season { SeasonName = "MCC Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var teamHome = new Team { SeasonID = season.SeasonID, TeamName = "Red Old" };
        var teamAway = new Team { SeasonID = season.SeasonID, TeamName = "Blue Old" };
        await _context.Teams.AddRangeAsync(teamHome, teamAway);
        await _context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = teamHome.TeamID, AwayTeamID = teamAway.TeamID, BestOf = 1 };
        await _context.SeasonMatches.AddAsync(seasonMatch);
        await _context.SaveChangesAsync();

        var user = new User { UserName = "H3Legend", NormalizedUserName = "H3LEGEND" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var recordedGame = new RecordedGame
        {
            GameVersionID = 1,
            SeasonID = season.SeasonID,
            SourceKey = "mcc-carnage-123"
        };
        await _context.RecordedGames.AddAsync(recordedGame);
        await _context.SaveChangesAsync();

        var recordedTeamHome = new RecordedGameTeam
        {
            RecordedGameID = recordedGame.RecordedGameID,
            Side = 1,
            TeamID = teamHome.TeamID,
            Score = 5,
            Outcome = Outcomes.Won
        };
        var recordedTeamAway = new RecordedGameTeam
        {
            RecordedGameID = recordedGame.RecordedGameID,
            Side = 2,
            TeamID = teamAway.TeamID,
            Score = 3,
            Outcome = Outcomes.Lost
        };
        await _context.RecordedGameTeams.AddRangeAsync(recordedTeamHome, recordedTeamAway);
        await _context.SaveChangesAsync();

        var pHome = new RecordedGameParticipant
        {
            RecordedGameID = recordedGame.RecordedGameID,
            RecordedGameTeamID = recordedTeamHome.RecordedGameTeamID,
            UserID = user.Id,
            Gamertag = "H3Legend",
            Kills = 40,
            Deaths = 12,
            Assists = 6,
            Goals = 5,
            Score = 50
        };
        await _context.RecordedGameParticipants.AddAsync(pHome);

        await _context.SeriesGames.AddAsync(new SeriesGame
        {
            SeasonMatchID = seasonMatch.SeasonMatchID,
            RecordedGameID = recordedGame.RecordedGameID,
            GameNumber = 1
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ReportedGames, Has.Length.EqualTo(1));
        var game = result.ReportedGames[0];
        Assert.Multiple(() =>
        {
            Assert.That(game.RecordedGameID, Is.EqualTo(recordedGame.RecordedGameID));
            Assert.That(game.HomeScore, Is.EqualTo(5));
            Assert.That(game.HomePlayers, Has.Count.EqualTo(1));
            Assert.That(game.HomePlayers[0].Gamertag, Is.EqualTo("H3Legend"));
            Assert.That(game.HomePlayers[0].BombScores, Is.EqualTo(5));
            Assert.That(game.HomePlayers[0].Kills, Is.EqualTo(40));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_OrderRecordedGamePlayers_ByBombScoresThenKillsThenDeaths()
    {
        var season = new Season { SeasonName = "MCC Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();
        var teamHome = new Team { SeasonID = season.SeasonID, TeamName = "Red Old" };
        var teamAway = new Team { SeasonID = season.SeasonID, TeamName = "Blue Old" };
        await _context.Teams.AddRangeAsync(teamHome, teamAway);
        await _context.SaveChangesAsync();
        var seasonMatch = new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = teamHome.TeamID, AwayTeamID = teamAway.TeamID, BestOf = 1 };
        var recordedGame = new RecordedGame { GameVersionID = 1, SeasonID = season.SeasonID, SourceKey = "mcc-carnage-order" };
        var user = new User { UserName = "OrderPlayer", NormalizedUserName = "ORDERPLAYER" };
        await _context.AddRangeAsync(seasonMatch, recordedGame, user);
        await _context.SaveChangesAsync();
        var home = new RecordedGameTeam { RecordedGameID = recordedGame.RecordedGameID, Side = 1, TeamID = teamHome.TeamID, Score = 5, Outcome = Outcomes.Won };
        var away = new RecordedGameTeam { RecordedGameID = recordedGame.RecordedGameID, Side = 2, TeamID = teamAway.TeamID, Score = 4, Outcome = Outcomes.Lost };
        await _context.RecordedGameTeams.AddRangeAsync(home, away);
        await _context.SaveChangesAsync();
        RecordedGameParticipant Player(RecordedGameTeam team, string gamertag, int goals, int kills, int deaths) => new()
        {
            RecordedGameID = recordedGame.RecordedGameID, RecordedGameTeamID = team.RecordedGameTeamID, UserID = user.Id, Gamertag = gamertag,
            Goals = goals, Kills = kills, Deaths = deaths,
        };
        await _context.RecordedGameParticipants.AddRangeAsync(
            Player(home, "FewKills", 1, 5, 9), Player(home, "MostBombs", 3, 2, 9), Player(home, "MoreDeaths", 1, 8, 9), Player(home, "FewerDeaths", 1, 8, 4),
            Player(away, "AwayKills", 0, 12, 3), Player(away, "AwayBomb", 2, 1, 3));
        await _context.SeriesGames.AddAsync(new SeriesGame { SeasonMatchID = seasonMatch.SeasonMatchID, RecordedGameID = recordedGame.RecordedGameID, GameNumber = 1 });
        await _context.SaveChangesAsync();

        var game = (await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None))!.ReportedGames.Single();

        Assert.Multiple(() =>
        {
            Assert.That(game.HomePlayers.Select(p => p.Gamertag), Is.EqualTo(new[] { "MostBombs", "FewerDeaths", "MoreDeaths", "FewKills" }));
            Assert.That(game.AwayPlayers.Select(p => p.Gamertag), Is.EqualTo(new[] { "AwayBomb", "AwayKills" }));
            Assert.That((game.HomeScore, game.AwayScore), Is.EqualTo(((int?)5, (int?)4)));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_Should_LeaveASideEmpty_When_TheRecordedGameHasNoTeamForIt()
    {
        var season = new Season { SeasonName = "MCC Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();
        // Teams not decided yet: home and away come from the recorded game's sides
        var seasonMatch = new SeasonMatch { SeasonID = season.SeasonID, BestOf = 1 };
        var recordedGame = new RecordedGame { GameVersionID = 1, SeasonID = season.SeasonID, SourceKey = "mcc-carnage-one-side" };
        var user = new User { UserName = "Solo", NormalizedUserName = "SOLO" };
        await _context.AddRangeAsync(seasonMatch, recordedGame, user);
        await _context.SaveChangesAsync();
        var home = new RecordedGameTeam { RecordedGameID = recordedGame.RecordedGameID, Side = 1, Score = 5, Outcome = Outcomes.Won };
        await _context.RecordedGameTeams.AddAsync(home);
        await _context.SaveChangesAsync();
        await _context.RecordedGameParticipants.AddAsync(new RecordedGameParticipant
        {
            RecordedGameID = recordedGame.RecordedGameID, RecordedGameTeamID = home.RecordedGameTeamID, UserID = user.Id, Gamertag = "Solo", Goals = 2, Kills = 7, Deaths = 1,
        });
        await _context.SeriesGames.AddAsync(new SeriesGame { SeasonMatchID = seasonMatch.SeasonMatchID, RecordedGameID = recordedGame.RecordedGameID, GameNumber = 1 });
        await _context.SaveChangesAsync();

        var game = (await _service.GetSeasonMatchPage(seasonMatch.SeasonMatchID, CancellationToken.None))!.ReportedGames.Single();

        Assert.Multiple(() =>
        {
            Assert.That(game.HomeScore, Is.EqualTo(5));
            Assert.That(game.HomePlayers.Select(p => p.Gamertag), Is.EqualTo(new[] { "Solo" }));
            Assert.That(game.AwayScore, Is.Null);
            Assert.That(game.AwayPlayers, Is.Empty);
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
