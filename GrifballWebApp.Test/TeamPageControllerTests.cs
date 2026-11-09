using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamPageControllerTests
{
    private GrifballContext _context = null!;
    private TeamController _controller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new TeamController(_context, new GrifballWebApp.Server.TeamPage.TeamPageService(_context), new TeamHistoryService(_context));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<(Season season, Team a, Team b, Team c, User capA, User p1, User p2)> Seed()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA", gamertag: "CapA");
        var p1 = await _context.AddUser("p1", gamertag: "P1");
        var p2 = await _context.AddUser("p2", gamertag: "P2");
        var capB = await _context.AddUser("capB", gamertag: "CapB");
        var capC = await _context.AddUser("capC", gamertag: "CapC");
        // players added in reverse round order to prove sorting
        var a = await _context.AddTeam(season, "Alpha", capA, 1, p1, p2);
        var b = await _context.AddTeam(season, "Bravo", capB, 2);
        var c = await _context.AddTeam(season, "Charlie", capC, 3);
        a.MovePick(p2.Id, 1);

        _context.SeasonMatches.AddRange(
            // A home win vs B (played)
            SeasonMatch.Seed(season.SeasonID, a.TeamID, b.TeamID, bestOf: 3, scheduledTime: new DateTime(2025, 1, 1)).WonBy(SeasonMatchSide.Home, 2, 0),
            // A away loss vs C (played)
            SeasonMatch.Seed(season.SeasonID, c.TeamID, a.TeamID, bestOf: 3, scheduledTime: new DateTime(2025, 1, 2)).WonBy(SeasonMatchSide.Home, 2, 1),
            // A away forfeit vs B (counts as a loss, no score)
            SeasonMatch.Seed(season.SeasonID, b.TeamID, a.TeamID, bestOf: 3, scheduledTime: new DateTime(2025, 1, 3)).Forfeited(Forfeiter.Away),
            // Unplayed, later
            SeasonMatch.Seed(season.SeasonID, a.TeamID, c.TeamID, bestOf: 5, scheduledTime: new DateTime(2025, 2, 10)),
            // Unplayed, earlier (away)
            SeasonMatch.Seed(season.SeasonID, b.TeamID, a.TeamID, bestOf: 3, scheduledTime: new DateTime(2025, 2, 1)),
            // Unrelated match
            SeasonMatch.Seed(season.SeasonID, b.TeamID, c.TeamID, bestOf: 3, scheduledTime: new DateTime(2025, 1, 5)));
        await _context.SaveChangesAsync();
        return (season, a, b, c, capA, p1, p2);
    }

    [Test]
    public async Task Team_Should_ReturnNull_When_NotFound()
    {
        Assert.That(await _controller.Team(12345, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task History_Should_ReturnTheFranchise_OfTheTeam()
    {
        var (season, a, _, _, _, _, _) = await Seed();

        var history = await _controller.History(a.TeamID, CancellationToken.None);

        Assert.That(history, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(history!.TeamName, Is.EqualTo("Alpha"));
            Assert.That(history.Seasons.Select(s => (s.TeamID, s.Season.SeasonID, s.Wins, s.Losses)), Is.EqualTo(new[] { (a.TeamID, season.SeasonID, 1, 2) }));
        });
        Assert.That(await _controller.History(12345, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Team_Should_ComputeRecord_And_OrderPlayers_CaptainFirst()
    {
        var (_, a, _, _, capA, p1, p2) = await Seed();

        var dto = await _controller.Team(a.TeamID, CancellationToken.None);

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.TeamName, Is.EqualTo("Alpha"));
            Assert.That(dto.Wins, Is.EqualTo(1));
            Assert.That(dto.Losses, Is.EqualTo(2));
            Assert.That(dto.TBD, Is.EqualTo(2));
            Assert.That(dto.Players.Select(p => p.UserID), Is.EqualTo(new[] { capA.Id, p2.Id, p1.Id }));
            Assert.That(dto.Players.Select(p => p.Gamertag), Is.EqualTo(new[] { "CapA", "P2", "P1" }));
            Assert.That(dto.Players[0].DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(dto.Players[1].DraftRound, Is.EqualTo(1));
            Assert.That(dto.Players.Select(p => p.TeamPlayerID), Is.All.GreaterThan(0));
        });
    }

    [Test]
    public async Task Matches_Should_ListHomeAndAway_FromTeamPerspective_UnplayedFirst()
    {
        var (_, a, b, c, _, _, _) = await Seed();

        var matches = await _controller.Matches(a.TeamID, CancellationToken.None);

        Assert.That(matches, Has.Length.EqualTo(5));
        Assert.That(matches.Select(m => m.ScheduledTime), Is.EqualTo(new DateTime?[]
        {
            new DateTime(2025, 2, 1), new DateTime(2025, 2, 10), // unplayed, by date
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 2), new DateTime(2025, 1, 3),
        }));
        Assert.Multiple(() =>
        {
            Assert.That(matches[0].OtherTeamID, Is.EqualTo(b.TeamID));
            Assert.That(matches[0].Result, Is.Null);
            Assert.That(matches[1].OtherTeamName, Is.EqualTo("Charlie"));
            Assert.That(matches[1].BestOf, Is.EqualTo(5));

            // home win: perspective of A
            Assert.That((matches[2].Score, matches[2].OtherScore, matches[2].Result, matches[2].OtherResult, matches[2].OtherTeamName),
                Is.EqualTo(((int?)2, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss, "Bravo")));
            // away loss: scores are swapped to A's perspective
            Assert.That((matches[3].Score, matches[3].OtherScore, matches[3].Result, matches[3].OtherTeamID),
                Is.EqualTo(((int?)1, (int?)2, (SeasonMatchResult?)SeasonMatchResult.Loss, (int?)c.TeamID)));
            Assert.That(matches[4].Result, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(matches[4].Score, Is.Null);
        });
    }

    [Test]
    public async Task PlayoffBye_CountsAsAWin_AndListsTheByeAsTheOpponent()
    {
        var (season, a, b, c, _, _, _) = await Seed();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context), new GrifballWebApp.Server.Championships.ChampionshipService(_context));
        // Three teams: W1 = seed 1 (B) vs BYE, W2 = seed 3 (C) vs seed 2 (A)
        await bracketService.CreateBracket(3, season.SeasonID, false, 1);
        await bracketService.SetSeeds(season.SeasonID, new CustomSeedDto[]
        {
            new() { Seed = 1, TeamID = b.TeamID },
            new() { Seed = 2, TeamID = a.TeamID },
            new() { Seed = 3, TeamID = c.TeamID },
        });
        _context.ChangeTracker.Clear();

        var team = await _controller.Team(b.TeamID, CancellationToken.None);
        var bye = (await _controller.Matches(b.TeamID, CancellationToken.None))
            .Single(m => m.OtherResult == SeasonMatchResult.Bye);

        Assert.Multiple(() =>
        {
            // Seed() gives B one win; the BYE adds another, the final (vs the winner of W2) is still to play
            Assert.That((team!.Wins, team.TBD), Is.EqualTo((2, 3)));
            Assert.That((bye.Result, bye.OtherTeamID, bye.OtherTeamName, bye.Score), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Won, (int?)null, (string?)null, (int?)null)));
        });
    }

    [Test]
    public async Task Team_OfAHistoricalSeason_HasItsSpreadsheetsRecord_Titles_Captain_AndStats()
    {
        var season = Season.Seed(seasonName: "Summer League 2010", historicalSourceID: "wb/sml10", historicalSourceUrl: "https://docs.google.com/spreadsheets/d/x").WithDates(seasonStart: new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2010, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var captain = User.Seed(userName: "historical:Captain R", displayName: "Captain R", isDummyUser: true);
        var runner = await _context.AddUser("runner", gamertag: "Runner");
        _context.Users.Add(captain);
        await _context.SaveChangesAsync();
        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Relentless", recordedWins: 9, recordedLosses: 1);
        team.AddPlayer(runner.Id);
        team.AddPlayer(captain.Id);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        team.Captained(captain.Id);
        _context.PlayerSeasonStats.AddRange(
            PlayerSeasonStat.FromSheet(season.SeasonID, runner.Id, 1, new StatSheetTotals { Gamertag = "Runner Old", Goals = 3 }, teamID: team.TeamID, isPlayoffs: true),
            PlayerSeasonStat.FromSheet(season.SeasonID, captain.Id, 1, new StatSheetTotals { Gamertag = "Captain R", Goals = 9 }, teamID: team.TeamID, tradeNote: "From Hens"));
        await _context.SaveChangesAsync();
        await _context.SeedChampionsAsync(season.SeasonID, "Pro", team.TeamID, null, 1);
        _context.ChangeTracker.Clear();

        var dto = (await _controller.Team(team.TeamID, CancellationToken.None))!;
        var stats = await new GrifballWebApp.Server.Seasons.SeasonService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context)).GetSeasonPlayerStats(season.SeasonID, team.TeamID);

        Assert.Multiple(() =>
        {
            Assert.That((dto.Season.SeasonID, dto.Season.IsHistorical, dto.Season.HistoricalSourceUrl), Is.EqualTo((season.SeasonID, true, "https://docs.google.com/spreadsheets/d/x")));
            Assert.That((dto.Wins, dto.Losses, dto.TBD, dto.RecordFromSource), Is.EqualTo((9, 1, 0, true)));
            Assert.That(dto.Titles, Is.EqualTo(new[] { "Pro" }));
            Assert.That(dto.Players.Select(p => (p.Gamertag, p.IsCaptain)), Is.EqualTo(new[] { ("Captain R", true), ("Runner", false) }),
                "The captain first; a placeholder by the name the sheet gave");
            Assert.That(stats.Select(s => (s.Gamertag, s.IsPlayoffs, s.Goals, s.TradeNote)), Is.EqualTo(new[] { ("Captain R", false, 9, (string?)"From Hens"), ("Runner Old", true, 3, (string?)null) }),
                "The team's players' stats, most goals first");
        });
    }

    [Test]
    public async Task Team_OfTheAppsOwnSeason_HasNoTitlesOrStats_AndItsRecordFromMatches()
    {
        var (_, a, _, _, _, _, _) = await Seed();

        var dto = (await _controller.Team(a.TeamID, CancellationToken.None))!;

        Assert.That((dto.RecordFromSource, dto.Season.IsHistorical, dto.Titles.Length, dto.Players[0].IsCaptain), Is.EqualTo((false, false, 0, true)));
    }

    [Test]
    public async Task ARecord_CountsTheSameOnTheTeamPageAndInTheStandings_ByeIsNoLoss()
    {
        var season = await _context.AddSeason();
        var a = await _context.AddTeam(season, "Alpha", await _context.AddUser("ca"), 1);
        var b = await _context.AddTeam(season, "Bravo", await _context.AddUser("cb"), 2);
        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(season.SeasonID, a.TeamID, b.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(season.SeasonID, b.TeamID, a.TeamID, bestOf: 1).Forfeited(Forfeiter.Away),
            SeasonMatch.Seed(season.SeasonID, null, a.TeamID, bestOf: 1).WithBye(SeasonMatchSide.Home));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var page = (await _controller.Team(a.TeamID, CancellationToken.None))!;
        var standing = (await new TeamStandingsService(_context).GetTeamStandings(season.SeasonID)).Single(s => s.TeamID == a.TeamID);

        Assert.That((page.Wins, page.Losses), Is.EqualTo((1, 1)), "A forfeit is a loss, a BYE neither");
        Assert.That((standing.Wins, standing.Losses), Is.EqualTo((page.Wins, page.Losses)));
    }
}
