using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileControllerTests
{
    private GrifballContext _context;
    private ISetGamertagService _setGamertagService;
    private ProfileController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _setGamertagService = Substitute.For<ISetGamertagService>();
        _controller = new ProfileController(_context, _setGamertagService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetGamertag_Should_ReturnNull_When_UserDoesNotExist()
    {
        // Arrange
        const int nonExistentUserId = 999;

        // Act
        var result = await _controller.GetGamertag(nonExistentUserId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetGamertag_Should_ReturnGamertag_When_UserHasXboxUser()
    {
        // Arrange
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var xboxUser = new XboxUser { Gamertag = "TestGamer123", XboxUserID = 12345 };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        user.XboxUserID = xboxUser.XboxUserID;
        await _context.XboxUsers.AddAsync(xboxUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetGamertag(user.Id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Gamertag, Is.EqualTo("TestGamer123"));
    }

    [Test]
    public async Task GetGamertag_Should_ReturnDifferentGamertags_When_MultipleUsersExist()
    {
        // Arrange
        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        var xboxUser1 = new XboxUser { Gamertag = "Gamer1", XboxUserID = 111 };
        var xboxUser2 = new XboxUser { Gamertag = "Gamer2", XboxUserID = 222 };

        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        user1.XboxUserID = xboxUser1.XboxUserID;
        user2.XboxUserID = xboxUser2.XboxUserID;
        await _context.XboxUsers.AddRangeAsync(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _controller.GetGamertag(user1.Id, CancellationToken.None);
        var result2 = await _controller.GetGamertag(user2.Id, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result1!.Gamertag, Is.EqualTo("Gamer1"));
            Assert.That(result2!.Gamertag, Is.EqualTo("Gamer2"));
        });
    }

    [Test]
    public async Task GetProfileStats_Should_ReturnNotFound_When_UserDoesNotExist()
    {
        var result = await _controller.GetProfileStats(999, CancellationToken.None);
        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetProfileStats_Should_ReturnFullProfileStats_WithChampionshipsAndCareerRollup()
    {
        var user = new User { UserName = "champion", DisplayName = "Spartan Legend" };
        var xbox = new XboxUser { Gamertag = "ChampTag", XboxUserID = 777 };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        user.XboxUserID = xbox.XboxUserID;
        await _context.XboxUsers.AddAsync(xbox);

        var league = new League { LeagueName = "AGLA", Slug = "agla" };
        var h3 = new GameVersion { GameVersionName = "Halo 3", Slug = "halo3" };
        await _context.AddRangeAsync(league, h3);
        await _context.SaveChangesAsync();

        var season = new Season
        {
            SeasonName = "Summer League 2018",
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            SeasonStart = new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2018, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            SignupsOpen = new DateTime(2018, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            SignupsClose = new DateTime(2018, 5, 25, 0, 0, 0, DateTimeKind.Utc),
            DraftStart = new DateTime(2018, 5, 26, 0, 0, 0, DateTimeKind.Utc),
            IsHistorical = true,
            HistoricalSourceID = "agla/summer-league-2018"
        };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var title = new PlayerChampionship
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            Tier = "Pro",
            TeamName = "Dynasty",
            IsCaptain = true,
            GamertagUsed = "ChampTag"
        };

        var stat = new PlayerSeasonStat
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            GameVersionID = h3.GameVesionID,
            Gamertag = "ChampTag",
            TeamName = "Dynasty",
            StintIndex = 1,
            IsPlayoffs = false,
            GamesPlayed = 10,
            Wins = 8,
            Losses = 2,
            Goals = 15,
            Kills = 120,
            Deaths = 60,
            Assists = 25,
            CarrierKills = 30,
            Sprees = 5,
            MultiKills = 4,
            Exterminations = 1
        };

        var alias = new PlayerAlias
        {
            UserID = user.Id,
            Alias = "ChampTagOld",
            NormalizedAlias = "CHAMPTAGOLD"
        };

        await _context.AddRangeAsync(title, stat, alias);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.GetProfileStats(user.Id, CancellationToken.None);
        var stats = actionResult.Value;

        Assert.That(stats, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stats!.UserID, Is.EqualTo(user.Id));
            Assert.That(stats.Gamertag, Is.EqualTo("ChampTag"));
            Assert.That(stats.DisplayName, Is.EqualTo("Spartan Legend"));
            Assert.That(stats.Aliases, Contains.Item("ChampTagOld"));

            Assert.That(stats.Championships, Has.Count.EqualTo(1));
            Assert.That(stats.Championships[0].Tier, Is.EqualTo("Pro"));
            Assert.That(stats.Championships[0].TeamName, Is.EqualTo("Dynasty"));
            Assert.That(stats.Championships[0].IsCaptain, Is.True);
            Assert.That(stats.Championships[0].Year, Is.EqualTo(2018));

            Assert.That(stats.OverallCareer, Is.Not.Null);
            Assert.That(stats.OverallCareer!.GamesPlayed, Is.EqualTo(10));
            Assert.That(stats.OverallCareer.Wins, Is.EqualTo(8));
            Assert.That(stats.OverallCareer.Losses, Is.EqualTo(2));
            Assert.That(stats.OverallCareer.WinPercent, Is.EqualTo(80.0m));
            Assert.That(stats.OverallCareer.Goals, Is.EqualTo(15));
            Assert.That(stats.OverallCareer.Kills, Is.EqualTo(120));
            Assert.That(stats.OverallCareer.Deaths, Is.EqualTo(60));
            Assert.That(stats.OverallCareer.KdRatio, Is.EqualTo(2.0m));
            Assert.That(stats.OverallCareer.KdSpread, Is.EqualTo(60));
            Assert.That(stats.OverallCareer.CarrierKills, Is.EqualTo(30));

            Assert.That(stats.CareerByGame, Has.Count.EqualTo(1));
            Assert.That(stats.CareerByGame[0].GameVersionSlug, Is.EqualTo("halo3"));

            Assert.That(stats.Seasons, Has.Count.EqualTo(1));
            Assert.That(stats.Seasons[0].SeasonName, Is.EqualTo("Summer League 2018"));
        });
    }
}
