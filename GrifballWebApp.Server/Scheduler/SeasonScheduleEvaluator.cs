using GeneticSharp;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Scheduler;

public class SeasonScheduleEvaluator : IFitness
{
    private readonly AvailabilityGridOption[] _options;
    public SeasonScheduleEvaluator(AvailabilityGridOption[] options)
    {
        _options = options;
    }

    public double Evaluate(IChromosome chromosome)
    {
        double score = 1;

        if (chromosome is not SeasonScheduleChromosome ssc)
            throw new Exception("SeasonScheduleEvaluator can only evaluate SeasonScheduleChromosome");

        var genes = chromosome.GetGenes().Select(x => (SeasonMatchGene)x.Value).ToList();

        //var GetoverLaps = new Func<SeasonMatchGene, List<SeasonMatchGene>>(current => genes
        //    .Except(new[] { current })
        //    //.Where(other => current.SameTeamPlaying(other))
        //    .Where(other => current.HasOverlap(other))
        //    .ToList());

        // TODO: need to make sure it fits teams availablity

        foreach (var gene in genes)
        {
            var count = gene.GetOverlap(genes).Count();

            score -= count;

            if (!gene.HomeTeamAvailable(_options))
                score -= 1;

            if (!gene.AwayTeamAvailable(_options))
                score -= 1;
        }

        return Math.Pow(Math.Abs(score), -1);
    }
}
