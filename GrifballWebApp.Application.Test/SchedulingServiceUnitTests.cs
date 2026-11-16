using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// A season's schedule checked and suggested (<see cref="SchedulingService"/>): a match's time against the rules
/// (<see cref="ScheduleChecker"/>), and the season's timeslots ranked for a match, best first.
/// </summary>
[TestFixture]
internal sealed class SchedulingServiceUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;

    // June 2026 (EDT, UTC-4); "now" is Monday 1 June, noon UTC
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    // Monday 1 June 20:00 Eastern, Tuesday 2 June 20:00 Eastern
    private static readonly DateTime Monday = new(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tuesday = new(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _time = new(Now);
    private Team _reds = null!, _blues = null!, _greens = null!;
    private SeasonMatch _match = null!, _other = null!;

    /// <summary>
    /// A season of two weeks (1 to 14 June), playing Mondays and Tuesdays at 20:00. Reds' four players can all play Mondays,
    /// two of them Tuesdays; Blues' four can play both. Reds vs Blues is unscheduled; Blues vs Greens is on the first Tuesday.
    /// </summary>
    private async Task Seed()
    {
        var season = Season.Seed(seasonName: "S1", seasonID: SeasonID);
        season.Schedule(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.Add(season);
        AvailabilityOption mondays = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(20, 0)),
            tuesdays = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Tuesday, time: new TimeOnly(20, 0));
        Context.Availability.AddRange(mondays, tuesdays);
        var users = Enumerable.Range(1, 9).Select(i => User.Register($"p{i}")).ToList();
        Context.Users.AddRange(users);
        await Context.SaveChangesAsync();
        Context.AddRange(SeasonAvailability.Seed(SeasonID, mondays.AvailabilityOptionID), SeasonAvailability.Seed(SeasonID, tuesdays.AvailabilityOptionID));

        _reds = Roster("Reds", users[0..4]);
        _blues = Roster("Blues", users[4..8]);
        _greens = Roster("Greens", users[8..9]);
        int[] both = [mondays.AvailabilityOptionID, tuesdays.AvailabilityOptionID], monday = [mondays.AvailabilityOptionID];
        Context.SeasonSignups.AddRange(users.Select((u, i) => SeasonSignup.Seed(SeasonID, u.Id, timeslots: i switch { < 2 => both, < 4 => monday, _ => both })));
        await Context.SaveChangesAsync();
        _match = SeasonMatch.Between(SeasonID, _reds.TeamID, _blues.TeamID, bestOf: 3);
        _other = SeasonMatch.Between(SeasonID, _blues.TeamID, _greens.TeamID, bestOf: 3);
        _other.ScheduleAt(Tuesday);
        Context.SeasonMatches.AddRange(_match, _other);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private Team Roster(string name, IEnumerable<User> players)
    {
        var list = players.ToList();
        var team = Team.Drafted(SeasonID, name, list[0].Id, 1);
        foreach (var player in list.Skip(1))
            team.Draft(player.Id);
        Context.Teams.Add(team);
        return team;
    }

    private SchedulingService Service() => new(Context, _time);

    private static DateTime[] Times(params DateTime[] times) => times;

    private static ScheduleConflictKind[] Kinds(params ScheduleConflictKind[] kinds) => kinds;

    [Test]
    public async Task CheckAsync_ATimeAtWhichBlueAlreadyPlays_IsRefused_AndAFreeOneIsFine()
    {
        await Seed();

        var clash = (await Service().CheckAsync(_match.SeasonMatchID, Tuesday))!;
        var free = await Service().CheckAsync(_match.SeasonMatchID, Monday);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clash.Select(c => c.Kind), Does.Contain(ScheduleConflictKind.TeamPlaysThen));
            Assert.That(clash.Any(c => c.Refuses), Is.True);
            Assert.That(free, Is.Empty);
            Assert.That(await Service().CheckAsync(999, Monday), Is.Null, "No such match");
        }
    }

    [Test]
    public async Task SuggestAsync_TheSeasonsSlotsFromNow_Best_First_TheRefusedLeftOut()
    {
        await Seed();

        var suggestions = (await Service().SuggestAsync(_match.SeasonMatchID))!;

        using (Assert.EnterMultipleScope())
        {
            // Mondays first (everyone can play), soonest first; the first Tuesday is refused (Blues play); the second Tuesday last (Reds short)
            Assert.That(suggestions.Select(s => s.StartsAt), Is.EqualTo(Times(Monday, Monday.AddDays(7), Tuesday.AddDays(7))));
            Assert.That(suggestions[0].Conflicts, Is.Empty);
            Assert.That((suggestions[0].HomePlayersAvailable, suggestions[0].AwayPlayersAvailable), Is.EqualTo((4, 4)));
            Assert.That(suggestions[2].Conflicts.Select(c => c.Kind), Is.EqualTo(Kinds(ScheduleConflictKind.HomeTeamUnavailable)), "Two of Reds' four: short of the season's four");
            Assert.That(suggestions[2].HomePlayersAvailable, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task SuggestAsync_FromAndTo_AndHowMany_NarrowIt_AndThePastIsLeftOut()
    {
        await Seed();
        _time.SetUtcNow(Monday.AddHours(1));

        var later = await Service().SuggestAsync(_match.SeasonMatchID, count: 1);
        var window = await Service().SuggestAsync(_match.SeasonMatchID, from: Monday.AddDays(6), to: Monday.AddDays(8));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(later!.Select(s => s.StartsAt), Is.EqualTo(Times(Monday.AddDays(7))), "This Monday's has begun");
            Assert.That(window!.Select(s => s.StartsAt), Is.EqualTo(Times(Monday.AddDays(7))));
            Assert.That(await Service().SuggestAsync(999), Is.Null);
        }
    }
}
