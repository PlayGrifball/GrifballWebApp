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

    public async Task<List<SeasonMatchGene>> GetTimeRecommendations(CancellationToken ct = default)
    {
        var season = await _context.Seasons.Where(x => x.SeasonID == 4).FirstOrDefaultAsync(ct)
            ?? throw new Exception("Season not found");

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var start = TimeZoneInfo.ConvertTimeFromUtc(season.SeasonStart, timeZone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(season.SeasonEnd, timeZone);

        var matches = await _context.SeasonMatches
            .Where(x => x.BracketMatch == null)
            .Where(x => x.HomeTeam != null)
            .Where(x => x.AwayTeam != null)
            .ToListAsync(ct);

        var timeSlots = await _context.Availability.ToArrayAsync(ct);

        var timeSlotsWithTeams = await _context.Availability
            .Include(x => x.TeamAvailability.Where(x => x.Team.SeasonID == 4))
            .ToArrayAsync(ct);

        var selection = new EliteSelection();
        var crossover = new OrderedCrossover();
        var mutation = new InsertionMutation();
        var fitness = new SeasonScheduleEvaluator(timeSlotsWithTeams);
        var adam = new SeasonScheduleChromosome(matches, matches.Count, start, end, timeSlots);
        var population = new Population(10, 100, adam);

        var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
        ga.Stopped += (o, arg) => _logger.LogInformation("GA Stopped");
        ga.TerminationReached += (o, arg) => _logger.LogInformation("GA Termination Reached");
        ga.Termination = new GenerationNumberTermination();

        // Create tasks so caller can cancel request
        var startTask = Task.Run(ga.Start);
        var stopTask = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(500);
                if (ct.IsCancellationRequested && ga.IsRunning)
                    ga.Stop();
                if (ga.State is GeneticAlgorithmState.TerminationReached or GeneticAlgorithmState.Stopped)
                    return;
            }
        });
        await Task.WhenAny(startTask, stopTask);
        ct.ThrowIfCancellationRequested();
        
        var bf = ga.BestChromosome.Fitness;

        var best = ga.BestChromosome as SeasonScheduleChromosome;

        var genes = best.GetGenes().Select(x => (SeasonMatchGene)x.Value).ToList();

        var overlaps = genes.Where(x => x.AnyOverlap(genes)).OrderBy(x => x.ScheduledTime).ToList();
        var nooverlaps = genes.Where(x => !x.AnyOverlap(genes)).OrderBy(x => x.ScheduledTime).ToList();

        var badMatches = genes
            .Where(x => x.AnyOverlap(genes) || !x.HomeTeamAvailable(timeSlotsWithTeams) || !x.AwayTeamAvailable(timeSlotsWithTeams))
            .OrderBy(x => x.ScheduledTime).ToList();

        //var min = genes.MinBy(x => x.ScheduledTime);
        //var max = genes.MaxBy(x => x.ScheduledTime);

        return genes;
    }


}
