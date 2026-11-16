using GrifballWebApp.Application.Calendar;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Calendar;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// CalendarController: each feed is open to anyone (a calendar app signs in to nothing), answers text/calendar, and 404
/// for a team, player or season there is none of, or one of a season not announced yet: staff signed in too, as a feed
/// only ever holds what a signed-out visitor may see.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CalendarControllerUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private CalendarController Controller()
    {
        return new CalendarController(new MatchCalendarService(Db, new SeasonVisibility(Db)));
    }

    /// <summary>The controller as a signed-out request to https://grif.test reaches it.</summary>
    private CalendarController Anonymous()
    {
        var controller = Controller().WithAnonymousUser();
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("grif.test");
        return controller;
    }

    private async Task<(Season season, Team red)> SeasonWithAMatch(bool announced)
    {
        var season = announced
            ? Season.Seed(seasonName: "Winter").WithDates(signupsOpen: Now.AddDays(-60), signupsClose: Now.AddDays(-50), draftStart: Now.AddDays(-40),
                seasonStart: Now.AddDays(-30), seasonEnd: Now.AddDays(30))
            : Season.Seed(seasonName: "Secret").WithDates(signupsOpen: Now.AddDays(35), signupsClose: Now.AddDays(45), draftStart: Now.AddDays(50),
                seasonStart: Now.AddDays(60), seasonEnd: Now.AddDays(120), publicAt: Now.AddDays(30));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var red = Team.Seed("Red", seasonID: season.SeasonID);
        var blue = Team.Seed("Blue", seasonID: season.SeasonID);
        Db.Teams.AddRange(red, blue);
        await Db.SaveChangesAsync();
        Db.SeasonMatches.Add(SeasonMatch.Seed(season.SeasonID, red.TeamID, blue.TeamID, scheduledTime: season.SeasonStart.AddDays(1)));
        await Db.SaveChangesAsync();
        return (season, red);
    }

    private static string Calendar(IActionResult result)
    {
        Assert.That(result, Is.InstanceOf<ContentResult>());
        var content = (ContentResult)result;
        Assert.That(content.ContentType, Is.EqualTo(IcsWriter.ContentType));
        return content.Content!;
    }

    [TestCase(nameof(CalendarController.TeamCalendar), "teams/{teamID:int}/calendar.ics")]
    [TestCase(nameof(CalendarController.PlayerCalendar), "users/{userID:int}/calendar.ics")]
    [TestCase(nameof(CalendarController.SeasonCalendar), "seasons/{seasonID:int}/calendar.ics")]
    public void AFeed_IsOpenToAnyone_AtItsIcsAddress(string action, string template)
    {
        var get = (Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)ControllerTestHelpers.Action(typeof(CalendarController), action)
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ActionAllowsAnonymous(typeof(CalendarController), action), Is.True);
            Assert.That(get.Template, Is.EqualTo(template));
        }
    }

    [Test]
    public async Task TeamCalendar_ATeamsMatches_AsTextCalendar()
    {
        var (_, red) = await SeasonWithAMatch(announced: true);

        var ics = Calendar(await Anonymous().TeamCalendar(red.TeamID, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ics, Does.StartWith("BEGIN:VCALENDAR\r\n"));
            Assert.That(ics, Does.Contain("SUMMARY:Red vs Blue (Winter)\r\n"));
            Assert.That(ics, Does.Contain("URL:https://grif.test/seasonmatch/"), "links to the site the feed was asked for from");
        }
    }

    [Test]
    public async Task TeamCalendar_NoSuchTeam_IsNotFound()
    {
        var result = await Anonymous().TeamCalendar(999, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, SeasonVisibilityFilter.NoSuchTeam);
    }

    [Test]
    public async Task TeamCalendar_ATeamOfASeasonNotAnnouncedYet_IsNotFound_EvenToStaff()
    {
        var (_, red) = await SeasonWithAMatch(announced: false);

        var result = await Controller().WithUser("1", "sysadmin", "Sysadmin").TeamCalendar(red.TeamID, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, SeasonVisibilityFilter.NoSuchTeam);
    }

    [Test]
    public async Task PlayerCalendar_APlayersMatches_AsTextCalendar()
    {
        var (_, red) = await SeasonWithAMatch(announced: true);
        var user = User.Seed(xboxUser: XboxUser.FromXbox(4, "Grif"));
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        red.AddPlayer(user.Id);
        await Db.SaveChangesAsync();

        var ics = Calendar(await Anonymous().PlayerCalendar(user.Id, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ics, Does.Contain("X-WR-CALNAME:Grif's matches\r\n"));
            Assert.That(ics, Does.Contain("SUMMARY:Red vs Blue (Winter)\r\n"));
        }
    }

    [Test]
    public async Task PlayerCalendar_NoSuchUser_IsNotFound()
    {
        var result = await Anonymous().PlayerCalendar(999, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, CalendarController.NoSuchUser);
    }

    [Test]
    public async Task SeasonCalendar_ASeasonsMatches_AsTextCalendar()
    {
        var (season, _) = await SeasonWithAMatch(announced: true);

        var ics = Calendar(await Anonymous().SeasonCalendar(season.SeasonID, CancellationToken.None));

        Assert.That(ics, Does.Contain("X-WR-CALNAME:Winter schedule\r\n"));
    }

    [Test]
    public async Task SeasonCalendar_ASeasonNotAnnouncedYet_IsNotFound_EvenToStaff()
    {
        var (season, _) = await SeasonWithAMatch(announced: false);

        var result = await Controller().WithUser("1", "sysadmin", "Sysadmin").SeasonCalendar(season.SeasonID, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, SeasonVisibilityFilter.NoSuchSeason);
    }
}
