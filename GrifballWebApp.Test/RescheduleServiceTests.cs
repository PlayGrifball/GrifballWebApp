using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Reschedule;
using GrifballWebApp.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using DiscordInterface.Generated;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RescheduleServiceTests
{
    private GrifballContext _context;
    private RescheduleService _service;
    private IDiscordRestClient _discordClient;
    private ILogger<RescheduleService> _logger;
    private IOptions<DiscordOptions> _options;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _logger = Substitute.For<ILogger<RescheduleService>>();
        _options = Substitute.For<IOptions<DiscordOptions>>();
        _options.Value.Returns(new DiscordOptions { ReschedulesChannel = 123UL });
        _service = new RescheduleService(_context, _discordClient, _logger, _options);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task RequestRescheduleAsync_ShouldCreateReschedule_WhenValid()
    {
        // Arrange: create a match
        var match = new SeasonMatch { ScheduledTime = DateTime.UtcNow.AddDays(1), Season = new() { SeasonName = "Test Season" } };
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var dto = new RescheduleRequestDto
        {
            SeasonMatchID = match.SeasonMatchID,
            NewScheduledTime = DateTime.UtcNow.AddDays(2),
            Reason = "Test reason"
        };
        var user = new User();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var reschedule = await _service.RequestRescheduleAsync(dto, user.Id);

        // Assert
        Assert.That(reschedule, Is.Not.Null);
        Assert.That(reschedule.SeasonMatchID, Is.EqualTo(match.SeasonMatchID));
        Assert.That(reschedule.Reason, Is.EqualTo("Test reason"));
        Assert.That(reschedule.Status, Is.EqualTo(RescheduleStatus.Pending));
    }

    [Test]
    public async Task ProcessRescheduleAsync_ShouldApproveReschedule()
    {
        // Arrange
        var match = new SeasonMatch { ScheduledTime = DateTime.UtcNow.AddDays(1), Season = new() { SeasonName = "Test Season" } };
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var user = new User();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var reschedule = new MatchReschedule
        {
            SeasonMatchID = match.SeasonMatchID,
            OriginalScheduledTime = match.ScheduledTime,
            NewScheduledTime = DateTime.UtcNow.AddDays(2),
            Reason = "Test reason",
            RequestedByUserID = user.Id,
            Status = RescheduleStatus.Pending
        };
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();
        match.ActiveRescheduleRequestId = reschedule.MatchRescheduleID;
        await _context.SaveChangesAsync();
        var dto = new ProcessRescheduleDto { Approved = true, CommissionerNotes = "Approved" };
        var commissionerId = user.Id;

        // Act
        var result = await _service.ProcessRescheduleAsync(reschedule.MatchRescheduleID, dto, commissionerId);

        // Assert
        Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Approved));
        Assert.That(result.ApprovedByUserID, Is.EqualTo(commissionerId));
        Assert.That(result.CommissionerNotes, Is.EqualTo("Approved"));
    }

    [Test]
    public async Task GetPendingReschedulesAsync_ShouldReturnPending()
    {
        // Arrange
        var match = new SeasonMatch { ScheduledTime = DateTime.UtcNow.AddDays(1), Season = new() { SeasonName = "Test Season" } };
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var user = new User();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var reschedule = new MatchReschedule
        {
            SeasonMatchID = match.SeasonMatchID,
            OriginalScheduledTime = match.ScheduledTime,
            NewScheduledTime = DateTime.UtcNow.AddDays(2),
            Reason = "Test reason",
            RequestedByUserID = user.Id,
            Status = RescheduleStatus.Pending
        };
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingReschedulesAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.GreaterThan(0));
        Assert.That(result[0].Status, Is.EqualTo(RescheduleStatus.Pending));
    }
}
