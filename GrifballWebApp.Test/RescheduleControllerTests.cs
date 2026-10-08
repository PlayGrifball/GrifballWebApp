using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using System.Security.Claims;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RescheduleControllerTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private RescheduleController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { ReschedulesChannel = 1UL });
        var service = new RescheduleService(_context, _discordClient, Substitute.For<ILogger<RescheduleService>>(), options);
        _controller = new RescheduleController(service);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private void SetUser(int id) => _controller.WithUser(id.ToString());

    private static object? Prop(object? value, string name) => value?.GetType().GetProperty(name)?.GetValue(value);

    [Test]
    public async Task RequestReschedule_ReturnsOk_WithId()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        SetUser(s.Requester.Id);

        var result = await _controller.RequestReschedule(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var id = (int)Prop(ok!.Value, "RescheduleId")!;
        Assert.That(Prop(ok.Value, "Message"), Is.EqualTo("Reschedule request submitted successfully"));
        Assert.That((await _context.MatchReschedules.SingleAsync()).MatchRescheduleID, Is.EqualTo(id));
        Assert.That((await _context.MatchReschedules.SingleAsync()).RequestedByUserID, Is.EqualTo(s.Requester.Id));
    }

    [Test]
    public async Task RequestReschedule_ReturnsBadRequest_When_MatchMissing()
    {
        SetUser(1);
        var result = await _controller.RequestReschedule(new RescheduleRequestDto { SeasonMatchID = 999, Reason = "r" }, CancellationToken.None);
        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("Season match not found"));
    }

    [Test]
    public async Task RequestReschedule_PropagatesInvalidOperation_When_AlreadyActive()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        SetUser(s.Requester.Id);
        await _controller.RequestReschedule(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None);

        // Only ArgumentException is mapped to 400; the "already active" InvalidOperationException escapes the action (500).
        Assert.ThrowsAsync<InvalidOperationException>(() => _controller.RequestReschedule(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None));
    }

    [TestCase(true, "approved")]
    [TestCase(false, "rejected")]
    public async Task ProcessReschedule_ReturnsOk(bool approved, string word)
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        SetUser(s.HomeCaptain.Id);

        var result = await _controller.ProcessReschedule(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = approved }, CancellationToken.None);

        Assert.That(Prop((result as OkObjectResult)?.Value, "Message"), Is.EqualTo($"Reschedule request {word} successfully"));
    }

    [Test]
    public async Task ProcessReschedule_ReturnsNotFound_When_Missing()
    {
        SetUser(1);
        var result = await _controller.ProcessReschedule(999, new ProcessRescheduleDto { Approved = true }, CancellationToken.None);
        Assert.That((result as NotFoundObjectResult)?.Value, Is.EqualTo("Reschedule request not found"));
    }

    [Test]
    public async Task ProcessReschedule_ReturnsBadRequest_When_AlreadyProcessed()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, RescheduleStatus.Approved);
        SetUser(s.HomeCaptain.Id);

        var result = await _controller.ProcessReschedule(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, CancellationToken.None);

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("Reschedule request has already been processed"));
    }

    [Test]
    public async Task GetPendingReschedules_ReturnsOkWithList()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);

        var result = await _controller.GetPendingReschedules(CancellationToken.None);

        var list = (result as OkObjectResult)?.Value as List<RescheduleDto>;
        Assert.That(list, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CreateDiscordThread_ReturnsOk_When_ThreadAlreadyExists()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 3UL);

        var result = await _controller.CreateDiscordThread(r.MatchRescheduleID, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public void Authorization_IsConfigured()
    {
        var type = typeof(RescheduleController);
        Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Not.Null, "controller requires authentication");
        Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(RescheduleController.RequestReschedule)), Is.Null, "any logged in user can request");
        foreach (var name in new[] { nameof(RescheduleController.ProcessReschedule), nameof(RescheduleController.GetPendingReschedules), nameof(RescheduleController.CreateDiscordThread) })
        {
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, name)?.Roles, Is.EqualTo("Commissioner,Sysadmin"), name);
        }
    }
}
