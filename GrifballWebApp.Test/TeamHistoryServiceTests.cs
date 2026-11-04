using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.TeamPage;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamHistoryServiceTests
{
    private GrifballContext _context = null!;
    private TeamHistoryService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new TeamHistoryService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<Season> AddSeason(string name, int year, string? division = null, string? historicalSourceID = null)
    {
        var season = new Season
        {
            SeasonName = name,
            SeasonStart = new DateTime(year, 1, 1),
            SeasonEnd = new DateTime(year, 3, 1),
            Division = division,
            HistoricalSourceID = historicalSourceID,
        };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private static SeasonMatch Played(Season season, Team home, Team away, bool homeWon) => new()
    {
        SeasonID = season.SeasonID,
        HomeTeamID = home.TeamID,
        AwayTeamID = away.TeamID,
        HomeTeamResult = homeWon ? SeasonMatchResult.Won : SeasonMatchResult.Loss,
        AwayTeamResult = homeWon ? SeasonMatchResult.Loss : SeasonMatchResult.Won,
        HomeTeamScore = homeWon ? 2 : 0,
        AwayTeamScore = homeWon ? 0 : 2,
        BestOf = 3,
    };

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("No Such Team")]
    public async Task Get_Should_ReturnNull_When_NoTeamHasTheName(string? name)
    {
        var season = await AddSeason("S1", 2020);
        await _context.AddTeam(season, "Trophy Hunters", await _context.AddUser("cap", gamertag: "Cap"), 1);

        Assert.That(await _service.GetAsync(name), Is.Null);
    }

    [Test]
    public async Task Get_Should_GroupTheNamesTeams_AcrossSeasons_NewestFirst_WithRecordsRostersAndTitles()
    {
        var s2019 = await AddSeason("Season 2019", 2019, historicalSourceID: "archive/2019");
        var s2021 = await AddSeason("Season 2021", 2021);
        var s2023 = await AddSeason("Season 2023", 2023);
        var cap = await _context.AddUser("cap", gamertag: "Captain Grif");
        var simmons = await _context.AddUser("simmons", gamertag: "Simmons");
        var donut = await _context.AddUser("donut", gamertag: "Donut");
        var other = await _context.AddUser("other", gamertag: "Other");

        // The same franchise, spelled three ways
        var old = await _context.AddTeam(s2019, "trophy hunters", cap, 1, simmons);
        old.RecordedWins = 7;
        old.RecordedLosses = 2;
        var mid = await _context.AddTeam(s2021, "Trophy Hunters ", simmons, 1, donut);
        var latest = await _context.AddTeam(s2023, "TROPHY HUNTERS", cap, 1, donut, simmons);
        // Not the franchise
        var rival = await _context.AddTeam(s2021, "Texas Shock", other, 2);
        await _context.AddTeam(s2023, "Trophy Hunters Jr", other, 2);

        _context.SeasonMatches.AddRange(
            Played(s2021, mid, rival, homeWon: true),
            Played(s2021, rival, mid, homeWon: true),
            Played(s2021, mid, rival, homeWon: true));
        _context.PlayerChampionships.AddRange(
            new PlayerChampionship { SeasonID = s2019.SeasonID, TeamID = old.TeamID, UserID = cap.Id, Tier = "Pro", IsCaptain = true, GamertagUsed = "Captain Grif" },
            new PlayerChampionship { SeasonID = s2019.SeasonID, TeamID = old.TeamID, UserID = simmons.Id, Tier = "Pro", GamertagUsed = "Simmons" },
            new PlayerChampionship { SeasonID = s2023.SeasonID, TeamID = latest.TeamID, UserID = cap.Id, Tier = "Open", IsCaptain = true, GamertagUsed = "Captain Grif" });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var history = await _service.GetAsync("  Trophy Hunters  ");

        Assert.That(history, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(history!.TeamName, Is.EqualTo("TROPHY HUNTERS"));
            Assert.That(history.Appearances, Is.EqualTo(3));
            Assert.That(history.Wins, Is.EqualTo(7 + 2));
            Assert.That(history.Losses, Is.EqualTo(2 + 1));
            Assert.That(history.Championships, Is.EqualTo(2));
            Assert.That(history.Players, Is.EqualTo(3));
            Assert.That(history.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { latest.TeamID, mid.TeamID, old.TeamID }));
            Assert.That(history.Seasons.Select(s => s.Season.SeasonName), Is.EqualTo(new[] { "Season 2023", "Season 2021", "Season 2019" }));
            Assert.That(history.Seasons.Select(s => s.TeamName), Is.EqualTo(new[] { "TROPHY HUNTERS", "Trophy Hunters ", "trophy hunters" }));

            var newest = history.Seasons[0];
            Assert.That(newest.Titles, Is.EqualTo(new[] { "Open" }));
            Assert.That((newest.Wins, newest.Losses, newest.RecordFromSource), Is.EqualTo((0, 0, false)));
            // Captain first, then by draft round
            Assert.That(newest.Players.Select(p => p.Gamertag), Is.EqualTo(new[] { "Captain Grif", "Donut", "Simmons" }));
            Assert.That(newest.Players.Select(p => p.IsCaptain), Is.EqualTo(new[] { true, false, false }));

            var middle = history.Seasons[1];
            Assert.That((middle.Wins, middle.Losses, middle.RecordFromSource), Is.EqualTo((2, 1, false)));
            Assert.That(middle.Titles, Is.Empty);
            Assert.That(middle.Players[0].Gamertag, Is.EqualTo("Simmons"));

            var oldest = history.Seasons[2];
            Assert.That((oldest.Wins, oldest.Losses, oldest.RecordFromSource), Is.EqualTo((7, 2, true)));
            Assert.That(oldest.Titles, Is.EqualTo(new[] { "Pro" }));
            Assert.That(oldest.Season.IsHistorical, Is.True);
        });
    }

    private async Task AddFinal(Season season, Team home, Team away, bool homeWon)
    {
        var final = Played(season, home, away, homeWon);
        final.BracketMatch = new MatchBracketInfo { Bracket = Bracket.Winner, RoundNumber = 1, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2 };
        _context.SeasonMatches.Add(final);
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Get_Should_CountTheBracketsChampion_When_TheSeasonRecordedNoTitle()
    {
        var open = await AddSeason("Open Season", 2022);
        var pro = await AddSeason("Pro Season", 2023, division: "Pro");
        var cap = await _context.AddUser("cap", gamertag: "Cap");
        var rivalCap = await _context.AddUser("rival", gamertag: "Rival");
        var openTeam = await _context.AddTeam(open, "Texas Shock", cap, 1);
        var proTeam = await _context.AddTeam(pro, "Texas Shock", cap, 1);
        await AddFinal(open, openTeam, await _context.AddTeam(open, "Rivals", rivalCap, 2), homeWon: true);
        // Lost its final: no title
        await AddFinal(pro, await _context.AddTeam(pro, "Rivals", rivalCap, 2), proTeam, homeWon: true);
        _context.ChangeTracker.Clear();

        var history = await _service.GetAsync("texas shock");

        Assert.That(history, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(history!.Seasons.Select(s => s.Titles), Is.EqualTo(new[] { Array.Empty<string>(), new[] { "Open" } }));
            Assert.That(history.Championships, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Get_Should_NameTheDivision_When_TheBracketsChampionHasNoTitleRecorded()
    {
        var pro = await AddSeason("Pro Season", 2023, division: "Pro");
        var cap = await _context.AddUser("cap", gamertag: "Cap");
        var team = await _context.AddTeam(pro, "Texas Shock", cap, 1);
        await AddFinal(pro, team, await _context.AddTeam(pro, "Rivals", await _context.AddUser("rival"), 2), homeWon: true);
        _context.ChangeTracker.Clear();

        var history = await _service.GetAsync("Texas Shock");

        Assert.That(history!.Seasons.Single().Titles, Is.EqualTo(new[] { "Pro" }));
    }

    [Test]
    public async Task Get_Should_NotCountAnImportedSeasonsBracket_OnlyItsRecordedTitles()
    {
        var imported = await AddSeason("Archive Season", 2015, historicalSourceID: "archive/2015");
        var cap = await _context.AddUser("cap", gamertag: "Cap");
        var team = await _context.AddTeam(imported, "Texas Shock", cap, 1);
        await AddFinal(imported, team, await _context.AddTeam(imported, "Rivals", await _context.AddUser("rival"), 2), homeWon: true);
        _context.ChangeTracker.Clear();

        var history = await _service.GetAsync("Texas Shock");

        Assert.That(history!.Seasons.Single().Titles, Is.Empty);
    }
}
