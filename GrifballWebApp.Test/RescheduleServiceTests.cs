using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Reschedule;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using DiscordInterface.Generated;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        _service = new RescheduleService(_context, new ContextOutbox(_context), _logger, _options);
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
        Assert.That(reschedule.SeasonMatchID, Is.EqualTo(match.SeasonMatchID));
        Assert.That(reschedule.Reason, Is.EqualTo("Test reason"));
        Assert.That(reschedule.Status, Is.EqualTo(RescheduleStatus.Pending));
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
        Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Approved));
        Assert.That(result.ApprovedByUserID, Is.EqualTo(commissionerId));
        Assert.That(result.CommissionerNotes, Is.EqualTo("Approved"));
        Assert.That(result.SeasonMatch.ScheduledTime, Is.EqualTo(newTime));
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
        Assert.That(result.Count, Is.GreaterThan(0));
        Assert.That(result[0].Status, Is.EqualTo(RescheduleStatus.Pending));
    }
}
