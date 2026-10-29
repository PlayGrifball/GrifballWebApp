using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Controllers;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections;
using System.Reflection;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchControllerTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _controller = new SeasonMatchController(new SeasonMatchService(_context, _dataPullService, Substitute.For<IBracketService>()));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetSeasonMatchPage_ReturnsNull_When_Missing()
    {
        Assert.That(await _controller.GetSeasonMatchPage(1234, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetSeasonMatchPage_ReturnsPage()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var page = await _controller.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        Assert.That(page!.HomeTeamName, Is.EqualTo("Home Team"));
    }

    [Test]
    public async Task ReportMatch_ReturnsBadRequest_ForEmptyGuid_WithoutCallingService()
    {
        var result = await _controller.ReportMatch(1, Guid.Empty, CancellationToken.None);

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("Provide valid Guid"));
        await _dataPullService.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
    }

    [Test]
    public async Task ReportMatch_ReturnsOk_AndLinksMatch()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var result = await _controller.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
        Assert.That(await _context.MatchLinks.AnyAsync(x => x.MatchID == match.MatchID), Is.True);
    }

    [Test]
    public async Task Forfeits_ReturnOk_AndSetResults()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var second = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(second);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var home = await _controller.HomeForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        var away = await _controller.AwayForfeit(second.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var r1 = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        var r2 = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == second.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(home, Is.InstanceOf<OkResult>());
            Assert.That(away, Is.InstanceOf<OkResult>());
            Assert.That(r1.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(r2.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
        });
    }

    [Test]
    public async Task DoubleForfeit_ReturnsOk_AndSetsBothResults()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.ChangeTracker.Clear();

        var result = await _controller.DoubleForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var sm = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<OkResult>());
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
        });
    }

    [TestCase(nameof(SeasonMatchController.HomeForfeit))]
    [TestCase(nameof(SeasonMatchController.AwayForfeit))]
    [TestCase(nameof(SeasonMatchController.DoubleForfeit))]
    public async Task Forfeits_ReturnBadRequest_ForTheGrandFinal(string action)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.Add(new MatchBracketInfo { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, RoundNumber = 3, MatchNumber = 4, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.GrandFinal });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var id = seeded.SeasonMatch.SeasonMatchID;

        var result = action switch
        {
            nameof(SeasonMatchController.HomeForfeit) => await _controller.HomeForfeit(id, CancellationToken.None),
            nameof(SeasonMatchController.AwayForfeit) => await _controller.AwayForfeit(id, CancellationToken.None),
            _ => await _controller.DoubleForfeit(id, CancellationToken.None),
        };

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("The grand final cannot be forfeited, report its games instead"));
    }

    [Test]
    public async Task UndoResult_ReturnsOk_AndClearsTheResult()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.ChangeTracker.Clear();
        await _controller.HomeForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        _context.ChangeTracker.Clear();

        var result = await _controller.UndoResult(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var sm = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<OkResult>());
            Assert.That((sm.HomeTeamResult, sm.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        });
    }

    [Test]
    public async Task UndoResult_ReturnsBadRequest_WithNothingToUndo()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _context.ChangeTracker.Clear();

        var result = await _controller.UndoResult(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("This match has no result to undo"));
    }

    [Test]
    public void CommissionerActions_ArePosts()
    {
        var type = typeof(SeasonMatchController);
        Assert.Multiple(() =>
        {
            foreach (var name in new[] { nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit), nameof(SeasonMatchController.DoubleForfeit), nameof(SeasonMatchController.UndoResult) })
            {
                Assert.That(ControllerTestHelpers.Action(type, name).GetCustomAttribute<HttpPostAttribute>(), Is.Not.Null, name);
                Assert.That(ControllerTestHelpers.Action(type, name).GetCustomAttribute<HttpGetAttribute>(), Is.Null, name);
            }
        });
    }

    [Test]
    public async Task GetPossibleMatches_DelegatesToService()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var result = await _controller.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        Assert.That(result, Is.Empty);
        await _dataPullService.ReceivedWithAnyArgs(1).DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public void Authorization_IsConfigured()
    {
        var type = typeof(SeasonMatchController);
        Assert.Multiple(() =>
        {
            foreach (var name in new[] { nameof(SeasonMatchController.ReportMatch), nameof(SeasonMatchController.GetPossibleMatches), nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit), nameof(SeasonMatchController.DoubleForfeit), nameof(SeasonMatchController.UndoResult) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name)?.Roles, Is.EqualTo("Commissioner"), name);
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(SeasonMatchController.GetSeasonMatchPage)), Is.Null);
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Null);
        });
    }
}
