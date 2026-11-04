using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DeploymentHistoryTests
{
    private GrifballContext _context;
    private DeploymentHistoryController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new DeploymentHistoryController(new DeploymentHistoryService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<Season> AddSeason(string name, DateTime start, DateTime end)
    {
        var season = new Season { SeasonName = name, SeasonStart = start, SeasonEnd = end };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private static DateTime Utc(int year, int month = 1) => new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetDeploymentHistory_NotFound_WhenTheUserDoesNotExist()
    {
        var result = await _controller.GetDeploymentHistory(999, CancellationToken.None);
        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetDeploymentHistory_Empty_ForAPlayerOnNoTeam()
    {
        var user = await _context.AddUser("benched");
        var result = await _controller.GetDeploymentHistory(user.Id, CancellationToken.None);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task GetDeploymentHistory_EachTeam_NewestFirst_WithCaptainRecordAndFinish()
    {
        var player = await _context.AddUser("player", gamertag: "Spartan");
        var other = await _context.AddUser("other", gamertag: "Other");
        var rival = await _context.AddUser("rival", gamertag: "Rival");

        // 2017: regular season only, a record of its matches
        var regular = await AddSeason("Season 1", Utc(2017), Utc(2017, 3));
        var delta = await _context.AddTeam(regular, "Delta", other, 1, player);
        var deltaRival = await _context.AddTeam(regular, "Delta Rival", rival, 2);
        _context.SeasonMatches.Add(new SeasonMatch { SeasonID = regular.SeasonID, BestOf = 1, HomeTeamID = delta.TeamID, HomeTeamResult = SeasonMatchResult.Loss, AwayTeamID = deltaRival.TeamID, AwayTeamResult = SeasonMatchResult.Won });

        // 2018: an archive season's champions, the player captaining; its record is the spreadsheet's
        var archive = await AddSeason("Summer League 2018", Utc(2018), Utc(2018, 3));
        archive.HistoricalSourceID = "2018-sml18/Stats";
        var dynasty = await _context.AddTeam(archive, "Dynasty", player, 1);
        dynasty.RecordedWins = 8;
        dynasty.RecordedLosses = 2;
        _context.PlayerChampionships.Add(new PlayerChampionship { UserID = player.Id, SeasonID = archive.SeasonID, TeamID = dynasty.TeamID, Tier = "Pro", IsCaptain = true, GamertagUsed = "Spartan" });

        // 2019: a regular-season win, then out in the playoff bracket
        var playoffs = await AddSeason("Season 3", Utc(2019), Utc(2019, 3));
        var bravo = await _context.AddTeam(playoffs, "Bravo", other, 1, player);
        var bravoRival = await _context.AddTeam(playoffs, "Bravo Rival", rival, 2);
        _context.SeasonMatches.Add(new SeasonMatch { SeasonID = playoffs.SeasonID, BestOf = 1, HomeTeamID = bravo.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamID = bravoRival.TeamID, AwayTeamResult = SeasonMatchResult.Loss });
        _context.SeasonMatches.Add(new SeasonMatch
        {
            SeasonID = playoffs.SeasonID, BestOf = 3, HomeTeamID = bravoRival.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamID = bravo.TeamID, AwayTeamResult = SeasonMatchResult.Forfeit,
            BracketMatch = new MatchBracketInfo { RoundNumber = 1, MatchNumber = 1, Bracket = Bracket.Winner },
        });

        // Now: under way, nothing decided
        var current = await AddSeason("Season 4", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(30));
        var charlie = await _context.AddTeam(current, "Charlie", other, 1, player);

        // Someone else's team is not the player's
        await _context.AddTeam(current, "Not Mine", rival, 2);
        await _context.SaveChangesAsync();

        var history = (await _controller.GetDeploymentHistory(player.Id, CancellationToken.None)).Value!;

        Assert.That(history.Select(h => h.TeamName), Is.EqualTo(new[] { "Charlie", "Bravo", "Dynasty", "Delta" }));
        Assert.Multiple(() =>
        {
            Assert.That((history[0].TeamID, history[0].Season.SeasonID, history[0].Finish, history[0].Wins, history[0].Losses), Is.EqualTo((charlie.TeamID, current.SeasonID, DeploymentFinish.InProgress, 0, 0)));
            Assert.That(history[0].IsCaptain, Is.False);

            Assert.That((history[1].Finish, history[1].Wins, history[1].Losses, history[1].IsCaptain), Is.EqualTo((DeploymentFinish.Playoffs, 1, 1, false)),
                "A forfeit is a loss");
            Assert.That(history[1].ChampionshipTier, Is.Null);

            Assert.That((history[2].Finish, history[2].ChampionshipTier, history[2].IsCaptain), Is.EqualTo((DeploymentFinish.Champion, "Pro", true)));
            Assert.That((history[2].Wins, history[2].Losses, history[2].RecordFromSource), Is.EqualTo((8, 2, true)));
            Assert.That(history[2].Season.IsHistorical, Is.True);

            Assert.That((history[3].Finish, history[3].Wins, history[3].Losses, history[3].RecordFromSource), Is.EqualTo((DeploymentFinish.RegularSeason, 0, 1, false)));
            Assert.That(history[3].Season.SeasonName, Is.EqualTo("Season 1"));
        });
    }

    [Test]
    public async Task GetDeploymentHistory_PlayoffStatsFromTheArchive_ArePlayoffs()
    {
        var player = await _context.AddUser("archived", gamertag: "Archived");
        var season = await AddSeason("Winter 2016", Utc(2016), Utc(2016, 3));
        var team = await _context.AddTeam(season, "Echo", player, 1);
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = player.Id, SeasonID = season.SeasonID, TeamID = team.TeamID, GameVersionID = 1, Gamertag = "Archived", IsPlayoffs = true, GamesPlayed = 2 });
        await _context.SaveChangesAsync();

        var history = (await _controller.GetDeploymentHistory(player.Id, CancellationToken.None)).Value!;

        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That((history[0].Finish, history[0].IsCaptain), Is.EqualTo((DeploymentFinish.Playoffs, true)));
    }

    [TestCase(true, true, true, DeploymentFinish.Champion)]
    [TestCase(true, false, false, DeploymentFinish.Champion)]
    [TestCase(false, true, false, DeploymentFinish.Playoffs)]
    [TestCase(false, true, true, DeploymentFinish.Playoffs)]
    [TestCase(false, false, true, DeploymentFinish.RegularSeason)]
    [TestCase(false, false, false, DeploymentFinish.InProgress)]
    public void FinishOf_TheBestTheTeamDid(bool champion, bool playedPlayoffs, bool seasonEnded, DeploymentFinish expected)
    {
        Assert.That(DeploymentHistoryService.FinishOf(champion, playedPlayoffs, seasonEnded), Is.EqualTo(expected));
    }
}
