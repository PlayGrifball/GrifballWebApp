using GeneticSharp;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ScheduleServiceTests
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
        var options = SeasonScheduleChromosomeTests.EveryDay(20, 21);
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
