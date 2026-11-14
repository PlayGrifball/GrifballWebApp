using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileControllerTests
{
    private static readonly string[] ChampTagOldOnly = ["ChampTagOld"];
    private static readonly string[] PastTagsInOrder = ["Bravo", "Charlie", "Undated A", "Undated B"];

    private GrifballContext _context;
    private ISetGamertagService _setGamertagService;
    private ProfileController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _setGamertagService = Substitute.For<ISetGamertagService>();
        _controller = new ProfileController(_context, _setGamertagService, new GrifballWebApp.Application.Profile.ProfileStatsService(_context, new GrifballWebApp.Application.Stats.SeasonStatsService(_context), new GrifballWebApp.Application.LateLeague.MatchmakingStatsService(_context)), new GrifballWebApp.Application.Profile.ProfileCommendationsService(_context));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetGamertag_Should_BeNotFound_When_UserDoesNotExist()
    {
        // Arrange
        const int nonExistentUserId = 999;

        // Act
        var result = await _controller.GetGamertag(nonExistentUserId, CancellationToken.None);

        // Assert
        result.AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound, "There is no such user");
    }

    [Test]
    public async Task GetGamertag_Should_ReturnGamertag_When_UserHasXboxUser()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var xboxUser = XboxUser.FromXbox(12345, "TestGamer123");

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        user.TieToXbox(xboxUser.XboxUserID);
        _context.XboxUsers.Add(xboxUser);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _controller.GetGamertag(user.Id, CancellationToken.None)).Value;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Gamertag, Is.EqualTo("TestGamer123"));
    }

    [Test]
    public async Task GetGamertag_Should_ReturnDifferentGamertags_When_MultipleUsersExist()
    {
        // Arrange
        var user1 = User.Seed(userName: "user1", displayName: "User 1");
        var user2 = User.Seed(userName: "user2", displayName: "User 2");
        var xboxUser1 = XboxUser.FromXbox(111, "Gamer1");
        var xboxUser2 = XboxUser.FromXbox(222, "Gamer2");

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        user1.TieToXbox(xboxUser1.XboxUserID);
        user2.TieToXbox(xboxUser2.XboxUserID);
        _context.XboxUsers.AddRange(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        // Act
        var result1 = (await _controller.GetGamertag(user1.Id, CancellationToken.None)).Value;
        var result2 = (await _controller.GetGamertag(user2.Id, CancellationToken.None)).Value;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result1!.Gamertag, Is.EqualTo("Gamer1"));
            Assert.That(result2!.Gamertag, Is.EqualTo("Gamer2"));
        }
    }

    [Test]
    public async Task GetProfileStats_Should_ReturnNotFound_When_UserDoesNotExist()
    {
        var result = await _controller.GetProfileStats(999, CancellationToken.None);
        result.AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound, "There is no such user");
    }

    [Test]
    public async Task GetProfileStats_Should_ReturnFullProfileStats_WithChampionshipsAndCareerRollup()
    {
        var user = User.Seed(userName: "champion", displayName: "Spartan Legend");
        var xbox = XboxUser.FromXbox(777, "ChampTag");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        user.TieToXbox(xbox.XboxUserID);
        _context.XboxUsers.Add(xbox);

        // Both are seeded by the migrations
        var league = await _context.Leagues.SingleAsync(l => l.Slug == "agla");
        var h3 = await _context.GameVersions.SingleAsync(g => g.Slug == "halo3");

        var season = Season.Seed(seasonName: "Summer League 2018", leagueID: league.LeagueID, gameVersionID: h3.GameVesionID, historicalSourceID: "2018-sml18-stats/Stats", historicalSourceUrl: "https://docs.google.com/spreadsheets/d/x").WithDates(seasonStart: new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2018, 8, 1, 0, 0, 0, DateTimeKind.Utc), signupsOpen: new DateTime(2018, 5, 1, 0, 0, 0, DateTimeKind.Utc), signupsClose: new DateTime(2018, 5, 25, 0, 0, 0, DateTimeKind.Utc), draftStart: new DateTime(2018, 5, 26, 0, 0, 0, DateTimeKind.Utc));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var dynasty = Team.Seed(seasonID: season.SeasonID, teamName: "Dynasty");
        _context.Teams.Add(dynasty);
        await _context.SaveChangesAsync();

        var title = PlayerChampionship.Seed(userID: user.Id, seasonID: season.SeasonID, gameVersionID: h3.GameVesionID, tier: "Pro", teamID: dynasty.TeamID, isCaptain: true, gamertagUsed: "ChampTag");

        var stat = PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, h3.GameVesionID, new StatSheetTotals { Gamertag = "ChampTag", GamesPlayed = 10, Wins = 8, Losses = 2, Goals = 15, Kills = 120, Deaths = 60, Assists = 25, CarrierKills = 30, Sprees = 5, MultiKills = 4, Exterminations = 1 }, teamID: dynasty.TeamID, stintIndex: 1, isPlayoffs: false);

        var renamed = new XboxGamertagHistory(xbox.XboxUserID, "ChampTagOld", source: GamertagHistorySource.RenameDetected);

        _context.AddRange(title, stat, renamed);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.GetProfileStats(user.Id, CancellationToken.None);
        var stats = actionResult.Value;

        Assert.That(stats, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats!.UserID, Is.EqualTo(user.Id));
            Assert.That(stats.Gamertag, Is.EqualTo("ChampTag"));
            Assert.That(stats.DisplayName, Is.EqualTo("Spartan Legend"));
            Assert.That(stats.PastGamertags, Is.EqualTo(ChampTagOldOnly));

            Assert.That(stats.Championships, Has.Count.EqualTo(1));
            Assert.That(stats.Championships[0].Tier, Is.EqualTo("Pro"));
            Assert.That(stats.Championships[0].TeamName, Is.EqualTo("Dynasty"));
            Assert.That(stats.Championships[0].TeamID, Is.EqualTo(dynasty.TeamID));
            Assert.That(stats.Championships[0].Season.IsHistorical, Is.True);
            Assert.That(stats.Championships[0].IsCaptain, Is.True);
            Assert.That(stats.Championships[0].Season.Year, Is.EqualTo(2018));
            Assert.That(stats.Championships[0].Season.HistoricalSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/x"));
            Assert.That(stats.Seasons[0].Season.HistoricalSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/x"));
            Assert.That((stats.Seasons[0].TeamID, stats.Seasons[0].TeamName), Is.EqualTo(((int?)dynasty.TeamID, "Dynasty")));
            Assert.That(stats.Seasons[0].Season.IsHistorical, Is.True);

            Assert.That(stats.OverallCareer, Is.Not.Null);
            Assert.That(stats.OverallCareer!.GamesPlayed, Is.EqualTo(10));
            Assert.That(stats.OverallCareer.Wins, Is.EqualTo(8));
            Assert.That(stats.OverallCareer.Losses, Is.EqualTo(2));
            Assert.That(stats.OverallCareer.WinPercent, Is.EqualTo(80.0m));
            Assert.That(stats.OverallCareer.Goals, Is.Null, "Goals are only totalled per game");
            Assert.That(stats.OverallCareer.Kills, Is.EqualTo(120));
            Assert.That(stats.OverallCareer.Deaths, Is.EqualTo(60));
            Assert.That(stats.OverallCareer.KdRatio, Is.EqualTo(2.0m));
            Assert.That(stats.OverallCareer.KdSpread, Is.EqualTo(60));
            Assert.That(stats.OverallCareer.CarrierKills, Is.Null, "Carrier stops are only totalled per game");

            Assert.That(stats.CareerByGame, Has.Count.EqualTo(1));
            Assert.That(stats.CareerByGame[0].GameVersionSlug, Is.EqualTo("halo3"));
            Assert.That((stats.CareerByGame[0].Goals, stats.CareerByGame[0].CarrierKills), Is.EqualTo(((int?)15, (int?)30)));

            Assert.That(stats.Seasons, Has.Count.EqualTo(1));
            Assert.That(stats.Seasons[0].Season.SeasonName, Is.EqualTo("Summer League 2018"));
        }
    }

    [Test]
    public async Task GetProfileStats_ALiveSeasonsGames_AndAnotherGamesStoredTotals_PerGameStatsPerGame_CareerTotalsAcross()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.Set<SeriesGame>().Add(new SeriesGame { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 });
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 103);
        var old = Season.Seed(seasonName: "Winter League 2019", gameVersionID: 1, historicalSourceID: "wl19/stats").WithDates(seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.Seasons.Add(old);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(old.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "x", GamesPlayed = 10, Goals = 20, CarrierKills = 5 }));
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.Seasons.Select(s => (s.Season.SeasonID, s.Source, s.GamesPlayed, s.Goals, s.TeamName)), Is.EqualTo(new[]
            {
                (seeded.Season.SeasonID, "Matches", 1, 3, (string?)"Home Team"),
                (old.SeasonID, "Recorded", 10, 20, (string?)null),
            }), "The live season from its games, the old one from its totals");
            Assert.That(stats.OverallCareer!.GamesPlayed, Is.EqualTo(11));
            Assert.That((stats.OverallCareer.GoalsPerGame, stats.OverallCareer.StopsPerGame), Is.EqualTo(((decimal?)2.09m, (decimal?)0.5m)),
                "Goals and stops add up across games: 23 goals in 11 games, 5 stops in the 10 that record them");
            var infinite = stats.CareerByGame.Single(c => c.GameVersionSlug == "infinite");
            var halo3 = stats.CareerByGame.Single(c => c.GameVersionSlug == "halo3");
            Assert.That((infinite.GoalsPerGame, infinite.StopsPerGame), Is.EqualTo(((decimal?)3m, (decimal?)null)), "Waypoint games record no stops");
            Assert.That((halo3.GoalsPerGame, halo3.StopsPerGame), Is.EqualTo(((decimal?)2m, (decimal?)0.5m)));
        }
    }

    [Test]
    public async Task GetProfileStats_PastGamertags_LeaveOutTheCurrentOne_LatestFirst()
    {
        var user = User.Seed(userName: "renamer", xboxUser: XboxUser.FromXbox(4242, "Alpha"));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        DateTime At(int year) => new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.Set<XboxGamertagHistory>().AddRange(
            // Alpha -> Bravo -> Alpha: Alpha is in the history too
            new XboxGamertagHistory(4242, "Alpha", lastSeen: At(2020), source: GamertagHistorySource.RenameDetected),
            new XboxGamertagHistory(4242, "Bravo", lastSeen: At(2022), source: GamertagHistorySource.RenameDetected),
            new XboxGamertagHistory(4242, "Undated B", firstSeen: At(2019), source: GamertagHistorySource.Manual),
            new XboxGamertagHistory(4242, "Undated A", firstSeen: At(2018), source: GamertagHistorySource.Manual),
            new XboxGamertagHistory(4242, "Charlie", lastSeen: At(2016), source: GamertagHistorySource.Manual));
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        Assert.That(stats.PastGamertags, Is.EqualTo(PastTagsInOrder));
    }

    [Test]
    public async Task GetProfileStats_ARowWithoutATeam_HasNone()
    {
        var user = User.Seed(userName: "teamless");
        _context.Users.Add(user);
        var season = Season.Seed(seasonName: "Summer League 2021 (Pro)", gameVersionID: 1, historicalSourceID: "sml21/stats").WithDates(seasonStart: new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2021, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "teamless", GamesPlayed = 10 }));
        await _context.SaveChangesAsync();

        var stats = (await _controller.GetProfileStats(user.Id, CancellationToken.None)).Value!;

        Assert.That((stats.Seasons.Single().TeamID, stats.Seasons.Single().TeamName, stats.Seasons.Single().Season.IsHistorical), Is.EqualTo(((int?)null, (string?)null, true)));
    }
}
