using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>ScheduleRecommendations against the fake IApplicationDbContext: saving a genetic-algorithm run as a
/// recommendation, reading the latest one back, and applying it to the season's matches.</summary>
[TestFixture]
internal sealed class ScheduleRecommendationsUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ProposedTime = new(2026, 6, 8, 18, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private ScheduleRecommendations _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new ScheduleRecommendations(Context, _time);
    }

    private async Task<(Team Home, Team Away, SeasonMatch Match)> SeedMatch()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        var home = Team.Seed(seasonID: SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: SeasonID, teamName: "Away");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();
        var match = SeasonMatch.Between(SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        return (home, away, match);
    }

    private static AvailabilityOption[] Options(DateTime time, params int[] availableTeamIDs) =>
        new[] { AvailabilityOption.Seed(dayOfWeek: time.DayOfWeek, time: TimeOnly.FromDateTime(time), teamAvailability: availableTeamIDs.Select(id => TeamAvailability.Seed(id))) };

    [Test]
    public async Task Save_StoresProposedTimesWithOverlapAndAvailabilityFlags()
    {
        var (home, away, match) = await SeedMatch();
        var gene = new SeasonMatchGene { SeasonMatchID = match.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        // Only the home team is available at this slot
        var run = new ScheduleRun([gene], 0.75, 42, Options(ProposedTime, home.TeamID));

        var saved = await _service.Save(SeasonID, run, "commissioner");

        Context.ChangeTracker.Clear();
        var reloaded = await Context.ScheduleRecommendations.Include(r => r.Matches).SingleAsync(r => r.ScheduleRecommendationID == saved.ScheduleRecommendationID);
        var proposedMatch = reloaded.Matches.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.Fitness, Is.EqualTo(0.75));
            Assert.That(reloaded.Generations, Is.EqualTo(42));
            Assert.That(reloaded.RequestedBy, Is.EqualTo("commissioner"));
            Assert.That(reloaded.AppliedAt, Is.Null);
            Assert.That(proposedMatch.SeasonMatchID, Is.EqualTo(match.SeasonMatchID));
            Assert.That(proposedMatch.ScheduledTime, Is.EqualTo(ProposedTime));
            Assert.That(proposedMatch.Overlaps, Is.False);
            Assert.That(proposedMatch.HomeTeamAvailable, Is.True);
            Assert.That(proposedMatch.AwayTeamAvailable, Is.False);
        }
    }

    [Test]
    public async Task Save_TwoOverlappingMatchesForTheSameTeam_FlagsBothAsOverlapping()
    {
        var (home, away, match) = await SeedMatch();
        var third = Team.Seed(seasonID: SeasonID, teamName: "Third");
        Context.Teams.Add(third);
        await Context.SaveChangesAsync();
        var otherMatch = SeasonMatch.Between(SeasonID, home.TeamID, third.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(otherMatch);
        await Context.SaveChangesAsync();
        var geneA = new SeasonMatchGene { SeasonMatchID = match.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        // Home plays again at the same time: both proposals overlap
        var geneB = new SeasonMatchGene { SeasonMatchID = otherMatch.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = third.TeamID, ScheduledTime = ProposedTime };
        var run = new ScheduleRun([geneA, geneB], 0.1, 5, Options(ProposedTime, home.TeamID, away.TeamID, third.TeamID));

        var saved = await _service.Save(SeasonID, run, null);

        Context.ChangeTracker.Clear();
        var reloaded = await Context.ScheduleRecommendations.Include(r => r.Matches).SingleAsync(r => r.ScheduleRecommendationID == saved.ScheduleRecommendationID);
        Assert.That(reloaded.Matches.All(m => m.Overlaps), Is.True);
    }

    [Test]
    public async Task Latest_NoRecommendations_ReturnsNull()
    {
        var latest = await _service.Latest(SeasonID);

        Assert.That(latest, Is.Null);
    }

    [Test]
    public async Task Latest_ReturnsNewestWithCurrentTimeAndExcludesDeletedMatches()
    {
        var (home, away, match) = await SeedMatch();
        var firstGene = new SeasonMatchGene { SeasonMatchID = match.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        await _service.Save(SeasonID, new ScheduleRun([firstGene], 0.5, 10, Options(ProposedTime, home.TeamID, away.TeamID)), "first");
        // The second, newer run also proposes a match that no longer exists: left out of the DTO
        var deletedMatchGene = new SeasonMatchGene { SeasonMatchID = 999999, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        await _service.Save(SeasonID, new ScheduleRun([firstGene, deletedMatchGene], 0.9, 20, Options(ProposedTime, home.TeamID, away.TeamID)), "second");

        var latest = await _service.Latest(SeasonID);

        Assert.That(latest, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(latest!.RequestedBy, Is.EqualTo("second"));
            Assert.That(latest.Matches, Has.Count.EqualTo(1), "the deleted match's proposal is left out");
            Assert.That(latest.Matches[0].HomeTeam, Is.EqualTo("Home"));
            Assert.That(latest.Matches[0].AwayTeam, Is.EqualTo("Away"));
            Assert.That(latest.Matches[0].CurrentTime, Is.Null, "the match itself is still unscheduled");
            Assert.That(latest.Matches[0].Decided, Is.False);
        }
    }

    [Test]
    public async Task Apply_NoSuchRecommendation_ReturnsNull()
    {
        var result = await _service.Apply(999, "commissioner");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Apply_SetsUndecidedMatchesToProposedTimeAndStampsRecommendation()
    {
        var (home, away, match) = await SeedMatch();
        var gene = new SeasonMatchGene { SeasonMatchID = match.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        var saved = await _service.Save(SeasonID, new ScheduleRun([gene], 0.9, 30, Options(ProposedTime, home.TeamID, away.TeamID)), "commissioner");

        var count = await _service.Apply(saved.ScheduleRecommendationID, "applier");

        // Apply stamps the row with ExecuteUpdate, which bypasses the change tracker: read it back fresh
        Context.ChangeTracker.Clear();
        var updatedMatch = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == match.SeasonMatchID);
        var reloaded = await Context.ScheduleRecommendations.SingleAsync(r => r.ScheduleRecommendationID == saved.ScheduleRecommendationID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(updatedMatch.ScheduledTime, Is.EqualTo(ProposedTime));
            Assert.That(reloaded.AppliedBy, Is.EqualTo("applier"));
            Assert.That(reloaded.AppliedAt, Is.EqualTo(Now));
        }
    }

    [Test]
    public async Task Apply_AlreadyApplied_ReturnsNullAndDoesNotReapply()
    {
        var (home, away, match) = await SeedMatch();
        var gene = new SeasonMatchGene { SeasonMatchID = match.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        var saved = await _service.Save(SeasonID, new ScheduleRun([gene], 0.9, 30, Options(ProposedTime, home.TeamID, away.TeamID)), "commissioner");
        await _service.Apply(saved.ScheduleRecommendationID, "first");

        var result = await _service.Apply(saved.ScheduleRecommendationID, "second");

        // Apply stamps the row with ExecuteUpdate, which bypasses the change tracker: read it back fresh
        Context.ChangeTracker.Clear();
        var reloaded = await Context.ScheduleRecommendations.SingleAsync(r => r.ScheduleRecommendationID == saved.ScheduleRecommendationID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(reloaded.AppliedBy, Is.EqualTo("first"), "the second Apply must not have overwritten it");
        }
    }

    [Test]
    public async Task Apply_SkipsAlreadyDecidedMatches()
    {
        var (home, away, match) = await SeedMatch();
        match.WonBy(SeasonMatchSide.Home); // decided already: Apply must leave its time alone
        await Context.SaveChangesAsync();
        var gene = new SeasonMatchGene { SeasonMatchID = match.SeasonMatchID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, ScheduledTime = ProposedTime };
        var saved = await _service.Save(SeasonID, new ScheduleRun([gene], 0.9, 30, Options(ProposedTime, home.TeamID, away.TeamID)), "commissioner");

        var count = await _service.Apply(saved.ScheduleRecommendationID, "applier");

        Context.ChangeTracker.Clear();
        var updatedMatch = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == match.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(count, Is.Zero);
            Assert.That(updatedMatch.ScheduledTime, Is.Null);
        }
    }
}
