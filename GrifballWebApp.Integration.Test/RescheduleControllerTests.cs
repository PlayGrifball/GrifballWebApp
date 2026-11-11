using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Reschedule;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleControllerTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private RescheduleController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        var service = new RescheduleService(_context, new ContextOutbox(_context));
        _controller = new RescheduleController(service);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private void SetUser(int id) => _controller.WithUser(id.ToString());


    [Test]
    public async Task RequestReschedule_IsCreated_WithId()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        SetUser(s.Requester.Id);

        var result = await _controller.RequestReschedule(s.Match.SeasonMatchID, new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None);

        var created = result as CreatedAtRouteResult;
        Assert.That(created, Is.Not.Null);
        var id = ((RescheduleRequestedDto)created!.Value!).RescheduleID;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.RouteName, Is.EqualTo("GetSeasonMatchPage"));
            Assert.That((await _context.MatchReschedules.SingleAsync()).MatchRescheduleID, Is.EqualTo(id));
            Assert.That((await _context.MatchReschedules.SingleAsync()).RequestedByUserID, Is.EqualTo(s.Requester.Id));
        }
    }

    [Test]
    public async Task RequestReschedule_ReturnsBadRequest_When_MatchMissing()
    {
        SetUser(1);
        var result = await _controller.RequestReschedule(999, new RescheduleRequestDto { SeasonMatchID = 999, Reason = "r" }, CancellationToken.None);
        result.AssertProblem(StatusCodes.Status400BadRequest, "Season match not found");
    }

    [Test]
    public async Task RequestReschedule_IsRefused_When_AlreadyActive()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        SetUser(s.Requester.Id);
        await _controller.RequestReschedule(s.Match.SeasonMatchID, new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None);

        // GlobalExceptionHandler answers it as a 409 ProblemDetails
        Assert.That(() => _controller.RequestReschedule(s.Match.SeasonMatchID, new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None),
            Throws.InstanceOf<RescheduleAlreadyActiveException>());
    }

    [Test]
    public async Task RequestReschedule_IsRefused_When_MatchHasAResult()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        s.Match.WonBy(SeasonMatchSide.Home);
        await _context.SaveChangesAsync();
        SetUser(s.Requester.Id);

        // GlobalExceptionHandler answers it as a 400 ProblemDetails
        Assert.That(() => _controller.RequestReschedule(s.Match.SeasonMatchID, new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None),
            Throws.InstanceOf<RescheduleNotAllowedException>().With.Message.EqualTo("This match already has a result."));
    }

    [Test]
    public async Task RequestReschedule_IsForbidden_When_UserIsOnNeitherTeam()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var outsider = await RescheduleTestData.AddUser(_context, "outsider");
        SetUser(outsider.Id);

        // GlobalExceptionHandler answers it as a 403 ProblemDetails
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => _controller.RequestReschedule(s.Match.SeasonMatchID, new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "r" }, CancellationToken.None),
                Throws.InstanceOf<RescheduleForbiddenException>());
            Assert.That(await _context.MatchReschedules.AnyAsync(), Is.False);
        }
    }

    [TestCase(true, "approved")]
    [TestCase(false, "rejected")]
    public async Task ProcessReschedule_ReturnsOk(bool approved, string word)
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        SetUser(s.HomeCaptain.Id);

        var result = await _controller.ProcessReschedule(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = approved }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NoContentResult>(), $"Reschedule request {word}");
    }

    [Test]
    public async Task ProcessReschedule_ReturnsNotFound_When_Missing()
    {
        SetUser(1);
        var result = await _controller.ProcessReschedule(999, new ProcessRescheduleDto { Approved = true }, CancellationToken.None);
        result.AssertProblem(StatusCodes.Status404NotFound, "Reschedule request not found");
    }

    [Test]
    public async Task ProcessReschedule_IsRefused_When_ApprovingADecidedMatch()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        // Decided past the model, as a report racing the request's decision leaves it: the match's own decision would
        // close its pending request (#297)
        var decided = _context.Entry(s.Match);
        decided.Property(m => m.HomeTeamResult).CurrentValue = SeasonMatchResult.Won;
        decided.Property(m => m.AwayTeamResult).CurrentValue = SeasonMatchResult.Loss;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        SetUser(s.HomeCaptain.Id);

        // GlobalExceptionHandler answers it as a 400 ProblemDetails
        Assert.That(() => _controller.ProcessReschedule(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, CancellationToken.None),
            Throws.InstanceOf<RescheduleNotAllowedException>().With.Message.EqualTo("This match already has a result."));
    }

    [Test]
    public async Task ProcessReschedule_IsRefused_When_AlreadyProcessed()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, RescheduleStatus.Approved);
        SetUser(s.HomeCaptain.Id);

        // GlobalExceptionHandler answers it as a 409 ProblemDetails
        Assert.That(() => _controller.ProcessReschedule(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, CancellationToken.None),
            Throws.InstanceOf<RescheduleAlreadyProcessedException>());
    }

    [Test]
    public async Task GetPendingReschedules_ReturnsOkWithList()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);

        var result = await _controller.GetPendingReschedules(CancellationToken.None);

        var list = result;
        Assert.That(list, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CreateDiscordThread_ReturnsOk_When_ThreadAlreadyExists()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 3UL);

        var result = await _controller.CreateDiscordThread(r.MatchRescheduleID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
        }
    }
}
