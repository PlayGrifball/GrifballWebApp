using NSubstitute;
using GrifballWebApp.Server.SeasonMatchPage;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Infrastructure.SeasonMatchPage;

namespace GrifballWebApp.Server.Test;

/// <summary>SeasonMatchControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchControllerUnitTests
{
    private readonly IDataPullService _dataPullService = Substitute.For<IDataPullService>();
    private readonly SeasonMatchController _controller;

    public SeasonMatchControllerUnitTests()
    {
        // Refused before the service reads the database
        _controller = new SeasonMatchController(new SeasonMatchService(null!, _dataPullService, null!, null!));
    }

    [Test]
    public void CommissionerActions_ArePosts()
    {
        var type = typeof(SeasonMatchController);
        using (Assert.EnterMultipleScope())
        {
            foreach (var name in new[] { nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit), nameof(SeasonMatchController.DoubleForfeit), nameof(SeasonMatchController.UndoResult) })
            {
                Assert.That(ControllerTestHelpers.Action(type, name).GetCustomAttribute<HttpPostAttribute>(), Is.Not.Null, name);
                Assert.That(ControllerTestHelpers.Action(type, name).GetCustomAttribute<HttpGetAttribute>(), Is.Null, name);
            }
        }
    }

    [Test]
    public void Authorization_IsConfigured()
    {
        var type = typeof(SeasonMatchController);
        using (Assert.EnterMultipleScope())
        {
            foreach (var name in new[] { nameof(SeasonMatchController.ReportMatch), nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit), nameof(SeasonMatchController.DoubleForfeit), nameof(SeasonMatchController.UndoResult),
                nameof(SeasonMatchController.AddManualGame), nameof(SeasonMatchController.EditManualGame), nameof(SeasonMatchController.RemoveGame) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name)?.Roles, Is.EqualTo("Commissioner"), name);
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(SeasonMatchController.PullMatches))?.Roles, Is.EqualTo("Player,Commissioner,Sysadmin"));
            foreach (var name in new[] { nameof(SeasonMatchController.GetSeasonMatchPage), nameof(SeasonMatchController.GetPossibleMatches), nameof(SeasonMatchController.MatchPullStatus) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name), Is.Null, name);
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Null);
        }
    }

    [Test]
    public async Task ReportMatch_ReturnsBadRequest_ForEmptyGuid_WithoutCallingService()
    {
        var result = await _controller.ReportMatch(1, Guid.Empty, CancellationToken.None);

        result.AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest, "Provide valid Guid");
        await _dataPullService.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
    }
}

/// <summary>SeasonMatchControllerTests' unit tests on a real GrifballContext (temp-file SQLite, #402), where
/// SeasonMatchService's reads need IApplicationDbContext alone.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchControllerDatabaseUnitTests : SqliteGrifballContextFixture
{
    private SeasonMatchController Controller() => new(new SeasonMatchService(Db, NSubstitute.Substitute.For<IDataPullService>(),
        NSubstitute.Substitute.For<GrifballWebApp.Application.Brackets.IBracketService>(), new GrifballWebApp.Application.Championships.ChampionshipService(Db)));

    [Test]
    public async Task GetSeasonMatchPage_RepliesNotFound_ForANonExistentMatch()
    {
        var result = await Controller().GetSeasonMatchPage(999, CancellationToken.None);

        result.Result.AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound, "There is no such season match");
    }

    [Test]
    public async Task GetPossibleMatches_ReturnsNone_ForANonExistentMatch()
    {
        var result = await Controller().GetPossibleMatches(999, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
