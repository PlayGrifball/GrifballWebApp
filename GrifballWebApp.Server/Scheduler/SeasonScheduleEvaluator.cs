using GeneticSharp;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Scheduler;

public class SeasonScheduleEvaluator : IFitness
{
    private readonly AvailabilityOption[] _options;
    public SeasonScheduleEvaluator(AvailabilityOption[] options)
    {
        _options = options;
    }

    public double Evaluate(IChromosome chromosome)
    {
        double penalties = 0;

        if (chromosome is not SeasonScheduleChromosome ssc)
            throw new Exception("SeasonScheduleEvaluator can only evaluate SeasonScheduleChromosome");

        var genes = chromosome.GetGenes().Select(x => (SeasonMatchGene)x.Value).ToList();

        // A chromosome should contain exactly one gene per match. GeneticSharp's OrderedCrossover
        // compares genes by value equality (including ScheduledTime) when deciding which genes a
        // parent already contributed, so it can duplicate a match's gene while dropping another
        // match's gene entirely. That must never score better than a complete schedule, no matter
        // how many overlap/availability penalties the complete schedule racks up (e.g. dropping an
        // unschedulable match is not an improvement), so treat it as the worst possible fitness
        // rather than folding it into the same penalty count below.
        if (genes.Select(x => x.SeasonMatchID).Distinct().Count() != genes.Count)
            return 0;

        //var GetoverLaps = new Func<SeasonMatchGene, List<SeasonMatchGene>>(current => genes
        //    .Except(new[] { current })
        //    //.Where(other => current.SameTeamPlaying(other))
        //    .Where(other => current.HasOverlap(other))
        //    .ToList());

        // TODO: need to make sure it fits teams availablity

        foreach (var gene in genes)
        {
            var count = gene.GetOverlap(genes).Count();

            penalties += count;

            if (!gene.HomeTeamAvailable(_options))
                penalties += 1;

            if (!gene.AwayTeamAvailable(_options))
                penalties += 1;
        }

        // Fitness must stay on a 0-1 scale where 1.0 is a perfect, conflict-free schedule
        // (GeneticSharp's FitnessThresholdTermination defaults to an expected fitness of
        // 1.00) and must decrease monotonically as penalties increase, so that EliteSelection
        // and comparisons against the best chromosome's fitness behave correctly.
        return 1.0 / (1.0 + penalties);
    }
}
