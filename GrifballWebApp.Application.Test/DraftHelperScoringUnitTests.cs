using GrifballWebApp.Application.Draft;
using GrifballWebApp.Application.Scheduling;

namespace GrifballWebApp.Application.Test;

/// <summary>The draft helper's fixed score: how a player fits a team's week, and how they have played against the pool.</summary>
[TestFixture]
public class DraftHelperScoringUnitTests
{
    private const int Monday = 1, Tuesday = 2;

    private static DraftPoolPlayer Player(int id, int[] slots, DraftCareer? career = null)
        => new(id, $"P{id}", null, false, [.. slots.Select(s => new DraftTimeslot(s, DayOfWeek.Monday, new TimeOnly(20, 0)))], career);

    private static TeamSlot Slot(int id, bool available, int players) => new(id, DayOfWeek.Monday, new TimeOnly(20, 0), available, players);

    [Test]
    public void Fit_ForATeamWhoseCaptainChoseItsTimeslots_CountsOnlyTheirsShortOfPlayers()
    {
        // The captain plays Mondays only; the roster is short both nights, but Tuesday is not the team's
        var team = new TeamSlots(1, SetByCaptain: true, RosterSize: 2, [Slot(Monday, true, 2), Slot(Tuesday, false, 1)]);

        var (mondays, why) = DraftHelperService.Fit(Player(1, [Monday]), team, minPlayers: 4);
        var (tuesdays, _) = DraftHelperService.Fit(Player(2, [Tuesday]), team, minPlayers: 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((mondays, why), Is.EqualTo((1.0, "Can play 1 of the 1 timeslots the team is short of")));
            Assert.That(tuesdays, Is.Zero, "Not a timeslot the team plays");
        }
    }

    [Test]
    public void Performance_AStatNotRecorded_IsAnUnknown_NotTheWorst()
    {
        // B's 5 games were all decided by vote: no goals or kills a game, which is not the pool's worst
        DraftCareer career(decimal win, decimal? goals, decimal? kills) => new(1, 10, win, goals, kills, 0);
        var pool = new[]
        {
            Player(1, [], career(50, 1, 10)),
            Player(2, [], career(50, null, null)),
            Player(3, [], career(50, 3, 30)),
        };

        var performance = DraftHelperService.Performance(pool);

        Assert.That(performance[2].Value, Is.EqualTo(0.5).Within(1e-9));
    }
}
