using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
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

        // Both are seeded by the migrations
        var league = await _context.Leagues.SingleAsync(l => l.Slug == "agla");
        var h3 = await _context.GameVersions.SingleAsync(g => g.Slug == "halo3");

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
            HistoricalSourceID = "2018-sml18-stats/Stats",
            HistoricalSourceUrl = "https://docs.google.com/spreadsheets/d/x"
        };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();
        var dynasty = new Team { SeasonID = season.SeasonID, TeamName = "Dynasty" };
        await _context.Teams.AddAsync(dynasty);
        await _context.SaveChangesAsync();

        var title = new PlayerChampionship
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            Tier = "Pro",
            TeamID = dynasty.TeamID,
            IsCaptain = true,
            GamertagUsed = "ChampTag"
        };

        var stat = new PlayerSeasonStat
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            GameVersionID = h3.GameVesionID,
            Gamertag = "ChampTag",
            TeamID = dynasty.TeamID,
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

        var renamed = new XboxGamertagHistory { XboxUserID = xbox.XboxUserID, Gamertag = "ChampTagOld", NormalizedGamertag = "CHAMPTAGOLD", Source = GamertagHistorySource.RenameDetected };

        await _context.AddRangeAsync(title, stat, renamed);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.GetProfileStats(user.Id, CancellationToken.None);
        var stats = actionResult.Value;

        Assert.That(stats, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stats!.UserID, Is.EqualTo(user.Id));
            Assert.That(stats.Gamertag, Is.EqualTo("ChampTag"));
            Assert.That(stats.DisplayName, Is.EqualTo("Spartan Legend"));
            Assert.That(stats.PastGamertags, Is.EqualTo(new[] { "ChampTagOld" }));

            Assert.That(stats.Championships, Has.Count.EqualTo(1));
            Assert.That(stats.Championships[0].Tier, Is.EqualTo("Pro"));
            Assert.That(stats.Championships[0].TeamName, Is.EqualTo("Dynasty"));
            Assert.That(stats.Championships[0].TeamID, Is.EqualTo(dynasty.TeamID));
            Assert.That(stats.Championships[0].IsHistorical, Is.True);
            Assert.That(stats.Championships[0].IsCaptain, Is.True);
            Assert.That(stats.Championships[0].Year, Is.EqualTo(2018));
            Assert.That(stats.Championships[0].SeasonSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/x"));
            Assert.That(stats.Seasons[0].SeasonSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/x"));
            Assert.That((stats.Seasons[0].TeamID, stats.Seasons[0].TeamName), Is.EqualTo(((int?)dynasty.TeamID, "Dynasty")));
            Assert.That(stats.Seasons[0].IsHistorical, Is.True);

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

    [Test]
    public async Task GetProfileStats_GoalsAndStopsPerGame_CountOnlyTheSheetsGames()
    {
        var user = new User { UserName = "infinite" };
        await _context.Users.AddAsync(user);
        var season = new Season
        {
            SeasonName = "Winter League 2023 (DA)",
            GameVersionID = 6,
            IsHistorical = true,
            HistoricalSourceID = "2023-da-wl23-stats/Halo Infinite Stats",
            SeasonStart = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
        var other = new Season
        {
            SeasonName = "Spring League 2024 (DA)",
            GameVersionID = 6,
            IsHistorical = true,
            HistoricalSourceID = "2024-da-sl24-stats/Aggregate",
            SeasonStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
        await _context.Seasons.AddRangeAsync(season, other);
        await _context.SaveChangesAsync();
        await _context.PlayerSeasonStats.AddRangeAsync(
            new PlayerSeasonStat { UserID = user.Id, SeasonID = season.SeasonID, GameVersionID = 6, Gamertag = "x", GamesPlayed = 10, Goals = 20, CarrierKills = 5, Source = StatSource.Manual },
            new PlayerSeasonStat { UserID = user.Id, SeasonID = other.SeasonID, GameVersionID = 6, Gamertag = "x", GamesPlayed = 30, Source = StatSource.Waypoint });
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(stats.OverallCareer!.GamesPlayed, Is.EqualTo(40));
            Assert.That(stats.OverallCareer.GoalsPerGame, Is.EqualTo(2.0m), "20 goals over the sheet's 10 games, not 40");
            Assert.That(stats.OverallCareer.StopsPerGame, Is.EqualTo(0.5m));
            Assert.That(stats.CareerByGame.Single().GoalsPerGame, Is.EqualTo(2.0m));
            Assert.That(stats.PastGamertags, Is.Empty);
        });
    }

    [Test]
    public async Task GetProfileStats_OnlyWaypointRows_HasNoGoalsOrStopsPerGame()
    {
        var user = new User { UserName = "waypoint" };
        await _context.Users.AddAsync(user);
        var season = new Season { SeasonName = "S 2024", GameVersionID = 6, IsHistorical = true, HistoricalSourceID = "w/s" };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();
        await _context.PlayerSeasonStats.AddAsync(new PlayerSeasonStat { UserID = user.Id, SeasonID = season.SeasonID, GameVersionID = 6, Gamertag = "x", GamesPlayed = 3, Source = StatSource.Waypoint });
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        Assert.That((stats.OverallCareer!.GoalsPerGame, stats.OverallCareer.StopsPerGame), Is.EqualTo(((decimal?)null, (decimal?)null)));
    }

    [Test]
    public async Task GetProfileStats_PastGamertags_LeaveOutTheCurrentOne_LatestFirst()
    {
        var user = new User { UserName = "renamer", XboxUser = new XboxUser { XboxUserID = 4242, Gamertag = "Alpha" } };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        DateTime At(int year) => new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _context.XboxGamertagHistory.AddRangeAsync(
            // Alpha -> Bravo -> Alpha: Alpha is in the history too
            new XboxGamertagHistory { XboxUserID = 4242, Gamertag = "Alpha", NormalizedGamertag = "ALPHA", LastSeen = At(2020), Source = GamertagHistorySource.RenameDetected },
            new XboxGamertagHistory { XboxUserID = 4242, Gamertag = "Bravo", NormalizedGamertag = "BRAVO", LastSeen = At(2022), Source = GamertagHistorySource.RenameDetected },
            new XboxGamertagHistory { XboxUserID = 4242, Gamertag = "Undated B", NormalizedGamertag = "UNDATED B", FirstSeen = At(2019), Source = GamertagHistorySource.Manual },
            new XboxGamertagHistory { XboxUserID = 4242, Gamertag = "Undated A", NormalizedGamertag = "UNDATED A", FirstSeen = At(2018), Source = GamertagHistorySource.Manual },
            new XboxGamertagHistory { XboxUserID = 4242, Gamertag = "Charlie", NormalizedGamertag = "CHARLIE", LastSeen = At(2016), Source = GamertagHistorySource.Manual });
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        Assert.That(stats.PastGamertags, Is.EqualTo(new[] { "Bravo", "Charlie", "Undated A", "Undated B" }));
    }

    [Test]
    public async Task GetProfileStats_ARowWithoutATeam_HasNone()
    {
        var user = new User { UserName = "teamless" };
        await _context.Users.AddAsync(user);
        var season = new Season
        {
            SeasonName = "Summer League 2021 (Pro)",
            GameVersionID = 1,
            IsHistorical = true,
            HistoricalSourceID = "sml21/stats",
            SeasonStart = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2021, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();
        await _context.PlayerSeasonStats.AddAsync(new PlayerSeasonStat { UserID = user.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = "teamless", GamesPlayed = 10 });
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        Assert.That((stats.Seasons.Single().TeamID, stats.Seasons.Single().TeamName, stats.Seasons.Single().IsHistorical), Is.EqualTo(((int?)null, (string?)null, true)));
    }
}
