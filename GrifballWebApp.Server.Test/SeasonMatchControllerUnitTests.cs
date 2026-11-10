using GrifballWebApp.Server.SeasonMatchPage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using System.Reflection;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;

namespace GrifballWebApp.Server.Test;

/// <summary>SeasonMatchControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchControllerUnitTests
{
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
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(SeasonMatchController.PullMatches))?.Roles, Is.EqualTo("Commissioner,Sysadmin"));
            foreach (var name in new[] { nameof(SeasonMatchController.GetSeasonMatchPage), nameof(SeasonMatchController.GetPossibleMatches), nameof(SeasonMatchController.MatchPullStatus) })
                Assert.That(ControllerTestHelpers.ActionAuthorize(type, name), Is.Null, name);
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Null);
        }
    }
}
