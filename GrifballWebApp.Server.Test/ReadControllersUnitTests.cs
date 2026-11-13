using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.Home;
using GrifballWebApp.Server.LateLeague;
using GrifballWebApp.Server.Leaderboards;
using GrifballWebApp.Server.MatchDetails;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Search;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The site's read-only pages' controllers, each over its real service on a temp-file
/// SQLite GrifballContext with a fixed clock: what each answers for something there, and for something that isn't.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ReadControllersUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Autumn = ["Autumn"];

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>A season running now, with teams Home and Away and a match between them; the Home team's roster is <paramref name="player"/>.</summary>
    private async Task<(Season Season, Team Home, Team Away)> RunningSeason(int? player = null)
    {
        var season = Season.Seed(seasonName: "Winter").WithDates(signupsOpen: Now.AddDays(-60), signupsClose: Now.AddDays(-50), draftStart: Now.AddDays(-40),
            seasonStart: Now.AddDays(-30), seasonEnd: Now.AddDays(30));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var home = await AddTeam(season, "Home");
        var away = await AddTeam(season, "Away");
        await AddSeasonMatch(season, home, away);
        if (player is int userID)
        {
            Db.Set<TeamPlayer>().Add(TeamPlayer.Seed(teamID: home.TeamID, userID: userID));
            await Db.SaveChangesAsync();
        }
        return (season, home, away);
    }

    [Test]
    public async Task UserMerges_ListsNone_AndAnUnknownMergeIsNotFound()
    {
        var controller = new UserMergesController(new UserMergeAuditService(Db, _time));

        var list = await controller.List(new PaginationFilter { PageNumber = 1, PageSize = 10 }, CancellationToken.None);
        var missing = await controller.Get(999, CancellationToken.None);

        Assert.That(list.TotalCount, Is.Zero);
        missing.AssertProblem(StatusCodes.Status404NotFound, "There is no such merge");
    }

    [Test]
    public async Task DeploymentHistory_IsThePlayersTeams_AndAnUnknownUserIsNotFound()
    {
        var player = await AddUser(5, "Grifter1");
        var (_, home, _) = await RunningSeason(player.Id);
        var controller = new DeploymentHistoryController(new DeploymentHistoryService(Db, _time));

        var history = await controller.GetDeploymentHistory(player.Id, CancellationToken.None);
        var missing = await controller.GetDeploymentHistory(999, CancellationToken.None);

        Assert.That(history.Value!.Select(d => (d.TeamID, d.TeamName)), Is.EqualTo(new[] { (home.TeamID, "Home") }));
        missing.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
    }

    [Test]
    public async Task Availability_IsSetAndReadBack()
    {
        var (season, _, _) = await RunningSeason();
        var controller = new AvailabilityController(new AvailabilityService(Db));
        var picked = new SeasonAvailabilityDto { Timeslots = [new TimeslotDto { DayOfWeek = DayOfWeek.Tuesday, Time = new TimeOnly(20, 30) }] };

        var result = await controller.UpdateSeasonAvailability(season.SeasonID, picked, CancellationToken.None);
        Db.ChangeTracker.Clear();
        var timeslots = await controller.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(picked.SeasonID, Is.EqualTo(season.SeasonID), "The route's season, whatever the body says");
            Assert.That(timeslots.Select(t => (t.DayOfWeek, t.Time)), Is.EqualTo(new[] { (DayOfWeek.Tuesday, new TimeOnly(20, 30)) }));
        }
    }

    [Test]
    public async Task Team_ItsPageHistoryAndMatches_AndAnUnknownTeamIsNotFound()
    {
        var (_, home, _) = await RunningSeason();
        var controller = new TeamController(new TeamPageService(Db, _time), new TeamHistoryService(Db, _time));

        var team = await controller.Team(home.TeamID, CancellationToken.None);
        var history = await controller.History(home.TeamID, CancellationToken.None);
        var matches = await controller.Matches(home.TeamID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.Value!.TeamName, Is.EqualTo("Home"));
            Assert.That(history.Value!.TeamName, Is.EqualTo("Home"));
            Assert.That(matches, Has.Length.EqualTo(1));
        }
        (await controller.Team(999, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound, "There is no such team");
        (await controller.History(999, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound, "There is no such team");
    }

    [Test]
    public async Task MatchDetails_AGameNotPulled_IsNotFound()
    {
        var controller = new MatchDetailsController(new MatchDetailsService(Db));

        var result = await controller.GetMatchDetails(Guid.NewGuid(), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, "That match has not been pulled");
    }

    [Test]
    public async Task Leaderboards_BoardsRankAndPlayerSearch()
    {
        var player = await AddUser(5, "Grifter1");
        var controller = new LeaderboardsController(new LeaderboardsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db),
            Substitute.For<IDistributedCache>(), new RecordingLogger<LeaderboardsService>()));

        var boards = await controller.GetLeaderboards(null, CancellationToken.None);
        var rank = await controller.GetPlayerRank("Grifter1", null, null, CancellationToken.None);
        var found = await controller.SearchPlayers("Grif", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(boards.GoalLeaders, Is.Empty);
            Assert.That((rank.Value!.UserID, rank.Value.Gamertag), Is.EqualTo((player.Id, "Grifter1")));
            Assert.That(found.Select(p => p.UserID), Is.EqualTo(new[] { player.Id }));
        }
        (await controller.GetPlayerRank("Nobody", null, null, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound, "No player matches that");
    }

    [Test]
    public async Task LateLeague_QueueSeasonsAndASeason_AndAnUnknownSeasonIsNotFound()
    {
        var season = MatchmakingSeason.Seed("Autumn", new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Db.MatchmakingSeasons.Add(season);
        await Db.SaveChangesAsync();
        var controller = new LateLeagueController(new MatchmakingStatsService(Db));

        var queue = await controller.GetQueue(CancellationToken.None);
        var seasons = await controller.GetSeasons(CancellationToken.None);
        var one = await controller.GetSeason(season.MatchmakingSeasonID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((queue.GameCount, queue.Players.Count), Is.EqualTo((0, 0)));
            Assert.That(seasons.Select(s => s.Name), Is.EqualTo(Autumn));
            Assert.That(one.Value!.Season.Name, Is.EqualTo("Autumn"));
        }
        (await controller.GetSeason(999, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound, "There is no such Late League season");
    }

    [Test]
    public async Task SeasonPages_StandingsGradesAndEvents()
    {
        var (season, home, away) = await RunningSeason();

        var standings = await new TeamStandingsController(new TeamStandingsService(Db)).GetTeamStandings(season.SeasonID, CancellationToken.None);
        var grades = await new GradesController(new GradesService(Db)).GetGrades(season.SeasonID, CancellationToken.None);
        var events = await new HomeController(new HomeEventsService(Db, _time)).CurrentAndFutureEvents(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(standings.Select(s => s.TeamID), Is.EquivalentTo(new[] { home.TeamID, away.TeamID }));
            Assert.That(grades.Totals, Is.Empty);
            Assert.That(events.Select(e => (e.SeasonID, e.EventType)), Is.EqualTo(new[] { (season.SeasonID, EventType.Season) }));
        }
    }

    [Test]
    public async Task QuickSearch_FindsTheTeam()
    {
        var (season, home, _) = await RunningSeason();

        var found = await new SearchController(new QuickSearchService(Db, _time)).QuickSearch("Hom", CancellationToken.None);

        Assert.That(found.Teams.Select(t => (t.SeasonID, t.TeamID)), Is.EqualTo(new[] { (season.SeasonID, home.TeamID) }));
    }
}
