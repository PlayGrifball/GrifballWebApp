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
            foreach (var name in new[] { nameof(SeasonMatchController.ReportMatch), nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name)?.Roles, Is.EqualTo("Commissioner"), name);
            foreach (var name in new[] { nameof(SeasonMatchController.GetSeasonMatchPage), nameof(SeasonMatchController.GetPossibleMatches) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name), Is.Null, name);
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Null);
        });
    }
}
