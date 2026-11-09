using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
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

    private async Task<Season> AddSeason(string name, int year, string? historicalSourceID = null, League? league = null)
    {
        var season = Season.Seed(seasonName: name, historicalSourceID: historicalSourceID, league: league).WithDates(seasonStart: new DateTime(year, 1, 1), seasonEnd: new DateTime(year, 3, 1));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private static SeasonMatch Played(Season season, Team home, Team away, bool homeWon, MatchBracketInfo? bracketMatch = null) =>
        SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3, bracketMatch: bracketMatch)
            .WonBy(homeWon ? SeasonMatchSide.Home : SeasonMatchSide.Away, 2, 0);


    [Test]
    public async Task Get_Should_ReturnNull_When_ThereIsNoSuchTeam()
    {
        var season = await AddSeason("S1", 2020);
        await _context.AddTeam(season, "Trophy Hunters", await _context.AddUser("cap", gamertag: "Cap"), 1);

        Assert.That(await _service.GetAsync(12345), Is.Null);
    }

    [Test]
    public async Task Get_Should_GroupTheNamesTeams_AcrossSeasons_NewestFirst_WithRecordsRostersAndTitles()
    {
        var s2017 = await AddSeason("Season 2017", 2017, historicalSourceID: "archive/2017");
        var s2019 = await AddSeason("Season 2019", 2019, historicalSourceID: "archive/2019");
        var s2021 = await AddSeason("Season 2021", 2021);
        var s2023 = await AddSeason("Season 2023", 2023);
        var cap = await _context.AddUser("cap", gamertag: "Captain Grif");
        var simmons = await _context.AddUser("simmons", gamertag: "Simmons");
        var donut = await _context.AddUser("donut", gamertag: "Donut");
        var other = await _context.AddUser("other", gamertag: "Other");

        // The same franchise, cased three ways, and once stored with the spaces around it that signups kept before
        var untrimmed = await _context.AddTeam(s2017, "  Trophy Hunters ", other, 1);
        var old = await _context.AddTeam(s2019, "trophy hunters", cap, 1, simmons);
        old.RecordArchive(7, 2);
        var mid = await _context.AddTeam(s2021, "Trophy Hunters", simmons, 1, donut);
        var latest = await _context.AddTeam(s2023, "TROPHY HUNTERS", cap, 1, donut, simmons);
        // Not the franchise
        var rival = await _context.AddTeam(s2021, "Texas Shock", other, 2);
        await _context.AddTeam(s2023, "Trophy Hunters Jr", other, 2);

        _context.SeasonMatches.AddRange(
            Played(s2021, mid, rival, homeWon: true),
            Played(s2021, rival, mid, homeWon: true),
            Played(s2021, mid, rival, homeWon: true));
        _context.PlayerChampionships.AddRange(
            PlayerChampionship.Seed(seasonID: s2019.SeasonID, teamID: old.TeamID, userID: cap.Id, tier: "Pro", isCaptain: true, gamertagUsed: "Captain Grif"),
            PlayerChampionship.Seed(seasonID: s2019.SeasonID, teamID: old.TeamID, userID: simmons.Id, tier: "Pro", gamertagUsed: "Simmons"),
            // Two tiers' titles, recorded Pro first: shown in name order
            PlayerChampionship.Seed(seasonID: s2023.SeasonID, teamID: latest.TeamID, userID: donut.Id, tier: "Pro", gamertagUsed: "Donut"),
            PlayerChampionship.Seed(seasonID: s2023.SeasonID, teamID: latest.TeamID, userID: cap.Id, tier: "Open", isCaptain: true, gamertagUsed: "Captain Grif"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var history = await _service.GetAsync(mid.TeamID);

        Assert.That(history, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(history!.TeamName, Is.EqualTo("TROPHY HUNTERS"));
            Assert.That(history.LeagueName, Is.EqualTo("AGLA"));
            Assert.That(history.Appearances, Is.EqualTo(4));
            Assert.That(history.Wins, Is.EqualTo(7 + 2));
            Assert.That(history.Losses, Is.EqualTo(2 + 1));
            Assert.That(history.Championships, Is.EqualTo(3));
            Assert.That(history.Players, Is.EqualTo(4));
            Assert.That(history.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { latest.TeamID, mid.TeamID, old.TeamID, untrimmed.TeamID }));
            Assert.That(history.Seasons.Select(s => s.Season.SeasonName), Is.EqualTo(new[] { "Season 2023", "Season 2021", "Season 2019", "Season 2017" }));
            Assert.That(history.Seasons.Select(s => s.TeamName), Is.EqualTo(new[] { "TROPHY HUNTERS", "Trophy Hunters", "trophy hunters", "  Trophy Hunters " }));

            var newest = history.Seasons[0];
            Assert.That(newest.Titles, Is.EqualTo(new[] { "Open", "Pro" }));
            Assert.That((newest.Wins, newest.Losses, newest.RecordFromSource), Is.EqualTo((0, 0, false)));
            // Captain first, then by draft round
            Assert.That(newest.Players.Select(p => p.Gamertag), Is.EqualTo(new[] { "Captain Grif", "Donut", "Simmons" }));
            Assert.That(newest.Players.Select(p => p.IsCaptain), Is.EqualTo(new[] { true, false, false }));

            var middle = history.Seasons[1];
            Assert.That((middle.Wins, middle.Losses, middle.RecordFromSource), Is.EqualTo((2, 1, false)));
            Assert.That(middle.Titles, Is.Empty);
            Assert.That(middle.Players[0].Gamertag, Is.EqualTo("Simmons"));

            var older = history.Seasons[2];
            Assert.That((older.Wins, older.Losses, older.RecordFromSource), Is.EqualTo((7, 2, true)));
            Assert.That(older.Titles, Is.EqualTo(new[] { "Pro" }));
            Assert.That(older.Season.IsHistorical, Is.True);
        });
    }

    [Test]
    public async Task Get_Should_FindTheSameFranchise_FromAnyOfItsTeams_StoredTrimmedOrNot()
    {
        var s1 = await AddSeason("S1", 2020);
        var s2 = await AddSeason("S2", 2021);
        var untrimmed = await _context.AddTeam(s1, " Texas Shock  ", await _context.AddUser("a", gamertag: "A"), 1);
        var trimmed = await _context.AddTeam(s2, "texas shock", await _context.AddUser("b", gamertag: "B"), 1);
        _context.ChangeTracker.Clear();

        var fromUntrimmed = await _service.GetAsync(untrimmed.TeamID);
        var fromTrimmed = await _service.GetAsync(trimmed.TeamID);

        var both = new[] { trimmed.TeamID, untrimmed.TeamID };
        Assert.Multiple(() =>
        {
            Assert.That(fromUntrimmed!.Seasons.Select(s => s.TeamID), Is.EqualTo(both));
            Assert.That(fromTrimmed!.Seasons.Select(s => s.TeamID), Is.EqualTo(both));
        });
    }

    [Test]
    public async Task Get_Should_NotMergeTeamsOfAPlaceholderName_AcrossSeasons()
    {
        var s1 = await AddSeason("Archive 2015", 2015, historicalSourceID: "archive/2015");
        var s2 = await AddSeason("Archive 2016", 2016, historicalSourceID: "archive/2016");
        var first = await _context.AddTeam(s1, "Team 1", await _context.AddUser("a", gamertag: "A"), 1);
        var second = await _context.AddTeam(s2, " team 1", await _context.AddUser("b", gamertag: "B"), 1);
        _context.ChangeTracker.Clear();

        var firstHistory = await _service.GetAsync(first.TeamID);
        var secondHistory = await _service.GetAsync(second.TeamID);

        Assert.Multiple(() =>
        {
            Assert.That(firstHistory!.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { first.TeamID }));
            Assert.That((firstHistory.Appearances, firstHistory.Players), Is.EqualTo((1, 1)));
            Assert.That(secondHistory!.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { second.TeamID }));
        });
    }

    [TestCase("Team 1", ExpectedResult = true)]
    [TestCase("team 12", ExpectedResult = true)]
    [TestCase("Team #3", ExpectedResult = true)]
    [TestCase("Team3", ExpectedResult = true)]
    [TestCase("  TEAM   A ", ExpectedResult = true)]
    [TestCase("Subs", ExpectedResult = true)]
    [TestCase("Free Agents", ExpectedResult = true)]
    [TestCase("", ExpectedResult = true)]
    [TestCase(null, ExpectedResult = true)]
    [TestCase("Trophy Hunters", ExpectedResult = false)]
    [TestCase("Team Rocket", ExpectedResult = false)]
    [TestCase("Team 1 Elite", ExpectedResult = false)]
    [TestCase("A Team", ExpectedResult = false)]
    [TestCase("TeamZ", ExpectedResult = false)]
    public bool IsPlaceholderName_Should_MatchOnlyGenericNames(string? name) => Franchises.IsPlaceholderName(name);

    private async Task AddFinal(Season season, Team home, Team away, bool homeWon)
    {
        var final = Played(season, home, away, homeWon,
            MatchBracketInfo.Seed(bracket: Bracket.Winner, roundNumber: 1, matchNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2));
        _context.SeasonMatches.Add(final);
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Get_Should_ShowTheTitlesTheTeamPageShows_NotABracketWinWithNoTitleRecorded()
    {
        var season = await AddSeason("Open Season", 2022);
        var cap = await _context.AddUser("cap", gamertag: "Cap");
        var team = await _context.AddTeam(season, "Texas Shock", cap, 1);
        await AddFinal(season, team, await _context.AddTeam(season, "Rivals", await _context.AddUser("rival"), 2), homeWon: true);
        _context.ChangeTracker.Clear();

        var history = await _service.GetAsync(team.TeamID);
        var page = await new TeamPageService(_context).GetAsync(team.TeamID);

        Assert.That(history!.Seasons.Single().Titles, Is.Empty);
        Assert.That(history.Championships, Is.Zero);
        Assert.That(page!.Titles, Is.Empty);
    }

    [Test]
    public async Task Get_Should_KeepAFranchiseToItsLeague()
    {
        var other = League.Seed("Other League", "other");
        var agla = await AddSeason("AGLA Season", 2021);
        var elsewhere = await AddSeason("Other Season", 2022, league: other);
        var cap = await _context.AddUser("cap", gamertag: "Cap");
        var rival = await _context.AddUser("rival", gamertag: "Rival");
        var ours = await _context.AddTeam(agla, "Texas Shock", cap, 1);
        var theirs = await _context.AddTeam(elsewhere, "texas shock", rival, 1);
        _context.ChangeTracker.Clear();

        var aglaHistory = await _service.GetAsync(ours.TeamID);
        var otherHistory = await _service.GetAsync(theirs.TeamID);

        Assert.Multiple(() =>
        {
            Assert.That(aglaHistory!.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { ours.TeamID }));
            Assert.That(aglaHistory.Players, Is.EqualTo(1));
            Assert.That(aglaHistory.LeagueName, Is.EqualTo("AGLA"));
            Assert.That(otherHistory!.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { theirs.TeamID }));
            Assert.That(otherHistory.LeagueName, Is.EqualTo("Other League"));
            Assert.That(otherHistory.Seasons[0].Season.LeagueSlug, Is.EqualTo("other"));
        });
    }
}
