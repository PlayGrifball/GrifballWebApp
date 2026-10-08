using GeneticSharp;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Test.CovA;

[TestFixture]
public class SeasonMatchGeneTests_A
{
    private static readonly DateTime Monday20 = new(2024, 1, 1, 20, 0, 0); // 2024-01-01 is a Monday

    private static SeasonMatchGene Gene(int id, int home, int away, DateTime at) =>
        new() { SeasonMatchID = id, HomeTeamID = home, AwayTeamID = away, ScheduledTime = at };

    private static AvailabilityOption Option(DayOfWeek day, int hour, params int[] teamIDs)
    {
        var option = new AvailabilityOption { DayOfWeek = day, Time = new TimeOnly(hour, 0) };
        foreach (var t in teamIDs)
            option.TeamAvailability.Add(new TeamAvailability { TeamID = t });
        return option;
    }

    [Test]
    public void EndsAt_IsOneTickBeforeThirtyMinutes()
    {
        var g = Gene(1, 1, 2, Monday20);
        Assert.That(g.EndsAt, Is.EqualTo(Monday20.AddMinutes(30).AddTicks(-1)));
    }

    [TestCase(1, 2, 0, true, Description = "same teams, same time")]
    [TestCase(2, 3, 15, true, Description = "shares team 2 (as home), overlapping time")]
    [TestCase(3, 1, 29, true, Description = "shares team 1 (as away), overlapping time")]
    [TestCase(1, 5, 0, true, Description = "shares home team")]
    [TestCase(5, 2, 0, true, Description = "shares away team")]
    [TestCase(1, 2, 30, false, Description = "back to back is not an overlap")]
    [TestCase(1, 2, -30, false, Description = "back to back before is not an overlap")]
    [TestCase(3, 4, 0, false, Description = "different teams at the same time")]
    public void HasOverlap(int home, int away, int minutesOffset, bool expected)
    {
        var a = Gene(1, 1, 2, Monday20);
        var b = Gene(2, home, away, Monday20.AddMinutes(minutesOffset));

        Assert.That(a.HasOverlap(b), Is.EqualTo(expected));
        Assert.That(b.HasOverlap(a), Is.EqualTo(expected), "overlap is symmetric");
    }

    [Test]
    public void GetOverlap_ExcludesSelfAndNonOverlapping()
    {
        var a = Gene(1, 1, 2, Monday20);
        var b = Gene(2, 2, 3, Monday20.AddMinutes(10));
        var c = Gene(3, 4, 5, Monday20);
        var list = new List<SeasonMatchGene> { a, b, c };

        Assert.Multiple(() =>
        {
            Assert.That(a.GetOverlap(list), Is.EqualTo(new[] { b }));
            Assert.That(a.AnyOverlap(list), Is.True);
            Assert.That(c.AnyOverlap(list), Is.False);
            Assert.That(a.AnyOverlap([a]), Is.False);
        });
    }

    [Test]
    public void TeamAvailable_UsesOptionMatchingDayAndTime()
    {
        var options = new[]
        {
            Option(DayOfWeek.Monday, 20, 1),
            Option(DayOfWeek.Monday, 21, 2),
            Option(DayOfWeek.Tuesday, 20, 2),
        };
        var g = Gene(1, 1, 2, Monday20);

        Assert.Multiple(() =>
        {
            Assert.That(g.HomeTeamAvailable(options), Is.True);
            Assert.That(g.AwayTeamAvailable(options), Is.False);
            var later = Gene(1, 1, 2, Monday20.AddHours(1));
            Assert.That(later.HomeTeamAvailable(options), Is.False);
            Assert.That(later.AwayTeamAvailable(options), Is.True);
        });
    }

    [Test]
    public void TeamAvailable_NoOptionForSlot_Throws()
    {
        var g = Gene(1, 1, 2, Monday20.AddMinutes(30));
        var options = new[] { Option(DayOfWeek.Monday, 20, 1, 2) };

        Assert.That(Assert.Throws<Exception>(() => g.HomeTeamAvailable(options))!.Message, Is.EqualTo("Missing option"));
        Assert.That(Assert.Throws<Exception>(() => g.AwayTeamAvailable(options))!.Message, Is.EqualTo("Missing option"));
    }
}

