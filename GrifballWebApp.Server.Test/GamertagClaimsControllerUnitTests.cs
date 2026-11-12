using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// GamertagClaimsController's unit tests: GamertagClaimService is IApplicationDbContext-backed (plus
/// IUserMergeService, substituted and never reached by these reads), on a real GrifballContext (temp-file SQLite,
/// #402). The lookup/duplicate actions stay GrifballWebApp.Integration.Test's.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimsControllerUnitTests : SqliteGrifballContextFixture
{
    private GamertagClaimsController Controller()
        => new(new GamertagClaimService(Db, Substitute.For<IUserMergeService>()), null!, null!);

    [Test]
    public async Task GetClaims_ReturnsNone_WhenThereAreNoPendingClaims()
    {
        var result = await Controller().GetClaims(GamertagClaimStatus.Pending, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task PastGamertags_RepliesNotFound_ForAnUnknownXboxAccount()
    {
        var result = await Controller().PastGamertags(xboxUserID: 12345, userID: null, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such Xbox account");
    }
}
