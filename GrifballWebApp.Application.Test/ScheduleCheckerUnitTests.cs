using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The one set of rules a match's time is checked against (<see cref="ScheduleChecker"/>), whoever sets it: the season
/// scheduler, staff by hand, a reschedule. Times are UTC; nights and weeks are the league's (US Eastern).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScheduleCheckerUnitTests
{
    private const int Home = 1, Away = 2, Third = 3, Fourth = 4;
    private static readonly TimeSpan Bo3 = ScheduleChecker.LengthOf(3);

    // The season: June 2026 (EDT, UTC-4), Mondays and Tuesdays at 20:00 and 21:00 league time
    private static readonly DateTime SeasonStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly (DayOfWeek, TimeOnly)[] Slots =
        [(DayOfWeek.Monday, new(20, 0)), (DayOfWeek.Monday, new(21, 0)), (DayOfWeek.Tuesday, new(20, 0)), (DayOfWeek.Tuesday, new(21, 0))];

    // Monday 8 June 2026, 20:00 and 21:00 Eastern; Tuesday 9 June 20:00
    private static readonly DateTime Monday8pm = new(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Monday9pm = new(2026, 6, 9, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tuesday8pm = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextMonday8pm = Monday8pm.AddDays(7);

    private static ScheduleChecker Checker(SeasonSchedulingRules? rules = null, Dictionary<int, (DayOfWeek, TimeOnly)[]>? available = null)
        => new(SeasonStart, SeasonEnd, rules ?? SeasonSchedulingRules.Defaults(1), Slots,
            (available ?? new() { [Home] = Slots, [Away] = Slots, [Third] = Slots, [Fourth] = Slots })
                .ToDictionary(t => t.Key, t => (IReadOnlySet<(DayOfWeek, TimeOnly)>)t.Value.ToHashSet()),
            ScheduleService.LeagueTimeZone());

    private static ScheduledMatch Match(int id, int home, int away, DateTime at) => new(id, home, away, at, Bo3);

    private static ScheduleConflictKind[] Only(params ScheduleConflictKind[] kinds) => kinds;

    private static IEnumerable<ScheduleConflictKind> Kinds(IEnumerable<ScheduleConflict> conflicts) => conflicts.Select(c => c.Kind);

    [Test]
    public void AMatchAtOneOfTheSeasonsSlots_BothTeamsFree_AndAvailable_HasNoConflict()
        => Assert.That(Checker().Check(Match(10, Home, Away, Monday8pm), []), Is.Empty);

    [Test]
    public void OutsideTheSeason_OrNotOneOfItsSlots_Conflicts()
    {
        var checker = Checker();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, SeasonEnd.AddDays(5)), [])), Does.Contain(ScheduleConflictKind.OutsideSeason));
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, Monday8pm.AddMinutes(30)), [])), Is.EqualTo(Only(ScheduleConflictKind.NotASeasonTimeslot)),
                "20:30 is no slot of the season's");
        }
    }

    [Test]
    public void ATeamPlayingThen_Conflicts_AndRefuses_ButNotAnotherPairsMatch()
    {
        var checker = Checker();
        var conflicts = checker.Check(Match(10, Home, Away, Monday8pm), [Match(11, Away, Third, Monday8pm.AddMinutes(30))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conflicts.Single(c => c.Kind == ScheduleConflictKind.TeamPlaysThen).Refuses, Is.True);
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, Monday8pm), [Match(11, Third, Fourth, Monday8pm)])), Is.Empty);
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, Monday8pm), [Match(10, Home, Away, Monday8pm)])), Is.Empty, "Not against itself");
        }
    }

    [Test]
    public void ATeamThatCantPlayTheSlot_Conflicts_AsAWarning()
    {
        var conflicts = Checker(available: new() { [Home] = Slots, [Away] = [(DayOfWeek.Tuesday, new(20, 0))] }).Check(Match(10, Home, Away, Monday8pm), []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Kinds(conflicts), Is.EqualTo(Only(ScheduleConflictKind.AwayTeamUnavailable)));
            Assert.That(conflicts.Single().Refuses, Is.False);
            Assert.That(Kinds(Checker(available: []).Check(Match(10, Home, Away, Monday8pm), [])),
                Is.EquivalentTo(Only(ScheduleConflictKind.HomeTeamUnavailable, ScheduleConflictKind.AwayTeamUnavailable)), "A team with no slots can play none");
        }
    }

    [Test]
    public void ATeamsSecondMatchInANight_OrThirdInAWeek_IsOverTheSeasonsCap()
    {
        var checker = Checker();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, Monday9pm), [Match(11, Home, Third, Monday8pm)])), Is.EqualTo(Only(ScheduleConflictKind.TooManyTonight)));
            // Monday 20:00 and 21:00 already (over a night's cap between them, but that's theirs): a third this week
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, Tuesday8pm.AddHours(1)), [Match(11, Home, Third, Monday8pm), Match(12, Fourth, Home, Monday9pm)])),
                Is.EqualTo(Only(ScheduleConflictKind.TooManyThisWeek)));
            Assert.That(Kinds(checker.Check(Match(10, Home, Away, NextMonday8pm), [Match(11, Home, Third, Monday8pm), Match(12, Home, Fourth, Tuesday8pm)])),
                Is.Empty, "Next week is another week");
        }
    }

    [Test]
    public void MoreMatchesAtOnceThanTheSeasonAllows_Conflicts_WhenItCapsThem()
    {
        var rules = SeasonSchedulingRules.Defaults(1);
        rules.Change(4, 1, 2, maxConcurrentMatches: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Kinds(Checker(rules).Check(Match(10, Home, Away, Monday8pm), [Match(11, Third, Fourth, Monday8pm)])), Is.EqualTo(Only(ScheduleConflictKind.TooManyAtOnce)));
            Assert.That(Kinds(Checker().Check(Match(10, Home, Away, Monday8pm), [Match(11, Third, Fourth, Monday8pm)])), Is.Empty, "No cap by default");
        }
    }

    [Test]
    public void LengthOf_ASeries_IsItsGames()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScheduleChecker.LengthOf(1), Is.EqualTo(TimeSpan.FromMinutes(ScheduleChecker.MinutesPerGame)));
            Assert.That(ScheduleChecker.LengthOf(5), Is.EqualTo(TimeSpan.FromMinutes(5 * ScheduleChecker.MinutesPerGame)));
        }
    }

    [Test]
    public void EveryConflict_SaysWhyInWords()
    {
        var rules = SeasonSchedulingRules.Defaults(1);
        rules.Change(4, 1, 1, maxConcurrentMatches: 1);
        var conflicts = Checker(rules, available: []).Check(Match(10, Home, Away, Monday8pm.AddMinutes(30)),
            [Match(11, Home, Third, Monday8pm), Match(12, Third, Fourth, Monday8pm.AddMinutes(30))]);

        Assert.That(conflicts.Select(c => c.Message), Has.All.Not.Empty.And.Count.GreaterThanOrEqualTo(5));
    }
}