[TestFixture]
public class SeasonScheduleChromosomeTests_A
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

[TestFixture]
public class SeasonScheduleEvaluatorTests_A
{
    private static readonly DateTime Monday20 = new(2024, 1, 1, 20, 0, 0);

    private static SeasonScheduleChromosome WithGenes(AvailabilityOption[] options, params SeasonMatchGene[] genes)
    {
        var matches = genes.Select(g => new SeasonMatch { SeasonMatchID = g.SeasonMatchID, HomeTeamID = g.HomeTeamID, AwayTeamID = g.AwayTeamID }).ToList();
        var chromosome = new SeasonScheduleChromosome(matches, matches.Count, Monday20.Date, Monday20.Date.AddDays(7), options);
        for (var i = 0; i < genes.Length; i++)
            chromosome.ReplaceGene(i, new Gene(genes[i]));
        return chromosome;
    }

    private static SeasonMatchGene Gene(int id, int home, int away, DateTime at) =>
        new() { SeasonMatchID = id, HomeTeamID = home, AwayTeamID = away, ScheduledTime = at };

    private static AvailabilityOption[] OptionsAvailableFor(params int[] teamIDs)
    {
        var options = SeasonScheduleChromosomeTests_A.EveryDay(20, 21);
        foreach (var o in options)
            foreach (var t in teamIDs)
                o.TeamAvailability.Add(new TeamAvailability { TeamID = t });
        return options;
    }

