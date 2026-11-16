using GeneticSharp;
using GrifballWebApp.Application.Scheduling;

namespace GrifballWebApp.Application.Scheduler;

/// <summary>
/// The season scheduler's fitness: each placed match's time checked against the season's rules (<see cref="ScheduleChecker"/>),
/// with the other placed matches and the ones whose time is already fixed (<paramref name="fixedMatches"/>: decided, or a
/// bracket's). A refused conflict (a team playing then) weighs <see cref="RefusedWeight"/>, a warning 1. Fitness is 0 to 1, 1 a
/// schedule with none, falling as they add up.
/// </summary>
/// <param name="checker">The season's rules, timeslots and teams' availability.</param>
/// <param name="bestOf">Each match's series, by its ID, for its length (a missing one: a single game).</param>
/// <param name="fixedMatches">The season's matches with a time the scheduler doesn't place.</param>
public class SeasonScheduleEvaluator(ScheduleChecker checker, IReadOnlyDictionary<int, int> bestOf, IReadOnlyList<ScheduledMatch>? fixedMatches = null) : IFitness
{
    /// <summary>How much a refused conflict (a team playing two matches at once) weighs against a warning's 1.</summary>
    public const double RefusedWeight = 3;

    private readonly IReadOnlyList<ScheduledMatch> _fixed = fixedMatches ?? [];

    public double Evaluate(IChromosome chromosome)
    {
        if (chromosome is not SeasonScheduleChromosome)
            throw new Exception("SeasonScheduleEvaluator can only evaluate SeasonScheduleChromosome");

        var genes = chromosome.GetGenes().Select(x => (SeasonMatchGene)x.Value).ToList();

        // One gene a match (ScheduleService's operators keep gene i = match i); a chromosome missing one (OrderedCrossover
        // duplicated a match's gene, #175) must never score better than a complete schedule, however many conflicts that has
        if (genes.Select(x => x.SeasonMatchID).Distinct().Count() != genes.Count)
            return 0;

        var placed = genes.Select(Scheduled).ToList();
        var others = _fixed.Concat(placed).ToList();
        var penalties = placed.Sum(match => checker.Check(match, others).Sum(c => c.Refuses ? RefusedWeight : 1));

        // 0 to 1, 1 a schedule with no conflict (FitnessThresholdTermination's 1.00), falling as penalties grow
        return 1.0 / (1.0 + penalties);
    }

    /// <summary>What is wrong with <paramref name="gene"/>'s time, among <paramref name="genes"/> (the run's) and the fixed matches.</summary>
    public IReadOnlyList<ScheduleConflict> ConflictsOf(SeasonMatchGene gene, IReadOnlyList<SeasonMatchGene> genes)
        => checker.Check(Scheduled(gene), [.. _fixed, .. genes.Select(Scheduled)]);

    /// <summary>A gene (its time in league time, as the timeslots are) as the checker takes a match: in UTC, its series long.</summary>
    private ScheduledMatch Scheduled(SeasonMatchGene gene)
        => new(gene.SeasonMatchID, gene.HomeTeamID, gene.AwayTeamID, ScheduleService.ToUtc(gene.ScheduledTime),
            ScheduleChecker.LengthOf(bestOf.GetValueOrDefault(gene.SeasonMatchID, 1)));
}
