using GeneticSharp;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using NSubstitute;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Scheduling;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The season scheduler's fitness (<see cref="SeasonScheduleEvaluator"/>): each match's time checked against the season's rules
/// (<see cref="ScheduleChecker"/>), a refused conflict (a team playing then) weighing <see cref="SeasonScheduleEvaluator.RefusedWeight"/>, a
/// warning 1; 1 is a schedule with none.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonScheduleEvaluatorTests
{
    // League time: Monday 1 January 2024, 20:00, and the Tuesday after
    private static readonly DateTime Monday20 = new(2024, 1, 1, 20, 0, 0);
    private static readonly DateTime Tuesday20 = Monday20.AddDays(1);
    private static readonly AvailabilityOption[] Options = AvailabilityTestData.EveryDay(20, 21);
    private static readonly int[] TeamsOneAndTwo = [1, 2];

    private static SeasonScheduleChromosome WithGenes(params SeasonMatchGene[] genes)
    {
        var matches = genes.Select(g => SeasonMatch.Seed(0, g.HomeTeamID, g.AwayTeamID).WithId(g.SeasonMatchID)).ToList();
        var chromosome = new SeasonScheduleChromosome(matches, matches.Count, Monday20.Date, Monday20.Date.AddDays(7), Options);
        for (var i = 0; i < genes.Length; i++)
            chromosome.ReplaceGene(i, new Gene(genes[i]));
        return chromosome;
    }

    private static SeasonMatchGene Gene(int id, int home, int away, DateTime at) =>
        new() { SeasonMatchID = id, HomeTeamID = home, AwayTeamID = away, ScheduledTime = at };

    /// <summary>The season's rules (the defaults) over its every-day 20:00 and 21:00 timeslots, <paramref name="available"/> able to play them all.</summary>
    private static ScheduleChecker Checker(params int[] available)
    {
        (DayOfWeek, TimeOnly)[] slots = [.. Options.Select(o => (o.DayOfWeek, o.Time))];
        return new ScheduleChecker(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonSchedulingRules.Defaults(1), slots,
            available.ToDictionary(t => t, _ => (IReadOnlySet<(DayOfWeek Day, TimeOnly Time)>)slots.ToHashSet()), ScheduleService.LeagueTimeZone());
    }

    private static SeasonScheduleEvaluator Evaluator(params int[] available) => new(Checker(available), new Dictionary<int, int>());

    [Test]
    public void Evaluate_ASchedule_WithNoConflict_IsOne()
        => Assert.That(Evaluator(1, 2, 3, 4).Evaluate(WithGenes(Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20), Gene(3, 1, 3, Tuesday20))), Is.EqualTo(1));

    [Test]
    public void Evaluate_AWarning_IsHalf()
    {
        // Team 4 can't play then
        var chromosome = WithGenes(Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20));

        Assert.That(Evaluator(1, 2, 3).Evaluate(chromosome), Is.EqualTo(1d / 2).Within(1e-9));
    }

    [Test]
    public void Evaluate_ATeamPlayingTwiceAtOnce_WeighsMoreThanAWarning()
    {
        // Team 2 in both at once: refused, counted from each match; and two matches of its one night, from each
        var clash = WithGenes(Gene(1, 1, 2, Monday20), Gene(2, 2, 3, Monday20));
        var warned = WithGenes(Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20));
        var evaluator = Evaluator(1, 2, 3, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluator.Evaluate(clash), Is.EqualTo(1d / (1 + 2 * SeasonScheduleEvaluator.RefusedWeight + 2)).Within(1e-9));
            Assert.That(evaluator.Evaluate(clash), Is.LessThan(Evaluator(1, 2, 3).Evaluate(warned)));
        }
    }

    [Test]
    public void Evaluate_AMatchAlreadyFixed_CountsAgainstTheOnesPlaced()
    {
        // Team 1's match at Monday 20:00 is fixed (decided, or a bracket's): placing another of team 1's then clashes with it
        var fixedMatch = new ScheduledMatch(99, 1, 5, ScheduleService.ToUtc(Monday20), ScheduleChecker.LengthOf(1));
        var evaluator = new SeasonScheduleEvaluator(Checker(TeamsOneAndTwo), new Dictionary<int, int>(), [fixedMatch]);

        Assert.That(evaluator.Evaluate(WithGenes(Gene(1, 1, 2, Monday20))), Is.LessThan(1));
    }

    [Test]
    public void Evaluate_DuplicateMatchID_ScoresZero_WorseThanAnyCompleteSchedule()
    {
        // Same SeasonMatchID twice (as GeneticSharp's OrderedCrossover can produce): a corrupted, incomplete schedule scores
        // zero, so it can never outscore a complete schedule however many conflicts that has
        var duplicated = WithGenes(Gene(1, 1, 2, Monday20), Gene(1, 3, 4, Tuesday20));
        var worstComplete = WithGenes(Gene(1, 1, 2, Monday20), Gene(2, 2, 3, Monday20));
        var evaluator = Evaluator();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluator.Evaluate(duplicated), Is.Zero);
            Assert.That(evaluator.Evaluate(worstComplete), Is.GreaterThan(evaluator.Evaluate(duplicated)));
        }
    }

    [Test]
    public void Evaluate_OtherChromosome_Throws()
    {
        var other = Substitute.For<IChromosome>();
        var ex = Assert.Throws<Exception>(() => Evaluator().Evaluate(other));
        Assert.That(ex!.Message, Is.EqualTo("SeasonScheduleEvaluator can only evaluate SeasonScheduleChromosome"));
    }
}
