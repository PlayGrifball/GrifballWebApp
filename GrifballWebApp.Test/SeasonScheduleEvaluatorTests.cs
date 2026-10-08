using GeneticSharp;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class SeasonScheduleEvaluatorTests
{
    private static readonly DateTime Monday20 = new(2024, 1, 1, 20, 0, 0);

    private static SeasonScheduleChromosome WithGenes(AvailabilityOption[] options, params SeasonMatchGene[] genes)
    {
        var matches = genes.Select(g => new SeasonMatch { SeasonMatchID = g.SeasonMatchID, HomeTeamID = g.HomeTeamID, AwayTeamID = g.AwayTeamID }).ToList();
        var chromosome = new SeasonScheduleChromosome(matches, matches.Count, Monday20.Date, Monday20.Date.AddDays(7), options);
        for (var i = 0; i < genes.Length; i++)
            chromosome.ReplaceGene(i, new Gene(genes[i]));
        return chromosome;
    }

    private static SeasonMatchGene Gene(int id, int home, int away, DateTime at) =>
        new() { SeasonMatchID = id, HomeTeamID = home, AwayTeamID = away, ScheduledTime = at };

    private static AvailabilityOption[] OptionsAvailableFor(params int[] teamIDs)
    {
        var options = SeasonScheduleChromosomeTests.EveryDay(20, 21);
        foreach (var o in options)
            foreach (var t in teamIDs)
                o.TeamAvailability.Add(new TeamAvailability { TeamID = t });
        return options;
    }

    [Test]
    public void Evaluate_PerfectSchedule_IsOne()
    {
        var options = OptionsAvailableFor(1, 2, 3, 4);
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20), Gene(3, 1, 3, Monday20.AddHours(1)));

        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_ManyPenalties_DecreasesFitness()
    {
        // Overlap (counted from both genes) and nobody available: score = 1 - 2 - 4 = -5
        var options = OptionsAvailableFor();
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 2, 3, Monday20));

        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(1d / 5).Within(1e-9));
    }

    [Test]
    public void Evaluate_ExactlyOnePenalty_IsInfinite()
    {
        var options = OptionsAvailableFor(1, 2, 3);
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20)); // away team 4 unavailable

        // BUG: SeasonScheduleEvaluator returns |score|^-1 with score starting at 1, so one penalty gives 0^-1 = +Infinity
        // (fitter than a perfect schedule) and two penalties give |-1|^-1 = 1 (as fit as a perfect schedule).
        // Expected: fitness strictly decreases with penalties.
        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(double.PositiveInfinity));
    }

    [Test]
    public void Evaluate_TwoPenalties_EqualsPerfectFitness()
    {
        var options = OptionsAvailableFor(1, 2);
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20)); // teams 3 and 4 unavailable

        // BUG: see Evaluate_ExactlyOnePenalty_IsInfinite
        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_OtherChromosome_Throws()
    {
        var other = Substitute.For<IChromosome>();
        var ex = Assert.Throws<Exception>(() => new SeasonScheduleEvaluator([]).Evaluate(other));
        Assert.That(ex!.Message, Is.EqualTo("SeasonScheduleEvaluator can only evaluate SeasonScheduleChromosome"));
    }
}
