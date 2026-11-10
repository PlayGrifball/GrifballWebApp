using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Integration.Test;

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

    private async Task<Season> AddSeason(string name, DateTime start, DateTime end, SeasonFormat format = SeasonFormat.LeagueSeason)
    {
        var season = Season.Seed(seasonName: name, format: format).WithDates(seasonStart: start, seasonEnd: end);
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
        _context.SeasonMatches.Add(SeasonMatch.Seed(regular.SeasonID, delta.TeamID, deltaRival.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Away));

        // 2018: an archive season's champions, the player captaining; its record is the spreadsheet's
        var archive = await AddSeason("Summer League 2018", Utc(2018), Utc(2018, 3));
        _context.MarkImported(archive, "2018-sml18/Stats");
        var dynasty = await _context.AddTeam(archive, "Dynasty", player, 1);
        dynasty.RecordArchive(8, 2);
        _context.Set<PlayerChampionship>().Add(PlayerChampionship.Seed(userID: player.Id, seasonID: archive.SeasonID, teamID: dynasty.TeamID, tier: "Pro", isCaptain: true, gamertagUsed: "Spartan"));

        // 2019: a regular-season win, then out in the playoff bracket
        var playoffs = await AddSeason("Season 3", Utc(2019), Utc(2019, 3));
        var bravo = await _context.AddTeam(playoffs, "Bravo", other, 1, player);
        var bravoRival = await _context.AddTeam(playoffs, "Bravo Rival", rival, 2);
        _context.SeasonMatches.Add(SeasonMatch.Seed(playoffs.SeasonID, bravo.TeamID, bravoRival.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home));
        _context.SeasonMatches.Add(SeasonMatch.Seed(playoffs.SeasonID, bravoRival.TeamID, bravo.TeamID, bestOf: 3, bracketMatch: MatchBracketInfo.Seed(roundNumber: 1, matchNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2, bracket: Bracket.Winner)).Forfeited(Forfeiter.Away));

        // Now: under way, nothing decided
        var current = await AddSeason("Season 4", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(30));
        var charlie = await _context.AddTeam(current, "Charlie", other, 1, player);

        // Someone else's team is not the player's
        await _context.AddTeam(current, "Not Mine", rival, 2);
        await _context.SaveChangesAsync();

        var history = (await _controller.GetDeploymentHistory(player.Id, CancellationToken.None)).Value!;

        Assert.That(history.Select(h => h.TeamName), Is.EqualTo(new[] { "Charlie", "Bravo", "Dynasty", "Delta" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That((history[0].TeamID, history[0].Season.SeasonID, history[0].Finish, history[0].Wins, history[0].Losses), Is.EqualTo((charlie.TeamID, current.SeasonID, DeploymentFinish.InProgress, 0, 0)));
            Assert.That(history[0].IsCaptain, Is.False);

            Assert.That((history[1].Finish, history[1].Wins, history[1].Losses, history[1].IsCaptain), Is.EqualTo((DeploymentFinish.Playoffs, 1, 1, false)),
                "A forfeit is a loss");
            Assert.That(history[1].ChampionshipTiers, Is.Empty);

            Assert.That((history[2].Finish, history[2].IsCaptain), Is.EqualTo((DeploymentFinish.Champion, true)));
            Assert.That(history[2].ChampionshipTiers, Is.EqualTo(new[] { "Pro" }));
            Assert.That((history[2].Wins, history[2].Losses, history[2].RecordFromSource), Is.EqualTo((8, 2, true)));
            Assert.That(history[2].Season.IsHistorical, Is.True);

            Assert.That((history[3].Finish, history[3].Wins, history[3].Losses, history[3].RecordFromSource), Is.EqualTo((DeploymentFinish.RegularSeason, 0, 1, false)));
            Assert.That(history[3].Season.SeasonName, Is.EqualTo("Season 1"));
        }
    }

    [Test]
    public async Task GetDeploymentHistory_PlayoffStatsFromTheArchive_ArePlayoffs()
    {
        var player = await _context.AddUser("archived", gamertag: "Archived");
        var season = await AddSeason("Winter 2016", Utc(2016), Utc(2016, 3));
        var team = await _context.AddTeam(season, "Echo", player, 1);
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(season.SeasonID, player.Id, 1, new StatSheetTotals { Gamertag = "Archived", GamesPlayed = 2 }, teamID: team.TeamID, isPlayoffs: true));
        await _context.SaveChangesAsync();

        var history = (await _controller.GetDeploymentHistory(player.Id, CancellationToken.None)).Value!;

        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That((history[0].Finish, history[0].IsCaptain), Is.EqualTo((DeploymentFinish.Playoffs, true)));
    }

    [Test]
    public async Task GetDeploymentHistory_ATeamWithTitlesInSeveralTiers_HasEveryTier()
    {
        var player = await _context.AddUser("twotitles", gamertag: "TwoTitles");
        var mate = await _context.AddUser("mate", gamertag: "Mate");
        var season = await AddSeason("Archive 2015", Utc(2015), Utc(2015, 3));
        var team = await _context.AddTeam(season, "Foxtrot", player, 1, mate);
        _context.Set<PlayerChampionship>().AddRange(
            PlayerChampionship.Seed(userID: player.Id, seasonID: season.SeasonID, teamID: team.TeamID, tier: "Pro", isCaptain: true, gamertagUsed: "TwoTitles"),
            PlayerChampionship.Seed(userID: player.Id, seasonID: season.SeasonID, teamID: team.TeamID, tier: "Amateur", isCaptain: true, gamertagUsed: "TwoTitles"),
            PlayerChampionship.Seed(userID: mate.Id, seasonID: season.SeasonID, teamID: team.TeamID, tier: "Pro", isCaptain: false, gamertagUsed: "Mate"));
        await _context.SaveChangesAsync();

        var history = (await _controller.GetDeploymentHistory(player.Id, CancellationToken.None)).Value!;

        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].Finish, Is.EqualTo(DeploymentFinish.Champion));
        Assert.That(history[0].ChampionshipTiers, Is.EqualTo(new[] { "Amateur", "Pro" }), "Each tier once, A to Z");
    }

    [Test]
    public async Task GetDeploymentHistory_ATournament_IsNeitherPlayoffsNorRegularSeason()
    {
        var player = await _context.AddUser("respawn", gamertag: "Respawn");
        var rival = await _context.AddUser("bracketrival", gamertag: "BracketRival");

        // Over: out in the bracket, no title
        var over = await AddSeason("Respawn 2024", Utc(2024), Utc(2024, 2), SeasonFormat.Tournament);
        var golf = await _context.AddTeam(over, "Golf", player, 1);
        var golfRival = await _context.AddTeam(over, "Golf Rival", rival, 2);
        _context.SeasonMatches.Add(SeasonMatch.Seed(over.SeasonID, golfRival.TeamID, golf.TeamID, bestOf: 3, bracketMatch: MatchBracketInfo.Seed(roundNumber: 1, matchNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2, bracket: Bracket.Winner)).WonBy(SeasonMatchSide.Home));

        // Over without a bracket match: still not a regular season
        var unplayed = await AddSeason("Respawn 2023", Utc(2023), Utc(2023, 2), SeasonFormat.Tournament);
        await _context.AddTeam(unplayed, "Hotel", player, 1);

        // Under way
        var current = await AddSeason("Respawn Now", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(6), SeasonFormat.Tournament);
        await _context.AddTeam(current, "India", player, 1);
        await _context.SaveChangesAsync();

        var history = (await _controller.GetDeploymentHistory(player.Id, CancellationToken.None)).Value!;

        Assert.That(history.Select(h => (h.TeamName, h.Finish)), Is.EqualTo(new[]
        {
            ("India", DeploymentFinish.InProgress),
            ("Golf", DeploymentFinish.Eliminated),
            ("Hotel", DeploymentFinish.Eliminated),
        }));
    }
}
