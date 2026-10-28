using GeneticSharp;
using GrifballWebApp.Database.Models;
using System;

namespace GrifballWebApp.Server.Scheduler;

public class SeasonScheduleChromosome : ChromosomeBase
{
    private readonly List<SeasonMatch> _matches;
    private readonly int _geneCount;
    private readonly DateTime _start;
    private readonly DateTime _end;
    private readonly AvailabilityOption[] _options;
    private Random Random { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="matches"></param>
    /// <param name="geneCount"></param>
    /// <param name="start">Start time in Eastern Standard Time</param>
    /// <param name="end">End time in Eastern Standard Time</param>
    /// <param name="options"></param>
    /// <param name="random">Source of random times, shared with chromosomes made by <see cref="CreateNew"/>. Pass a seeded one for reproducible runs.</param>
    /// <exception cref="ArgumentException"></exception>
    public SeasonScheduleChromosome(List<SeasonMatch> matches, int geneCount, DateTime start, DateTime end, AvailabilityOption[] options, Random? random = null) : base(geneCount)
    {
        if (geneCount != matches.Count)
        {
            throw new ArgumentException("geneCount does not match number of matches");
        }
        _matches = matches;
        _geneCount = geneCount;
        _start = start;
        _end = end;
        _options = options;
        Random = random ?? new Random();
        CreateGenes();
    }

    public override IChromosome CreateNew()
    {
        return new SeasonScheduleChromosome(_matches, _geneCount, _start, _end, _options, Random);
    }

    /// <summary>
    /// Gene <paramref name="geneIndex"/> is always match <paramref name="geneIndex"/>, with a random time.
    /// The GA's operators keep it that way (see <see cref="ScheduleService.CreateCrossover"/> and
    /// <see cref="ScheduleService.CreateMutation"/>), so every chromosome holds each match exactly once.
    /// </summary>
    public override Gene GenerateGene(int geneIndex)
    {
        var match = _matches.Skip(geneIndex).FirstOrDefault() ?? throw new Exception($"Missing course with index {geneIndex}");

        DateTime randomDate = new DateTime(Random.NextInt64(_start.Ticks, _end.Ticks));

        var possibleTimes = _options
            .Where(x => x.DayOfWeek == randomDate.DayOfWeek)
            .ToArray();
        var index = Random.Next(possibleTimes.Length);
        var time = possibleTimes[index].Time;

        var newDateTime = new DateTime(randomDate.Year, randomDate.Month, randomDate.Day, time.Hour, time.Minute, time.Second);

        return new Gene(new SeasonMatchGene()
        {
            SeasonMatchID = match.SeasonMatchID,
            HomeTeamID = match.HomeTeamID ?? throw new Exception("Cannot create gene with home team"),
            AwayTeamID = match.AwayTeamID ?? throw new Exception("Cannot create gene with away team"),
            ScheduledTime = newDateTime,
        });
    }
}