    [Test]
    public void Evaluate_PerfectSchedule_IsOne()
    {
        var options = OptionsAvailableFor(1, 2, 3, 4);
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20), Gene(3, 1, 3, Monday20.AddHours(1)));

        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_ManyPenalties_DecreasesFitness()
    {
        // Overlap (counted from both genes) and nobody available: score = 1 - 2 - 4 = -5
        var options = OptionsAvailableFor();
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 2, 3, Monday20));

        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(1d / 5).Within(1e-9));
    }

    [Test]
    public void Evaluate_ExactlyOnePenalty_IsInfinite()
    {
        var options = OptionsAvailableFor(1, 2, 3);
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20)); // away team 4 unavailable

        // BUG: SeasonScheduleEvaluator returns |score|^-1 with score starting at 1, so one penalty gives 0^-1 = +Infinity
        // (fitter than a perfect schedule) and two penalties give |-1|^-1 = 1 (as fit as a perfect schedule).
        // Expected: fitness strictly decreases with penalties.
        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(double.PositiveInfinity));
    }

    [Test]
    public void Evaluate_TwoPenalties_EqualsPerfectFitness()
    {
        var options = OptionsAvailableFor(1, 2);
        var chromosome = WithGenes(options, Gene(1, 1, 2, Monday20), Gene(2, 3, 4, Monday20)); // teams 3 and 4 unavailable

        // BUG: see Evaluate_ExactlyOnePenalty_IsInfinite
        Assert.That(new SeasonScheduleEvaluator(options).Evaluate(chromosome), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_OtherChromosome_Throws()
    {
        var other = Substitute.For<IChromosome>();
        var ex = Assert.Throws<Exception>(() => new SeasonScheduleEvaluator([]).Evaluate(other));
        Assert.That(ex!.Message, Is.EqualTo("SeasonScheduleEvaluator can only evaluate SeasonScheduleChromosome"));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ScheduleServiceTests_A
{
    private const int SeasonID = 4; // ScheduleService is hardcoded to season 4
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
            _context.Seasons.Add(new Season
            {
                SeasonID = SeasonID,
                SeasonName = "S4",
                SeasonStart = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                SeasonEnd = new DateTime(2024, 1, 29, 12, 0, 0, DateTimeKind.Utc),
            });
            await _context.SaveChangesAsync();
            await _context.EnableContraints("[Event].[Seasons]");
            await tx.CommitAsync();
        }

        var teams = Enumerable.Range(1, 4).Select(i => new Team { SeasonID = SeasonID, TeamName = $"T{i}" }).ToList();
        _context.Teams.AddRange(teams);
        var options = SeasonScheduleChromosomeTests_A.EveryDay(20, 21);
        _context.Availability.AddRange(options);
        await _context.SaveChangesAsync();
        foreach (var o in options)
            foreach (var t in teams)
                _context.TeamAvailability.Add(new TeamAvailability { TeamID = t.TeamID, AvailabilityOptionID = o.AvailabilityOptionID });

        _context.SeasonMatches.AddRange(
            new SeasonMatch { SeasonID = SeasonID, HomeTeamID = teams[0].TeamID, AwayTeamID = teams[1].TeamID },
            new SeasonMatch { SeasonID = SeasonID, HomeTeamID = teams[2].TeamID, AwayTeamID = teams[3].TeamID },
            new SeasonMatch { SeasonID = SeasonID, HomeTeamID = teams[0].TeamID, AwayTeamID = teams[2].TeamID },
            // Excluded: missing a team
            new SeasonMatch { SeasonID = SeasonID, HomeTeamID = teams[0].TeamID });
        await _context.SaveChangesAsync();
        // Excluded: playoff match
        _context.SeasonMatches.Add(new SeasonMatch
        {
            SeasonID = SeasonID, HomeTeamID = teams[1].TeamID, AwayTeamID = teams[3].TeamID,
            BracketMatch = new MatchBracketInfo { MatchNumber = 1, RoundNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2 },
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return teams;
    }

    [Test]
    public void GetTimeRecommendations_NoSeason4_Throws()
    {
        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetTimeRecommendations());
        Assert.That(ex!.Message, Is.EqualTo("Season not found"));
    }

    [Test]
    public async Task GetTimeRecommendations_SchedulesEveryRegularSeasonMatchInSeasonWindow()
    {
        var teams = await Arrange();
        var expectedIDs = await _context.SeasonMatches
            .Where(x => x.BracketMatch == null && x.HomeTeamID != null && x.AwayTeamID != null)
            .Select(x => x.SeasonMatchID).ToListAsync();

        var genes = await _service.GetTimeRecommendations();

        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var start = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), eastern);
        var end = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2024, 1, 29, 12, 0, 0, DateTimeKind.Utc), eastern);
        Assert.Multiple(() =>
        {
            Assert.That(genes.Select(g => g.SeasonMatchID), Is.EquivalentTo(expectedIDs));
            Assert.That(genes.All(g => teams.Any(t => t.TeamID == g.HomeTeamID) && teams.Any(t => t.TeamID == g.AwayTeamID)), Is.True);
            // Times are on a day in the season window (the date is random, the time is snapped to an option)
            Assert.That(genes.All(g => g.ScheduledTime >= start.Date && g.ScheduledTime <= end), Is.True);
            Assert.That(genes.All(g => g.ScheduledTime.Hour is 20 or 21 && g.ScheduledTime.Minute == 0), Is.True);
        });
    }

    [Test]
    public async Task GetTimeRecommendations_IncludesMatchesFromOtherSeasons()
    {
        await Arrange();
        var otherSeason = new Season { SeasonName = "Other" };
        _context.Seasons.Add(otherSeason);
        await _context.SaveChangesAsync();
        var a = new Team { SeasonID = otherSeason.SeasonID, TeamName = "OA" };
        var b = new Team { SeasonID = otherSeason.SeasonID, TeamName = "OB" };
        _context.Teams.AddRange(a, b);
        await _context.SaveChangesAsync();
        var other = new SeasonMatch { SeasonID = otherSeason.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = b.TeamID };
        _context.SeasonMatches.Add(other);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var genes = await _service.GetTimeRecommendations();

        // BUG: ScheduleService.GetTimeRecommendations loads season 4 but does not filter SeasonMatches by SeasonID,
        // so regular season matches from every season are scheduled. Expected: only season 4 matches.
        Assert.That(genes.Select(g => g.SeasonMatchID), Does.Contain(other.SeasonMatchID));
        Assert.That(genes, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task GetTimeRecommendations_CancelledToken_Throws()
    {
        await Arrange();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await _service.GetTimeRecommendations(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task SchedulerController_DelegatesToService()
    {
        await Arrange();
        var controller = new SchedulerController(_context, _service);

        var genes = await controller.GetTimeRecommendations(CancellationToken.None);

        Assert.That(genes, Has.Count.EqualTo(3));
    }
}
