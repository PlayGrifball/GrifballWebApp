using GeneticSharp;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Scheduler;

public partial class ScheduleService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ScheduleService> _logger;
    private readonly TimeProvider _time;
    public ScheduleService(IApplicationDbContext context, ILogger<ScheduleService> logger, TimeProvider? time = null)
    {
        _context = context;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// The league's time zone, US Eastern. By its IANA id, which every platform knows (Windows maps it since .NET 6);
    /// the Windows id only resolves where ICU can map it and needs the tz database too, which the Alpine image adds (tzdata).
    /// </summary>
    public const string LeagueTimeZoneId = "America/New_York";

    public static TimeZoneInfo LeagueTimeZone() => League;

    // Found once: the scheduler converts times many times a generation
    private static readonly TimeZoneInfo League = TimeZoneInfo.FindSystemTimeZoneById(LeagueTimeZoneId);

    /// <summary>A league wall-clock time (the GA's, which picks the season's weekly timeslots) as UTC, the time matches are kept in.</summary>
    public static DateTime ToUtc(DateTime leagueTime)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(leagueTime, DateTimeKind.Unspecified), LeagueTimeZone());

    /// <summary>Why a season can't be scheduled: it has no timeslots of its own.</summary>
    public const string NoTimeslots = "The season has no timeslots: set them on its edit page first.";

    /// <summary>Why a season can't be scheduled: the algorithm needs two matches to place, and fewer are left to play.</summary>
    public const string TooFewToPlace = "Fewer than two matches are left to schedule: set the time by hand (its match page suggests some).";

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

        // The matches it places: the regular ones still to play, not one whose reschedule is waiting or was agreed (a time
        // already). The rest with a time are fixed: the placed are checked against them where they are.
        var matches = await _context.SeasonMatches
            .Where(x => x.SeasonID == seasonID)
            .Where(x => x.BracketMatch == null)
            .Where(x => x.HomeTeam != null)
            .Where(x => x.AwayTeam != null)
            .Where(SeasonMatch.Undecided)
            .Where(x => !_context.MatchReschedules.Any(r => r.SeasonMatchID == x.SeasonMatchID
                && (r.Status == RescheduleStatus.Pending || r.Status == RescheduleStatus.Approved)))
            .ToListAsync(ct);
        if (matches.Count < 2)
            throw new InvalidOperationException(TooFewToPlace);

        // A season under way: only times still to come. And each match over by the season's end (the longest series's length)
        var now = _time.GetUtcNow().UtcDateTime;
        var from = now > season.SeasonStart && now < season.SeasonEnd ? now : season.SeasonStart;
        var longest = GrifballWebApp.Application.Scheduling.ScheduleChecker.LengthOf(matches.Select(m => m.BestOf).DefaultIfEmpty(1).Max());
        var timeZone = LeagueTimeZone();
        var start = TimeZoneInfo.ConvertTimeFromUtc(from, timeZone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(season.SeasonEnd - longest, timeZone);

        // The season's own weekly timeslots: not every option any season ever had
        var timeSlots = await _context.Availability
            .Where(x => x.SeasonAvailability.Any(s => s.SeasonID == seasonID))
            .ToArrayAsync(ct);
        if (timeSlots.Length == 0)
            throw new InvalidOperationException(NoTimeslots);
        // The season's rules and its teams' availability, and the matches it doesn't place (a time already: a bracket's)
        var schedule = (await new GrifballWebApp.Application.Scheduling.SchedulingService(_context, TimeProvider.System).LoadAsync(seasonID, ct))!;
        var placing = matches.Select(m => m.SeasonMatchID).ToHashSet();
        var fixedMatches = schedule.Scheduled.Where(m => !placing.Contains(m.SeasonMatchID)).ToList();

        var selection = new EliteSelection();
        var crossover = CreateCrossover();
        var mutation = CreateMutation();
        var fitness = new SeasonScheduleEvaluator(schedule.Checker, matches.ToDictionary(m => m.SeasonMatchID, m => m.BestOf), fixedMatches);
        var adam = new SeasonScheduleChromosome(matches, matches.Count, start, end, timeSlots);
        var population = new Population(10, 100, adam);

        var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
        ga.Stopped += (o, arg) => _logger.LogInformation("GA Stopped");
        ga.TerminationReached += (o, arg) => LogTerminationReached(_logger, ga.GenerationsNumber);
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
        return new ScheduleRun(genes, best.Fitness ?? 0, ga.GenerationsNumber, fitness.ConflictsOf);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "GA Termination Reached after {Generations} generations")]
    private static partial void LogTerminationReached(ILogger logger, int generations);


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

/// <summary>
/// The algorithm's best schedule (one gene per match), its fitness (0 to 1), the generations it ran, and what is wrong with a
/// gene's time among the run's (the season's rules: its flags on the recommendation).
/// </summary>
public sealed record ScheduleRun(List<SeasonMatchGene> Genes, double Fitness, int Generations,
    Func<SeasonMatchGene, IReadOnlyList<SeasonMatchGene>, IReadOnlyList<GrifballWebApp.Application.Scheduling.ScheduleConflict>> Conflicts);
