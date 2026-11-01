using GeneticSharp;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;

namespace GrifballWebApp.Server.Scheduler;

public class ScheduleService
{
    private readonly GrifballContext _context;
    private readonly ILogger<ScheduleService> _logger;
    public ScheduleService(GrifballContext context, ILogger<ScheduleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// The league's time zone, US Eastern. By its IANA id, which every platform knows (Windows maps it since .NET 6);
    /// the Windows id only resolves where ICU can map it and needs the tz database too, which the Alpine image adds (tzdata).
    /// </summary>
    public const string LeagueTimeZoneId = "America/New_York";

    public static TimeZoneInfo LeagueTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(LeagueTimeZoneId);

    /// <summary>The GA's best schedule (gene i is match i's time), as the old endpoint returned it: 100 generations.</summary>
    public async Task<List<SeasonMatchGene>> GetTimeRecommendations(int seasonID, CancellationToken ct = default)
        => (await Recommend(seasonID, new ScheduleRunOptions { MaxGenerations = 100, TimeBudget = null }, null, ct)).Genes;

    /// <summary>
    /// Runs the genetic algorithm over the season's regular matches (those with both teams) and returns its best
    /// schedule: it stops at a perfect one (no overlap, every team available), after <see cref="ScheduleRunOptions.MaxGenerations"/>
    /// generations or once <see cref="ScheduleRunOptions.TimeBudget"/> is spent, whichever comes first. CPU-bound: the
    /// schedule recommendation job runs it on the worker. <paramref name="progress"/> hears each generation (its number and
    /// the best fitness so far).
    /// </summary>
    public async Task<ScheduleRun> Recommend(int seasonID, ScheduleRunOptions options, Action<int, double>? progress = null, CancellationToken ct = default)
    {
        var season = await _context.Seasons.Where(x => x.SeasonID == seasonID).FirstOrDefaultAsync(ct)
            ?? throw new Exception("Season not found");

        var timeZone = LeagueTimeZone();
        var start = TimeZoneInfo.ConvertTimeFromUtc(season.SeasonStart, timeZone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(season.SeasonEnd, timeZone);

        var matches = await _context.SeasonMatches
            .Where(x => x.SeasonID == seasonID)
            .Where(x => x.BracketMatch == null)
            .Where(x => x.HomeTeam != null)
            .Where(x => x.AwayTeam != null)
            .ToListAsync(ct);

        var timeSlots = await _context.Availability.ToArrayAsync(ct);

        var timeSlotsWithTeams = await _context.Availability
            .Include(x => x.TeamAvailability.Where(x => x.Team.SeasonID == seasonID))
            .ToArrayAsync(ct);

        var selection = new EliteSelection();
        var crossover = CreateCrossover();
        var mutation = CreateMutation();
        var fitness = new SeasonScheduleEvaluator(timeSlotsWithTeams);
        var adam = new SeasonScheduleChromosome(matches, matches.Count, start, end, timeSlots);
        var population = new Population(10, 100, adam);

        var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
        ga.Stopped += (o, arg) => _logger.LogInformation("GA Stopped");
        ga.TerminationReached += (o, arg) => _logger.LogInformation("GA Termination Reached after {Generations} generations", ga.GenerationsNumber);
        ITermination termination = new OrTermination(new GenerationNumberTermination(options.MaxGenerations), new FitnessThresholdTermination(1.0));
        if (options.TimeBudget is { } budget)
            termination = new OrTermination(termination, new TimeEvolvingTermination(budget));
        ga.Termination = termination;
        if (progress is not null)
            ga.GenerationRan += (o, arg) => progress(ga.GenerationsNumber, ga.BestChromosome?.Fitness ?? 0);

        // Start blocks until it ends: on a thread of its own, stopped when the caller cancels
        using (ct.Register(() =>
        {
            if (ga.IsRunning)
                ga.Stop();
        }))
        {
            await Task.Run(ga.Start, CancellationToken.None);
        }
        ct.ThrowIfCancellationRequested();

        var best = (SeasonScheduleChromosome)ga.BestChromosome;
        var genes = best.GetGenes().Select(x => (SeasonMatchGene)x.Value).ToList();
        return new ScheduleRun(genes, best.Fitness ?? 0, ga.GenerationsNumber, timeSlotsWithTeams);
    }

    /// <summary>
    /// A schedule is "a time for each of N fixed matches" (gene i is always match i), not an ordering
    /// of matches, so order-based operators don't fit. OrderedCrossover assumes the genes are a
    /// permutation of distinct values and, because SeasonMatchGene equality includes ScheduledTime,
    /// could copy one match twice and drop another (#175). UniformCrossover takes each index from one
    /// parent or the other, so it only exchanges times between the two parents' copies of a match.
    /// </summary>
    public static ICrossover CreateCrossover() => new UniformCrossover();

    /// <summary>
    /// Regenerates each gene with the GA's mutation probability through
    /// <see cref="SeasonScheduleChromosome.GenerateGene"/>, giving that match a new random time.
    /// InsertionMutation only moved genes around, which never changes a match's time, so the GA could
    /// only recombine the times of its initial population. Create one per run: UniformMutation caches
    /// the gene indexes on first use.
    /// </summary>
    public static IMutation CreateMutation() => new UniformMutation(allGenesMutable: true);


}

/// <summary>
/// How long the genetic algorithm may run (ScheduleRecommendations: the job's): whichever limit comes first, and sooner at a
/// perfect schedule.
/// </summary>
public sealed class ScheduleRunOptions
{
    public int MaxGenerations { get; set; } = 2000;
    public TimeSpan? TimeBudget { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>The algorithm's best schedule (one gene per match), its fitness (0 to 1) and the generations it ran.</summary>
public sealed record ScheduleRun(List<SeasonMatchGene> Genes, double Fitness, int Generations, GrifballWebApp.Database.Models.AvailabilityOption[] Availability);
