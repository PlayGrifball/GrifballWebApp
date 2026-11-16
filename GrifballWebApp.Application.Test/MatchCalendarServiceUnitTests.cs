using GrifballWebApp.Application.Calendar;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// MatchCalendarService's feeds of a team, a player and a season: each scheduled match an event, at its time for a slot's
/// length, under a UID that stays the match's when it moves; and only what a signed-out visitor may see (#435), a season
/// not announced yet never.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
internal sealed class MatchCalendarServiceUnitTests : ApplicationDbContextTests
{
    private const string BaseUrl = "https://grif.test";
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Modified = new(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private MatchCalendarService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new MatchCalendarService(Context, new SeasonVisibility(Context, _time), _time);
    }

    /// <summary>A season that started a month ago: public.</summary>
    private async Task<Season> PublicSeason(string name = "Season 5")
    {
        var season = Season.Seed(seasonName: name).WithDates(signupsOpen: Now.AddDays(-60), signupsClose: Now.AddDays(-50),
            draftStart: Now.AddDays(-40), seasonStart: Now.AddDays(-30), seasonEnd: Now.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season;
    }

    /// <summary>A season announced in 5 days and starting in 30: staff's alone for now.</summary>
    private async Task<Season> HiddenSeason(string name = "Secret Season")
    {
        var season = Season.Seed(seasonName: name).WithDates(signupsOpen: Now.AddDays(10), signupsClose: Now.AddDays(20),
            draftStart: Now.AddDays(25), seasonStart: Now.AddDays(30), seasonEnd: Now.AddDays(90), publicAt: Now.AddDays(5));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season;
    }

    private async Task<Team> TeamOf(Season season, string name)
    {
        var team = Team.Seed(name, seasonID: season.SeasonID);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        return team;
    }

    private async Task<SeasonMatch> MatchOf(Season season, Team? home, Team? away, DateTime? at, int bestOf = 3, DateTime? modified = null)
    {
        var match = SeasonMatch.Seed(season.SeasonID, home?.TeamID, away?.TeamID, bestOf, at);
        ((IAuditStamp)match).ModifiedAt = modified ?? Modified;
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        return match;
    }

    private async Task<User> Player(string? gamertag, params Team[] teams)
    {
        var user = gamertag is null ? User.Seed() : User.Seed(xboxUser: XboxUser.FromXbox(gamertag.Length, gamertag));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        foreach (var team in teams)
            team.AddPlayer(user.Id);
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>The feed's lines as a calendar app reads them, folded lines joined again.</summary>
    private static string[] Lines(string ics) => ics.Replace("\r\n ", "").Split("\r\n");

    /// <summary>Each VEVENT's properties, by name.</summary>
    private static List<Dictionary<string, string>> Events(string ics)
    {
        var events = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (var line in Lines(ics))
        {
            if (line == "BEGIN:VEVENT")
                current = [];
            else if (line == "END:VEVENT")
            {
                events.Add(current!);
                current = null;
            }
            else if (current is not null)
                current[line[..line.IndexOf(':')]] = line[(line.IndexOf(':') + 1)..];
        }
        return events;
    }

    private static string CalendarName(string ics) => Lines(ics).Single(l => l.StartsWith("X-WR-CALNAME:", StringComparison.Ordinal))["X-WR-CALNAME:".Length..];

    [Test]
    public async Task TeamAsync_NoSuchTeam_IsNull()
    {
        Assert.That(await _service.TeamAsync(999, BaseUrl), Is.Null);
    }

    [Test]
    public async Task TeamAsync_ATeamOfASeasonNotAnnouncedYet_IsNull_AsOneThatDoesntExist()
    {
        var season = await HiddenSeason();
        var red = await TeamOf(season, "Red");
        await MatchOf(season, red, await TeamOf(season, "Blue"), Now.AddDays(31));

        Assert.That(await _service.TeamAsync(red.TeamID, BaseUrl), Is.Null);
    }

    [Test]
    public async Task TeamAsync_HasEachScheduledMatchOfTheTeam_HomeOrAway_AsAnEvent()
    {
        var season = await PublicSeason();
        var red = await TeamOf(season, "Red");
        var blue = await TeamOf(season, "Blue");
        var green = await TeamOf(season, "Green");
        var home = await MatchOf(season, red, blue, Now.AddDays(1), bestOf: 3);
        var away = await MatchOf(season, green, red, Now.AddDays(2), bestOf: 5);
        await MatchOf(season, blue, green, Now.AddDays(3));

        var ics = await _service.TeamAsync(red.TeamID, BaseUrl);

        var events = Events(ics!);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CalendarName(ics!), Is.EqualTo("Red (Season 5)"));
            Assert.That(events.Select(e => e["UID"]), Is.EqualTo(new[] { MatchCalendarService.Uid(home.SeasonMatchID), MatchCalendarService.Uid(away.SeasonMatchID) }));
            Assert.That(events[0]["SUMMARY"], Is.EqualTo("Red vs Blue (Season 5)"));
            Assert.That(events[0]["DTSTART"], Is.EqualTo("20260602T000000Z"));
            Assert.That(events[0]["DTEND"], Is.EqualTo("20260602T003000Z"), "a slot's length after its start");
            Assert.That(events[0]["DTSTAMP"], Is.EqualTo("20260520T080000Z"));
            Assert.That(events[0]["LAST-MODIFIED"], Is.EqualTo("20260520T080000Z"));
            Assert.That(events[0]["URL"], Is.EqualTo($"{BaseUrl}/seasonmatch/{home.SeasonMatchID}"));
            Assert.That(events[0]["DESCRIPTION"], Is.EqualTo($@"Season 5\, best of 3\n{BaseUrl}/seasonmatch/{home.SeasonMatchID}"));
            Assert.That(events[1]["SUMMARY"], Is.EqualTo("Green vs Red (Season 5)"));
            Assert.That(events[1]["DESCRIPTION"], Does.StartWith(@"Season 5\, best of 5\n"));
        }
    }

