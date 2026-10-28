using GeneticSharp;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Scheduler;

namespace GrifballWebApp.Test;

/// <summary>
/// The crossover and mutation ScheduleService gives the GA must keep "gene i is match i", so a schedule
/// never loses or duplicates a match (#175). GeneticSharp's operators read the static
/// RandomizationProvider.Current, which these tests point at a seeded one, hence NonParallelizable.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ScheduleServiceOperatorsTests
{
    private const int Seed = 175;
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0);
    private static readonly DateTime End = new(2024, 1, 29, 0, 0, 0);

    private IRandomization _previousRandomization;

    [SetUp]
    public void SetUp()
    {
        _previousRandomization = RandomizationProvider.Current;
        RandomizationProvider.Current = new SeededRandomization(Seed);
    }

    [TearDown]
    public void TearDown()
    {
        RandomizationProvider.Current = _previousRandomization;
    }

    // Round robin between 4 teams: every team plays 3 matches, so times can clash.
    private static List<SeasonMatch> RoundRobin()
    {
        var pairs = new[] { (1, 2), (3, 4), (1, 3), (2, 4), (1, 4), (2, 3) };
        return pairs.Select((p, i) => new SeasonMatch { SeasonMatchID = i + 1, HomeTeamID = p.Item1, AwayTeamID = p.Item2 }).ToList();
    }

    private static SeasonScheduleChromosome Adam(List<SeasonMatch> matches, AvailabilityOption[] options) =>
        new(matches, matches.Count, Start, End, options, new Random(Seed));

    private static List<SeasonMatchGene> Genes(IChromosome chromosome) =>
        chromosome.GetGenes().Select(g => (SeasonMatchGene)g.Value).ToList();

    [Test]
    public void CrossoverAndMutation_NeverDuplicateOrDropAMatch()
    {
        var matches = RoundRobin();
        var expectedIDs = matches.Select(m => m.SeasonMatchID).ToArray();
        var adam = Adam(matches, SeasonScheduleChromosomeTests.EveryDay(20, 21));
        var crossover = ScheduleService.CreateCrossover();
        var mutation = ScheduleService.CreateMutation();
        var pool = Enumerable.Range(0, 10).Select(_ => adam.CreateNew()).ToList();
        var rng = new Random(Seed);

        // Breed children from children so any corruption would compound across generations.
        for (var i = 0; i < 2000; i++)
        {
            var parents = new List<IChromosome> { pool[rng.Next(pool.Count)], pool[rng.Next(pool.Count)] };
            foreach (var child in crossover.Cross(parents))
            {
                mutation.Mutate(child, 0.5f);
                Assert.That(Genes(child).Select(g => g.SeasonMatchID), Is.EqualTo(expectedIDs), $"iteration {i}");
                pool[rng.Next(pool.Count)] = child;
            }
        }
    }

    [Test]
    public void Crossover_KeepsEachMatchAtItsIndexWithATimeFromOneParent()
    {
        var adam = Adam(RoundRobin(), SeasonScheduleChromosomeTests.EveryDay(20, 21));
        var first = adam.CreateNew();
        var second = adam.CreateNew();

        var children = ScheduleService.CreateCrossover().Cross([first, second]);

        var firstGenes = Genes(first);
        var secondGenes = Genes(second);
        Assert.That(children, Has.Count.EqualTo(2));
        foreach (var child in children)
        {
            var childGenes = Genes(child);
            for (var i = 0; i < childGenes.Count; i++)
                Assert.That(childGenes[i], Is.EqualTo(firstGenes[i]).Or.EqualTo(secondGenes[i]));
        }
    }

    [Test]
    public void Mutation_GivesMatchesNewTimes()
    {
        var chromosome = Adam(RoundRobin(), SeasonScheduleChromosomeTests.EveryDay(20, 21));
        var before = Genes(chromosome);

        // Probability 1: every gene is regenerated.
        ScheduleService.CreateMutation().Mutate(chromosome, 1f);

        var after = Genes(chromosome);
        Assert.Multiple(() =>
        {
            Assert.That(after.Select(g => (g.SeasonMatchID, g.HomeTeamID, g.AwayTeamID)),
                Is.EqualTo(before.Select(g => (g.SeasonMatchID, g.HomeTeamID, g.AwayTeamID))));
            // InsertionMutation only reordered genes, so no match ever got a time it didn't start with.
            Assert.That(after.Select(g => g.ScheduledTime), Is.Not.EqualTo(before.Select(g => g.ScheduledTime)));
            Assert.That(after.All(g => g.ScheduledTime.Hour is 20 or 21 && g.ScheduledTime.Minute == 0), Is.True);
        });
    }

    [Test]
    public void Mutation_ProbabilityZero_ChangesNothing()
    {
        var chromosome = Adam(RoundRobin(), SeasonScheduleChromosomeTests.EveryDay(20, 21));
        var before = Genes(chromosome);

        ScheduleService.CreateMutation().Mutate(chromosome, 0f);

        Assert.That(Genes(chromosome), Is.EqualTo(before));
    }

    [Test]
    public void GeneticAlgorithm_BestScheduleHasEveryMatchExactlyOnce()
    {
        var matches = RoundRobin();
        var options = SeasonScheduleChromosomeTests.EveryDay(20, 21);
        foreach (var o in options)
            foreach (var team in new[] { 1, 2, 3, 4 })
                o.TeamAvailability.Add(new TeamAvailability { TeamID = team });

        var ga = new GeneticAlgorithm(new Population(10, 100, Adam(matches, options)), new SeasonScheduleEvaluator(options),
            new EliteSelection(), ScheduleService.CreateCrossover(), ScheduleService.CreateMutation())
        {
            Termination = new GenerationNumberTermination(50),
        };
        ga.Start();

        var best = Genes(ga.BestChromosome);
        Assert.Multiple(() =>
        {
            Assert.That(best.Select(g => g.SeasonMatchID), Is.EqualTo(matches.Select(m => m.SeasonMatchID)));
            Assert.That(ga.BestChromosome.Fitness, Is.GreaterThan(0));
        });
    }
}
