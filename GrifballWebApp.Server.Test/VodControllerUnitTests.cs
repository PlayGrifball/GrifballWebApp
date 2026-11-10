using System.Reflection;
using GrifballWebApp.Server.Historical.Vods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Net;
using GrifballWebApp.Application.Historical.Vods;

namespace GrifballWebApp.Server.Test;

/// <summary>VodControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodControllerUnitTests
{
    [TestCase(nameof(VodController.Review), true)]
    [TestCase(nameof(VodController.LinkSeasons), true)]
    [TestCase(nameof(VodController.LinkOptions), true)]
    [TestCase(nameof(VodController.Link), true)]
    [TestCase(nameof(VodController.Dismiss), true)]
    [TestCase(nameof(VodController.ForSeason), false)]
    [TestCase(nameof(VodController.ForSeasonMatch), false)]
    [TestCase(nameof(VodController.ForTeam), false)]
    [TestCase(nameof(VodController.ForPlayer), false)]
    public void OnlyTheReview_IsForSysadmins(string action, bool sysadmin)
    {
        var authorize = typeof(VodController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.That(authorize?.Roles, sysadmin ? (NUnit.Framework.Constraints.IResolveConstraint)Is.EqualTo("Sysadmin") : Is.Null);
    }
}
