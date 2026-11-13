using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// GamertagClaimsController's unit tests: GamertagClaimService, XuidLookupService and DuplicateSuggestionService are
/// all IApplicationDbContext-backed (plus IUserMergeService, substituted), on a real GrifballContext (temp-file
/// SQLite, #402). None of the reads here ever reach the Halo client.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimsControllerUnitTests : SqliteGrifballContextFixture
{
    private GamertagClaimsController Controller()
        => new(new GamertagClaimService(Db, Substitute.For<IUserMergeService>()),
            new XuidLookupService(Db, null!, new GamertagMatching(Db), Options.Create(new XuidLookupOptions()), TimeProvider.System),
            new XuidLookupSignal(),
            new DuplicateSuggestionService(Db, Substitute.For<IUserMergeService>(), TimeProvider.System));

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

    [Test]
    public async Task RemoveGamertagHistory_Throws_ForAnUnknownHistoryEntry()
    {
        await Assert.ThrowsAsync<GamertagClaimException>(() => Controller().RemoveGamertagHistory(999, CancellationToken.None));
    }

    [Test]
    public async Task ApproveClaim_Throws_ForAnUnknownClaim()
    {
        await Assert.ThrowsAsync<GamertagClaimException>(() => Controller().ApproveClaim(999, CancellationToken.None));
    }

    [Test]
    public async Task RejectClaim_Throws_ForAnUnknownClaim()
    {
        await Assert.ThrowsAsync<GamertagClaimException>(() => Controller().RejectClaim(999, request: null, CancellationToken.None));
    }

    [Test]
    public async Task AddGamertagHistory_AddsIt_ForAKnownXboxAccount()
    {
        await AddUser(123, "Someone");
        var request = new AddGamertagHistoryRequest { XboxUserID = 123, Gamertag = "Someone Else" };

        var result = await Controller().AddGamertagHistory(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status204NoContent));
            var saved = await Db.XboxUsers.PastGamertags().Where(h => h.XboxUserID == 123).ToListAsync();
            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0].Gamertag, Is.EqualTo("Someone Else"));
        }
    }

    [Test]
    public async Task XuidLookupStatus_ReturnsNoPlaceholders_ForAnEmptyDatabase()
    {
        var result = await Controller().XuidLookupStatus(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Placeholders, Is.Zero);
            Assert.That(result.Due, Is.Zero);
        }
    }

    [Test]
    public async Task LookUpXuids_AcceptsAndReportsStatus_ForAnEmptyDatabase()
    {
        var result = await Controller().LookUpXuids(CancellationToken.None);

        var accepted = (Microsoft.AspNetCore.Mvc.ObjectResult)result.Result!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(accepted.StatusCode, Is.EqualTo(StatusCodes.Status202Accepted));
            Assert.That(((XuidLookupStatusDto)accepted.Value!).Placeholders, Is.Zero);
        }
    }

    [Test]
    public async Task DuplicateSuggestions_ReturnsNone_ForAnEmptyDatabase()
    {
        var result = await Controller().DuplicateSuggestions(50, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task DismissDuplicate_RemembersThePair()
    {
        var result = await Controller().DismissDuplicate(new DuplicatePairRequest { SourceUserID = 1, TargetUserID = 2 }, CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status204NoContent));
        Assert.That(await Db.DuplicateDismissals.AnyAsync(), Is.True);
    }

    [Test]
    public async Task MergeDuplicate_Throws_ForAnUnknownUser()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(() => Controller().MergeDuplicate(new DuplicatePairRequest { SourceUserID = 999, TargetUserID = 1 }, CancellationToken.None));
    }
}
