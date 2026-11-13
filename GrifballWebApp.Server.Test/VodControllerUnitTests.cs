using System.Reflection;
using GrifballWebApp.Server.Historical.Vods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net;
using GrifballWebApp.Application.Historical.Vods;

namespace GrifballWebApp.Server.Test;

/// <summary>VodController's reflection-only unit tests: no database, host, file or network.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class VodControllerAttributeTests
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

/// <summary>VodControllerTests's unit tests: a real GrifballContext (temp-file SQLite, #402) for VodService's reads,
/// which touch nothing else. Its other tests stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodControllerUnitTests : SqliteGrifballContextFixture
{
    private VodController Controller() => new(new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!));

    [Test]
    public async Task ForSeasonMatch_ReturnsNone_WhenThereAreNoVods()
    {
        var result = await Controller().ForSeasonMatch(1, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Review_ReturnsNone_WhenNothingNeedsIt()
    {
        var result = await Controller().Review(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task LinkSeasons_ReturnsNone_WhenThereAreNoUnresolvedVods()
    {
        var result = await Controller().LinkSeasons(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task LinkOptions_RepliesNotFound_ForANonExistentSeason()
    {
        var result = await Controller().LinkOptions(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season");
    }
}
