using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// ScheduleService.Recommend and GetTimeRecommendations against the fake IApplicationDbContext: which matches it
/// schedules (the season's regular ones with both teams), the timeslots it reads (with only this season's teams' availability),
/// its limits, its progress reports and its cancellation. The GA's times are random (the chromosome makes its own
/// Random), so these assert only what holds for any run: one gene per match, in range.
/// </summary>
[TestFixture]
internal sealed class ScheduleServiceRecommendUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private const int OtherSeasonID = 2;
    private const int OtherTeamID = 5;
    private static readonly int[] SeasonTeams = [1, 2, 3, 4];
    private static readonly (int Home, int Away)[] RoundRobin = [(1, 2), (3, 4), (1, 3), (2, 4), (1, 4), (2, 3)];

    private ScheduleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ScheduleService(Context, Substitute.For<ILogger<ScheduleService>>());
    }

    /// <summary>A season of teams 1-4 playing a round robin (and a bracket match and one still missing a team, which
    /// aren't scheduled), timeslots every day at 20:00 and 21:00 for its teams, and another season's team on one of them.
    /// Returns the regular matches' ids.</summary>
    private async Task<List<int>> SeedSeason()
    {
        var season = Season.Seed(seasonName: "S1", seasonID: SeasonID);
        season.Schedule(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.AddRange(season, Season.Seed(seasonName: "S2", seasonID: OtherSeasonID));
        Context.Teams.AddRange(SeasonTeams.Select(id => Team.Seed(teamName: $"T{id}", seasonID: SeasonID, teamID: id)));
        Context.Teams.Add(Team.Seed(teamName: "Other", seasonID: OtherSeasonID, teamID: OtherTeamID));
        await Context.SaveChangesAsync();

        var options = AvailabilityTestData.EveryDayFor(SeasonTeams, 20, 21);
        Context.Availability.AddRange(options);
        await Context.SaveChangesAsync();
        Context.Add(TeamAvailability.Seed(OtherTeamID, options[0].AvailabilityOptionID));

        var regular = RoundRobin.Select(p => SeasonMatch.Seed(SeasonID, p.Home, p.Away)).ToList();
        Context.SeasonMatches.AddRange(regular);
        Context.SeasonMatches.Add(SeasonMatch.Seed(SeasonID, 1, 2, bracketMatch: MatchBracketInfo.Seed(roundNumber: 1, matchNumber: 1)));
        Context.SeasonMatches.Add(SeasonMatch.Seed(SeasonID, 3, null));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return regular.Select(m => m.SeasonMatchID).ToList();
    }

    [Test]
    public async Task Recommend_NoSuchSeason_Throws()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.Recommend(999, new ScheduleRunOptions()));

        Assert.That(ex!.Message, Is.EqualTo("Season not found"));
    }

    [Test]
    public async Task Recommend_SchedulesEachRegularMatchWithBothTeamsOnce_WithinItsLimits()
    {
        var regular = await SeedSeason();
        var generations = new List<int>();
        var fitnesses = new List<double>();

        var run = await _service.Recommend(SeasonID, new ScheduleRunOptions { MaxGenerations = 5, TimeBudget = TimeSpan.FromMinutes(1) },
            (generation, fitness) => { generations.Add(generation); fitnesses.Add(fitness); });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.Genes.Select(g => g.SeasonMatchID), Is.EquivalentTo(regular));
            Assert.That(run.Genes.Select(g => (g.HomeTeamID, g.AwayTeamID)), Is.EquivalentTo(RoundRobin));
            Assert.That(run.Generations, Is.InRange(1, 5));
            Assert.That(run.Fitness, Is.InRange(0.0, 1.0));
            Assert.That(generations, Is.EqualTo(Enumerable.Range(1, run.Generations)));
            Assert.That(fitnesses, Has.All.InRange(0.0, 1.0));
            Assert.That(run.Availability, Has.Length.EqualTo(14));
            // Only this season's teams' availability is read with the timeslots
            Assert.That(run.Availability.SelectMany(o => o.TeamAvailability).Select(a => a.TeamID).Distinct(), Is.EquivalentTo(SeasonTeams));
        }
    }

    [Test]
    public async Task Recommend_NoTimeBudget_StillStopsAtItsGenerations()
    {
        var regular = await SeedSeason();

        var run = await _service.Recommend(SeasonID, new ScheduleRunOptions { MaxGenerations = 3, TimeBudget = null });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.Genes, Has.Count.EqualTo(regular.Count));
            Assert.That(run.Generations, Is.InRange(1, 3));
        }
    }

    [Test]
    public async Task Recommend_CancelledWhileRunning_StopsAndThrows()
    {
        await SeedSeason();
        using var cts = new CancellationTokenSource();

        // Cancelled from the first generation's report: whether or not the GA stops there, the run ends in the cancellation
        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.Recommend(SeasonID,
            new ScheduleRunOptions { MaxGenerations = 20, TimeBudget = null }, (_, _) => cts.Cancel(), cts.Token));
    }

    [Test]
    public async Task GetTimeRecommendations_ReturnsAGeneForEachRegularMatch()
    {
        var regular = await SeedSeason();

        var genes = await _service.GetTimeRecommendations(SeasonID);

        Assert.That(genes.Select(g => g.SeasonMatchID), Is.EquivalentTo(regular));
    }
}
