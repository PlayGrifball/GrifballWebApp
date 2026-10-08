using GeneticSharp;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class SeasonScheduleChromosomeTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0);
    private static readonly DateTime End = new(2024, 1, 15, 0, 0, 0);

    internal static AvailabilityOption[] EveryDay(params int[] hours) =>
        Enum.GetValues<DayOfWeek>()
            .SelectMany(d => hours.Select(h => new AvailabilityOption { DayOfWeek = d, Time = new TimeOnly(h, 0) }))
            .ToArray();

    private static List<SeasonMatch> Matches(int count) =>
        Enumerable.Range(1, count).Select(i => new SeasonMatch { SeasonMatchID = i, HomeTeamID = i * 10, AwayTeamID = i * 10 + 1 }).ToList();

    [Test]
    public void Constructor_GeneCountMismatch_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SeasonScheduleChromosome(Matches(2), 3, Start, End, EveryDay(20)));
        Assert.That(ex!.Message, Is.EqualTo("geneCount does not match number of matches"));
    }

    [Test]
    public void Constructor_CreatesOneGenePerMatchAtAvailableSlots()
    {
        var options = EveryDay(19, 21);
        var matches = Matches(5);

        var chromosome = new SeasonScheduleChromosome(matches, 5, Start, End, options);

        var genes = chromosome.GetGenes().Select(g => (SeasonMatchGene)g.Value).ToList();
        Assert.That(genes.Select(g => (g.SeasonMatchID, g.HomeTeamID, g.AwayTeamID)),
            Is.EqualTo(matches.Select(m => (m.SeasonMatchID, m.HomeTeamID!.Value, m.AwayTeamID!.Value))));
        foreach (var g in genes)
        {
            Assert.That(g.ScheduledTime, Is.InRange(Start, End));
            Assert.That(g.ScheduledTime.Hour, Is.AnyOf(19, 21));
            Assert.That(g.ScheduledTime.Minute, Is.Zero);
        }
    }

    [Test]
    public void CreateNew_ReturnsFreshChromosomeOfSameShape()
    {
        var chromosome = new SeasonScheduleChromosome(Matches(3), 3, Start, End, EveryDay(20));

        var created = chromosome.CreateNew();

        Assert.That(created, Is.TypeOf<SeasonScheduleChromosome>().And.Not.SameAs(chromosome));
        Assert.That(created.Length, Is.EqualTo(3));
        Assert.That(created.GetGenes().Select(g => ((SeasonMatchGene)g.Value).SeasonMatchID), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void GenerateGene_IndexOutOfRange_Throws()
    {
        var chromosome = new SeasonScheduleChromosome(Matches(2), 2, Start, End, EveryDay(20));

        var ex = Assert.Throws<Exception>(() => chromosome.GenerateGene(5));
        Assert.That(ex!.Message, Is.EqualTo("Missing course with index 5"));
    }

    [Test]
    public void Constructor_MatchWithoutHomeTeam_Throws()
    {
        var matches = new List<SeasonMatch> { new() { SeasonMatchID = 1, HomeTeamID = 1, AwayTeamID = 2 }, new() { SeasonMatchID = 2, HomeTeamID = null, AwayTeamID = 2 } };
        var ex = Assert.Throws<Exception>(() => new SeasonScheduleChromosome(matches, 2, Start, End, EveryDay(20)));
        Assert.That(ex!.Message, Is.EqualTo("Cannot create gene with home team"));
    }

    [Test]
    public void Constructor_MatchWithoutAwayTeam_Throws()
    {
        var matches = new List<SeasonMatch> { new() { SeasonMatchID = 1, HomeTeamID = 1, AwayTeamID = 2 }, new() { SeasonMatchID = 2, HomeTeamID = 1, AwayTeamID = null } };
        var ex = Assert.Throws<Exception>(() => new SeasonScheduleChromosome(matches, 2, Start, End, EveryDay(20)));
        Assert.That(ex!.Message, Is.EqualTo("Cannot create gene with away team"));
    }
}
