using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using NSubstitute;
using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Reschedule;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleServiceTests
{
    private GrifballContext _context;
    private RescheduleService _service;
    private IDiscordRestClient _discordClient;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _service = new RescheduleService(_context, new ContextOutbox(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task RequestRescheduleAsync_ShouldCreateReschedule_WhenValid()
    {
        // Arrange: a match between two teams, asked for by one of their players
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddDays(1));
        var match = s.Match;
        var dto = new RescheduleRequestDto
        {
            SeasonMatchID = match.SeasonMatchID,
            NewScheduledTime = DateTime.UtcNow.AddDays(2),
            Reason = "Test reason"
        };

        // Act
        var reschedule = await _service.RequestRescheduleAsync(dto, s.Requester.Id);

        // Assert
        Assert.That(reschedule, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reschedule.SeasonMatchID, Is.EqualTo(match.SeasonMatchID));
            Assert.That(reschedule.Reason, Is.EqualTo("Test reason"));
            Assert.That(reschedule.Status, Is.EqualTo(RescheduleStatus.Pending));
        }
    }

    [Test]
    public async Task ProcessRescheduleAsync_ShouldApproveReschedule()
    {
        // Arrange: a pending request of a match between two teams
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddDays(1));
        var newTime = DateTime.UtcNow.AddDays(2);
        var reschedule = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: newTime);
        var dto = new ProcessRescheduleDto { Approved = true, CommissionerNotes = "Approved" };
        var commissionerId = s.HomeCaptain.Id;

        // Act
        var result = await _service.ProcessRescheduleAsync(reschedule.MatchRescheduleID, dto, commissionerId);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Approved));
            Assert.That(result.ApprovedByUserID, Is.EqualTo(commissionerId));
            Assert.That(result.CommissionerNotes, Is.EqualTo("Approved"));
            Assert.That(result.SeasonMatch.ScheduledTime, Is.EqualTo(newTime));
        }
    }

    [Test]
    public async Task GetPendingReschedulesAsync_ShouldReturnPending()
    {
        // Arrange
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddDays(1));
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: DateTime.UtcNow.AddDays(2));

        // Act
        var result = await _service.GetPendingReschedulesAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.GreaterThan(0));
        Assert.That(result[0].Status, Is.EqualTo(RescheduleStatus.Pending));
    }

    [Test]
    public async Task ProcessRescheduleAsync_ApprovingATimeATeamAlreadyPlays_IsRefused_AndChangesNothing()
    {
        // The home team already plays at the time asked for
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddDays(1));
        var clash = DateTime.UtcNow.AddDays(3);
        var other = SeasonMatch.Seed(s.Match.SeasonID, s.Match.HomeTeamID, s.Match.AwayTeamID, bestOf: 3, scheduledTime: clash);
        _context.SeasonMatches.Add(other);
        await _context.SaveChangesAsync();
        var reschedule = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: clash);

        var refused = await Assert.ThrowsAsync<RescheduleNotAllowedException>(() =>
            _service.ProcessRescheduleAsync(reschedule.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id));

        _context.ChangeTracker.Clear();
        var after = await _context.MatchReschedules.Include(r => r.SeasonMatch).SingleAsync(r => r.MatchRescheduleID == reschedule.MatchRescheduleID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused!.Message, Does.Contain("plays another match then"));
            Assert.That(after.Status, Is.EqualTo(RescheduleStatus.Pending), "Still waiting: another time, or a rejection");
            Assert.That(after.SeasonMatch.ScheduledTime, Is.Not.EqualTo(clash));
        }
    }

    [Test]
    public async Task ProcessRescheduleAsync_RejectingOne_ATeamAlreadyPlaysAt_IsFine()
    {
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddDays(1));
        var clash = DateTime.UtcNow.AddDays(3);
        _context.SeasonMatches.Add(SeasonMatch.Seed(s.Match.SeasonID, s.Match.HomeTeamID, s.Match.AwayTeamID, bestOf: 3, scheduledTime: clash));
        await _context.SaveChangesAsync();
        var reschedule = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: clash);

        var result = await _service.ProcessRescheduleAsync(reschedule.MatchRescheduleID, new ProcessRescheduleDto { Approved = false }, s.HomeCaptain.Id);

        Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Rejected));
    }
}