    [Test]
    public async Task TeamAsync_LeavesOutAnUnscheduledMatch()
    {
        var season = await PublicSeason();
        var red = await TeamOf(season, "Red");
        await MatchOf(season, red, await TeamOf(season, "Blue"), at: null);

        var ics = await _service.TeamAsync(red.TeamID, BaseUrl);

        Assert.That(Events(ics!), Is.Empty);
    }

    [Test]
    public async Task TeamAsync_ListsTheMatchesSoonestFirst()
    {
        var season = await PublicSeason();
        var red = await TeamOf(season, "Red");
        var blue = await TeamOf(season, "Blue");
        var later = await MatchOf(season, red, blue, Now.AddDays(9));
        var sooner = await MatchOf(season, blue, red, Now.AddDays(-2));

        var ics = await _service.TeamAsync(red.TeamID, BaseUrl);

        Assert.That(Events(ics!).Select(e => e["UID"]), Is.EqualTo(new[] { MatchCalendarService.Uid(sooner.SeasonMatchID), MatchCalendarService.Uid(later.SeasonMatchID) }));
    }

    [Test]
    public async Task TeamAsync_ARescheduledMatch_KeepsItsUid_WithItsNewTimeAndLastModified()
    {
        var season = await PublicSeason();
        var red = await TeamOf(season, "Red");
        var match = await MatchOf(season, red, await TeamOf(season, "Blue"), Now.AddDays(1));
        var before = Events((await _service.TeamAsync(red.TeamID, BaseUrl))!).Single();

        match.ScheduleAt(Now.AddDays(4).AddHours(2));
        ((IAuditStamp)match).ModifiedAt = Now;
        await Context.SaveChangesAsync();
        var after = Events((await _service.TeamAsync(red.TeamID, BaseUrl))!).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after["UID"], Is.EqualTo(before["UID"]));
            Assert.That(before["DTSTART"], Is.EqualTo("20260602T000000Z"));
            Assert.That(after["DTSTART"], Is.EqualTo("20260605T020000Z"));
            Assert.That(after["DTEND"], Is.EqualTo("20260605T023000Z"));
            Assert.That(after["LAST-MODIFIED"], Is.EqualTo("20260601T000000Z"));
            Assert.That(after["DTSTAMP"], Is.EqualTo("20260601T000000Z"));
        }
    }

    [Test]
    public async Task TeamAsync_AMatchNeverStamped_IsStampedNow()
    {
        var season = await PublicSeason();
        var red = await TeamOf(season, "Red");
        await MatchOf(season, red, await TeamOf(season, "Blue"), Now.AddDays(1), modified: default(DateTime));

        var ics = await _service.TeamAsync(red.TeamID, BaseUrl);

        Assert.That(Events(ics!).Single()["DTSTAMP"], Is.EqualTo("20260601T000000Z"));
    }

    [Test]
    public async Task PlayerAsync_NoSuchUser_IsNull()
    {
        Assert.That(await _service.PlayerAsync(999, BaseUrl), Is.Null);
    }

    [Test]
    public async Task PlayerAsync_HasTheMatchesOfEachOfThePlayersTeams_ButNotOfASeasonNotAnnouncedYet()
    {
        var past = await PublicSeason("Season 4");
        var current = await PublicSeason("Season 5");
        var secret = await HiddenSeason();
        var oldTeam = await TeamOf(past, "Old Red");
        var red = await TeamOf(current, "Red");
        var hidden = await TeamOf(secret, "Hidden");
        var oldMatch = await MatchOf(past, oldTeam, await TeamOf(past, "Old Blue"), Now.AddDays(-45));
        var match = await MatchOf(current, await TeamOf(current, "Blue"), red, Now.AddDays(1));
        await MatchOf(secret, hidden, await TeamOf(secret, "Hidden Blue"), Now.AddDays(31));
        await MatchOf(current, await TeamOf(current, "Green"), await TeamOf(current, "Gold"), Now.AddDays(2));
        var player = await Player("Grif", oldTeam, red, hidden);

        var ics = await _service.PlayerAsync(player.Id, BaseUrl);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CalendarName(ics!), Is.EqualTo("Grif's matches"));
            Assert.That(Events(ics!).Select(e => e["UID"]), Is.EqualTo(new[] { MatchCalendarService.Uid(oldMatch.SeasonMatchID), MatchCalendarService.Uid(match.SeasonMatchID) }));
            Assert.That(ics, Does.Not.Contain("Secret Season").And.Not.Contain("Hidden"));
        }
    }

    [Test]
    public async Task PlayerAsync_APlayerOnNoTeam_HasAnEmptyCalendar()
    {
        var player = await Player("Simmons");

        var ics = await _service.PlayerAsync(player.Id, BaseUrl);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ics, Does.StartWith("BEGIN:VCALENDAR\r\n"));
            Assert.That(Events(ics!), Is.Empty);
        }
    }

    [Test]
    public async Task PlayerAsync_APlayerWithNoNameShown_HasACalendarNamedForTheLeague()
    {
        var player = await Player(gamertag: null);

        var ics = await _service.PlayerAsync(player.Id, BaseUrl);

        Assert.That(CalendarName(ics!), Is.EqualTo("Grifball matches"));
    }

    [Test]
    public async Task SeasonAsync_NoSuchSeason_IsNull()
    {
        Assert.That(await _service.SeasonAsync(999, BaseUrl), Is.Null);
    }

    [Test]
    public async Task SeasonAsync_ASeasonNotAnnouncedYet_IsNull_AsOneThatDoesntExist()
    {
        var season = await HiddenSeason();
        await MatchOf(season, await TeamOf(season, "Red"), await TeamOf(season, "Blue"), Now.AddDays(31));

        Assert.That(await _service.SeasonAsync(season.SeasonID, BaseUrl), Is.Null);
    }

    [Test]
    public async Task SeasonAsync_HasEveryScheduledMatch_ABracketSlotsTeamToCome_ButNoByes()
    {
        var season = await PublicSeason();
        var other = await PublicSeason("Season 6");
        var red = await TeamOf(season, "Red");
        var blue = await TeamOf(season, "Blue");
        var regular = await MatchOf(season, red, blue, Now.AddDays(1));
        var waiting = await MatchOf(season, red, away: null, Now.AddDays(8));
        var bye = await MatchOf(season, blue, away: null, Now.AddDays(8));
        bye.PlaceBye(SeasonMatchSide.Away);
        await Context.SaveChangesAsync();
        await MatchOf(season, red, blue, at: null);
        await MatchOf(other, await TeamOf(other, "Green"), await TeamOf(other, "Gold"), Now.AddDays(1));

        var ics = await _service.SeasonAsync(season.SeasonID, BaseUrl);

        var events = Events(ics!);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CalendarName(ics!), Is.EqualTo("Season 5 schedule"));
            Assert.That(events.Select(e => e["UID"]), Is.EqualTo(new[] { MatchCalendarService.Uid(regular.SeasonMatchID), MatchCalendarService.Uid(waiting.SeasonMatchID) }));
            Assert.That(events[1]["SUMMARY"], Is.EqualTo("Red vs TBD (Season 5)"));
        }
    }

    [Test]
    public async Task SeasonAsync_ABracketSlotWhoseHomeTeamIsToCome_IsTbdAtHome()
    {
        var season = await PublicSeason();
        await MatchOf(season, home: null, await TeamOf(season, "Blue"), Now.AddDays(8));

        var ics = await _service.SeasonAsync(season.SeasonID, BaseUrl);

        Assert.That(Events(ics!).Single()["SUMMARY"], Is.EqualTo("TBD vs Blue (Season 5)"));
    }

    [Test]
    public async Task TeamAsync_WithoutAClockGiven_StampsAnUnstampedMatchWithTheSystemClock()
    {
        var service = new MatchCalendarService(Context, new SeasonVisibility(Context));
        var season = Season.Seed(seasonName: "Now").WithDates(signupsOpen: DateTime.UtcNow.AddDays(-60), signupsClose: DateTime.UtcNow.AddDays(-50),
            draftStart: DateTime.UtcNow.AddDays(-40), seasonStart: DateTime.UtcNow.AddDays(-30), seasonEnd: DateTime.UtcNow.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var red = await TeamOf(season, "Red");
        await MatchOf(season, red, await TeamOf(season, "Blue"), DateTime.UtcNow.AddDays(1), modified: default(DateTime));
        var before = DateTime.UtcNow;

        var stamp = Events((await service.TeamAsync(red.TeamID, BaseUrl))!).Single()["DTSTAMP"];

        Assert.That(DateTime.ParseExact(stamp, "yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(before.AddSeconds(-1)));
    }

    [Test]
    public void Uid_IsTheSeasonMatchsOwn()
    {
        Assert.That(MatchCalendarService.Uid(42), Is.EqualTo("seasonmatch-42@grifballwebapp"));
    }
}
