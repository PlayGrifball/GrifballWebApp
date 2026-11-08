using GeneticSharp;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Scheduler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScheduleServiceTests
{
    private const int SeasonID = 4;
    private GrifballContext _context;
    private ScheduleService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new ScheduleService(_context, Substitute.For<ILogger<ScheduleService>>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<List<Team>> Arrange()
    {
        await using (var tx = await _context.Database.BeginTransactionAsync())
        {
            // IDENTITY_INSERT is per session, the transaction keeps the connection open
            await _context.DisableContraints("[Event].[Seasons]");
            _context.Seasons.Add(Season.Seed(seasonID: SeasonID, seasonName: "S4").WithDates(seasonStart: new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2024, 1, 29, 12, 0, 0, DateTimeKind.Utc)));
            await _context.SaveChangesAsync();
            await _context.EnableContraints("[Event].[Seasons]");
            await tx.CommitAsync();
        }

        var teams = Enumerable.Range(1, 4).Select(i => new Team { SeasonID = SeasonID, TeamName = $"T{i}" }).ToList();
        _context.Teams.AddRange(teams);
        var options = SeasonScheduleChromosomeTests.EveryDay(20, 21);
        _context.Availability.AddRange(options);
        await _context.SaveChangesAsync();
        foreach (var o in options)
            foreach (var t in teams)
                _context.TeamAvailability.Add(new TeamAvailability { TeamID = t.TeamID, AvailabilityOptionID = o.AvailabilityOptionID });

        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(SeasonID, teams[0].TeamID, teams[1].TeamID),
            SeasonMatch.Seed(SeasonID, teams[2].TeamID, teams[3].TeamID),
            SeasonMatch.Seed(SeasonID, teams[0].TeamID, teams[2].TeamID),
            // Excluded: missing a team
            SeasonMatch.Seed(SeasonID, teams[0].TeamID, null));
        await _context.SaveChangesAsync();
        // Excluded: playoff match
        _context.SeasonMatches.Add(SeasonMatch.Seed(SeasonID, teams[1].TeamID, teams[3].TeamID, bracketMatch: MatchBracketInfo.Seed(matchNumber: 1, roundNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2)));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return teams;
    }

    [Test]
    public async Task GetTimeRecommendations_NoSuchSeason_Throws()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetTimeRecommendations(SeasonID));
        Assert.That(ex!.Message, Is.EqualTo("Season not found"));
    }

    [Test]
    public async Task GetTimeRecommendations_SchedulesEveryRegularSeasonMatchInSeasonWindow()
    {
        var teams = await Arrange();
        var expectedIDs = await _context.SeasonMatches
            .Where(x => x.SeasonID == SeasonID && x.BracketMatch == null && x.HomeTeamID != null && x.AwayTeamID != null)
            .Select(x => x.SeasonMatchID).ToListAsync();

        var genes = await _service.GetTimeRecommendations(SeasonID);

        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var start = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), eastern);
        var end = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2024, 1, 29, 12, 0, 0, DateTimeKind.Utc), eastern);
        Assert.Multiple(() =>
        {
            Assert.That(genes.Select(g => g.SeasonMatchID), Is.EquivalentTo(expectedIDs));
            Assert.That(genes.All(g => teams.Any(t => t.TeamID == g.HomeTeamID) && teams.Any(t => t.TeamID == g.AwayTeamID)), Is.True);
            Assert.That(genes.All(g => g.ScheduledTime >= start && g.ScheduledTime <= end), Is.True);
            Assert.That(genes.All(g => g.ScheduledTime.Hour is 20 or 21 && g.ScheduledTime.Minute == 0), Is.True);
        });
    }

    [Test]
    public async Task GetTimeRecommendations_ExcludesMatchesFromOtherSeasons()
    {
        await Arrange();
        var otherSeason = Season.Seed(seasonName: "Other");
        _context.Seasons.Add(otherSeason);
        await _context.SaveChangesAsync();
        var a = new Team { SeasonID = otherSeason.SeasonID, TeamName = "OA" };
        var b = new Team { SeasonID = otherSeason.SeasonID, TeamName = "OB" };
        _context.Teams.AddRange(a, b);
        await _context.SaveChangesAsync();
        var other = SeasonMatch.Seed(otherSeason.SeasonID, a.TeamID, b.TeamID);
        _context.SeasonMatches.Add(other);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var genes = await _service.GetTimeRecommendations(SeasonID);

        Assert.That(genes.Select(g => g.SeasonMatchID), Does.Not.Contain(other.SeasonMatchID));
        Assert.That(genes, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetTimeRecommendations_CancelledToken_Throws()
    {
        await Arrange();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await _service.GetTimeRecommendations(SeasonID, cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Recommend_StopsAtItsGenerationLimit_AndReportsEachGeneration()
    {
        await Arrange();
        var heard = new List<int>();

        var run = await _service.Recommend(SeasonID, new ScheduleRunOptions { MaxGenerations = 5, TimeBudget = TimeSpan.FromMinutes(1) }, (generation, _) => heard.Add(generation));

        Assert.Multiple(() =>
        {
            Assert.That(run.Genes, Has.Count.EqualTo(3));
            Assert.That(run.Generations, Is.InRange(1, 5), "At most the limit; fewer at a perfect schedule");
            Assert.That(heard, Is.EqualTo(Enumerable.Range(1, run.Generations)));
            Assert.That(run.Fitness, Is.InRange(0.0, 1.0));
        });
    }

    [Test]
    public async Task Recommend_CancelledWhileRunning_StopsTheAlgorithm_AndThrows()
    {
        await Arrange();
        using var cts = new CancellationTokenSource();

        // Cancelled from its first generation: the run is stopped, not left to its limit
        Assert.That(async () => await _service.Recommend(SeasonID, new ScheduleRunOptions { MaxGenerations = 1_000_000, TimeBudget = null }, (_, _) => cts.Cancel(), cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    private ScheduleRecommendations Recommendations() => new(_context, TimeProvider.System);

    private SchedulerController Controller(InMemoryJobs jobs, string? user = "commish")
    {
        var controller = new SchedulerController(_context, Recommendations(), jobs.OnDemand);
        if (user is not null)
            controller.WithUser("1", user);
        return controller;
    }

    [Test]
    public async Task TheJob_KeepsItsBestSchedule_WithEachMatchsConflicts()
    {
        var teams = await Arrange();
        var context = Substitute.For<Quartz.IJobExecutionContext>();
        var job = new ScheduleRecommendationJob(_service, Recommendations(), Microsoft.Extensions.Options.Options.Create(new ScheduleRunOptions { MaxGenerations = 3, TimeBudget = null }), TimeProvider.System);

        await job.Execute(context, new RecommendSchedule(SeasonID, "commish"));

        var latest = await Recommendations().Latest(SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(latest, Is.Not.Null);
            Assert.That((latest!.RequestedBy, latest.AppliedAt), Is.EqualTo(("commish", (DateTime?)null)));
            Assert.That(latest.Matches, Has.Count.EqualTo(3));
            Assert.That(latest.Matches.Select(m => m.ScheduledTime), Is.Ordered);
            Assert.That(latest.Matches.Select(m => m.HomeTeam).Distinct(), Is.SubsetOf(teams.Select(t => t.TeamName)));
            Assert.That(latest.Matches.All(m => m.CurrentTime is null && !m.Decided), Is.True, "Unscheduled, undecided");
            Assert.That(((Quartz.JobRunReport)context.Result!).Summary, Does.Contain("of 3 matches with a conflict"));
            Assert.That(job.Percent(1, TimeSpan.Zero), Is.EqualTo(33));
            Assert.That(job.Percent(3, TimeSpan.Zero), Is.EqualTo(99), "Never 100 before it's saved");
        });
    }

    [Test]
    public async Task Applying_SetsTheTimesOfTheMatchesWithoutAResult_Once()
    {
        await Arrange();
        var run = await _service.Recommend(SeasonID, new ScheduleRunOptions { MaxGenerations = 2, TimeBudget = null });
        var saved = await Recommendations().Save(SeasonID, run, "commish");
        var decided = run.Genes[0].SeasonMatchID;
        await _context.SeasonMatches.Where(m => m.SeasonMatchID == decided)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.HomeTeamResult, SeasonMatchResult.Won).SetProperty(m => m.AwayTeamResult, SeasonMatchResult.Loss));
        await using var jobs = await InMemoryJobs.Create(TimeProvider.System, null);
        var controller = Controller(jobs);

        var applied = await controller.Apply(saved.ScheduleRecommendationID, CancellationToken.None);
        var again = await controller.Apply(saved.ScheduleRecommendationID, CancellationToken.None);
        var missing = await controller.Apply(saved.ScheduleRecommendationID + 1000, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var times = await _context.SeasonMatches.Where(m => m.SeasonID == SeasonID).ToDictionaryAsync(m => m.SeasonMatchID, m => m.ScheduledTime);
        var latest = (await controller.Recommendation(SeasonID, CancellationToken.None)).Latest!;
        var stored = await _context.ScheduleRecommendationMatches.AsNoTracking().Include(m => m.ScheduleRecommendation).ThenInclude(r => r.Season)
            .Where(m => m.ScheduleRecommendationID == saved.ScheduleRecommendationID).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(applied.Value!.MatchesScheduled, Is.EqualTo(2));
            Assert.That(times[decided], Is.Null, "A decided match keeps its time");
            foreach (var gene in run.Genes.Skip(1))
                Assert.That(times[gene.SeasonMatchID], Is.EqualTo(gene.ScheduledTime).Within(TimeSpan.FromSeconds(1)));
            Assert.That(again.Result, Is.InstanceOf<ConflictResult>(), "Applied already");
            Assert.That(missing.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That((latest.AppliedBy, latest.AppliedAt.HasValue), Is.EqualTo(("commish", true)));
            Assert.That(latest.Matches.Single(m => m.SeasonMatchID == decided).Decided, Is.True);
            Assert.That(stored.Select(m => (m.ScheduleRecommendation.ScheduleRecommendationID, m.ScheduleRecommendation.Season.SeasonID)).Distinct(), Is.EqualTo(new[] { (saved.ScheduleRecommendationID, SeasonID) }));
            Assert.That(stored.Select(m => m.ScheduleRecommendationMatchID).Distinct().Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Applying_ReturnsNull_WhenAnotherApplyStampsItBetweenTheReadAndTheWrite()
    {
        // Two clicks at once: both read the recommendation unapplied, so the model check passes on both. The rival stamps it
        // in the gap after this apply's loading SELECT; only the guarded UPDATE (AppliedAt IS NULL) can then refuse it.
        // The rival writes just before this apply's transaction begins, not inside it: Microsoft.Data.Sqlite's BEGIN is
        // IMMEDIATE and takes the writer lock, so a second connection writing inside it would fail with SQLITE_BUSY. The gap
        // before BEGIN is the same window a real concurrent click lands in, and it is deterministic on SQL Server, Postgres
        // and SQLite alike.
        await Arrange();
        var run = await _service.Recommend(SeasonID, new ScheduleRunOptions { MaxGenerations = 2, TimeBudget = null });
        var saved = await Recommendations().Save(SeasonID, run, "commish");
        var before = await _context.SeasonMatches.AsNoTracking().ToDictionaryAsync(m => m.SeasonMatchID, m => m.ScheduledTime);
        var rivalAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var rival = new RivalApply(_context, saved.ScheduleRecommendationID, rivalAt);
        await using var racing = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).AddInterceptors(rival).Options);

        var applied = await new ScheduleRecommendations(racing, TimeProvider.System).Apply(saved.ScheduleRecommendationID, "commish");

        await using var ctx = _context.NewContextLike();
        var stamp = await ctx.ScheduleRecommendations.AsNoTracking().Where(r => r.ScheduleRecommendationID == saved.ScheduleRecommendationID)
            .Select(r => new { r.AppliedBy, r.AppliedAt }).SingleAsync();
        var after = await ctx.SeasonMatches.AsNoTracking().ToDictionaryAsync(m => m.SeasonMatchID, m => m.ScheduledTime);
        Assert.Multiple(() =>
        {
            Assert.That(rival.Stamped, Is.True, "The rival ran in the gap");
            Assert.That(applied, Is.Null, "The 409 path");
            Assert.That(stamp.AppliedBy, Is.EqualTo("other"));
            Assert.That(stamp.AppliedAt, Is.EqualTo(rivalAt).Within(TimeSpan.FromSeconds(1)));
            Assert.That(after, Is.EqualTo(before), "No match time changed");
        });
    }

    /// <summary>
    /// Stamps the recommendation applied by "other", through a second context, once: right after the apply's loading SELECT,
    /// just before its transaction begins.
    /// </summary>
    private sealed class RivalApply(GrifballContext like, int recommendationID, DateTime at) : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public bool Stamped { get; private set; }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbTransaction>> TransactionStartingAsync(
            System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.TransactionStartingEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbTransaction> result, CancellationToken cancellationToken = default)
        {
            if (!Stamped)
            {
                Stamped = true;
                await using var ctx = like.NewContextLike();
                await ctx.ScheduleRecommendations.Where(r => r.ScheduleRecommendationID == recommendationID)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.AppliedAt, (DateTime?)at).SetProperty(r => r.AppliedBy, "other"), cancellationToken);
            }
            return result;
        }
    }

    [Test]
    public async Task Recommending_QueuesOneRunPerSeason_OnTheWorker()
    {
        await Arrange();
        await using var jobs = await InMemoryJobs.Create(TimeProvider.System, null);
        var controller = Controller(jobs);

        var before = await controller.Recommendation(SeasonID, CancellationToken.None);
        var queued = (await controller.Recommend(SeasonID, CancellationToken.None)).Value;
        var again = (await controller.Recommend(SeasonID, CancellationToken.None)).Value;
        var missing = await controller.Recommend(SeasonID + 1000, CancellationToken.None);
        var after = await controller.Recommendation(SeasonID, CancellationToken.None);

        var triggers = await jobs.Scheduler.GetTriggersOfJob(OnDemandJobs.JobKeyOf(typeof(ScheduleRecommendationJob)));
        Assert.Multiple(() =>
        {
            Assert.That((before.Run.State, before.Latest), Is.EqualTo((OnDemandJobState.Idle, (ScheduleRecommendationDto?)null)));
            Assert.That((queued!.State, again!.State, after.Run.State), Is.EqualTo((OnDemandJobState.Queued, OnDemandJobState.Queued, OnDemandJobState.Queued)));
            Assert.That(triggers, Has.Count.EqualTo(1), "Asked twice: one run");
            Assert.That(missing.Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(SchedulerController))?.Roles, Is.EqualTo("Commissioner,Sysadmin"));
        });
    }

    [Test]
    public async Task GetTimeRecommendations_SchedulesEachMatchExactlyOnce()
    {
        await Arrange();
        var expectedIDs = await _context.SeasonMatches
            .Where(x => x.SeasonID == SeasonID && x.BracketMatch == null && x.HomeTeamID != null && x.AwayTeamID != null)
            .Select(x => x.SeasonMatchID).ToListAsync();

        var genes = await _service.GetTimeRecommendations(SeasonID);

        // OrderedCrossover used to copy one match twice and drop another (#175).
        Assert.That(genes.Select(g => g.SeasonMatchID), Is.Unique.And.EquivalentTo(expectedIDs));
    }

    [Test]
    public void LeagueTimeZone_IsUsEastern_WithDaylightSaving()
    {
        var zone = ScheduleService.LeagueTimeZone();

        Assert.Multiple(() =>
        {
            Assert.That(zone.GetUtcOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)), Is.EqualTo(TimeSpan.FromHours(-5)));
            Assert.That(zone.GetUtcOffset(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)), Is.EqualTo(TimeSpan.FromHours(-4)));
        });
    }
}
