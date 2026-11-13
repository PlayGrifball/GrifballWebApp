using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// GamertagClaimService's paths GamertagClaimServiceUnitTests leaves: a Discord match's approval (its merge trigger), a
/// claim whose placeholder is no longer an unlinked one (409, rolled back), and the gamertag history's refusals.
/// </summary>
[TestFixture]
internal sealed class GamertagClaimServiceMoreUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private IUserMergeService _mergeService = null!;
    private GamertagClaimService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mergeService = Substitute.For<IUserMergeService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.GetCurrentUserId().Returns(9);
        _service = new GamertagClaimService(Context, _mergeService, currentUser, new FakeTimeProvider(Now));
    }

    [Test]
    public async Task Approve_ADiscordMatch_MergesThePlaceholderIntoTheClaimant_AsADiscordMatch()
    {
        var claimant = User.Seed(userName: "claimant", displayName: "Claimant");
        Context.Users.Add(claimant);
        await Context.SaveChangesAsync();
        var placeholder = User.ImportPlaceholder("historical:OldName", "OldName", null);
        Context.Users.Add(placeholder);
        await Context.SaveChangesAsync();
        var claim = GamertagClaim.FromDiscordMatch(claimant.Id, null, "OldName", "OLDNAME", placeholder.Id);
        Context.GamertagClaims.Add(claim);
        await Context.SaveChangesAsync();

        await _service.Approve(claim.GamertagClaimID, CancellationToken.None);

        var reread = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reread.Status, Is.EqualTo(GamertagClaimStatus.Approved));
            Assert.That(reread.DecidedByUserID, Is.EqualTo(9));
        }
        await _mergeService.Received(1).Merge(claimant.Id, placeholder.Id,
            Arg.Is<MergeOptions>(o => o.Trigger == MergeTrigger.DiscordMatch && o.Reason == $"claim {claim.GamertagClaimID}"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Approve_APlaceholderThatIsNoLongerAnUnlinkedOne_ThrowsAConflict_AndChangesNothing()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "Current"));
        // A real account, not an import placeholder: a claim must never merge or tie it
        var notAPlaceholder = User.Seed(userName: "realperson", displayName: "Real");
        Context.Users.Add(notAPlaceholder);
        await Context.SaveChangesAsync();
        var claim = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 1, "OldName", "OLDNAME", notAPlaceholder.Id);
        Context.GamertagClaims.Add(claim);
        await Context.SaveChangesAsync();
        var claimId = claim.GamertagClaimID;

        var ex = Assert.ThrowsAsync<GamertagClaimConflictException>(() => _service.Approve(claimId, CancellationToken.None));

        var reread = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        var pastGamertags = await Context.XboxUsers.SelectMany(x => x.PastGamertags).CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain($"User {notAPlaceholder.Id} is no longer an unlinked placeholder"));
            Assert.That(reread.Status, Is.EqualTo(GamertagClaimStatus.Pending), "Rolled back");
            Assert.That(pastGamertags, Is.Zero, "The gamertag recorded in the same transaction was rolled back too");
            Assert.That((await Context.Users.AsNoTracking().SingleAsync(u => u.Id == notAPlaceholder.Id)).XboxUserID, Is.Null);
        }
        await _mergeService.DidNotReceiveWithAnyArgs().Merge(default, default, default, default);
    }

    [Test]
    public async Task AddHistory_ABlankGamertag_Throws()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(123, "CurrentName"));
        await Context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<GamertagClaimException>(() => _service.AddHistory(new AddGamertagHistoryRequest { XboxUserID = 123, Gamertag = "  " }, CancellationToken.None));

        Assert.That(ex!.Message, Is.EqualTo("Give the gamertag"));
    }

    [Test]
    public async Task AddHistory_DatesOutOfOrder_Throws()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(123, "CurrentName"));
        await Context.SaveChangesAsync();
        var request = new AddGamertagHistoryRequest
        {
            XboxUserID = 123,
            Gamertag = "OldName",
            FirstSeen = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSeen = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var ex = Assert.ThrowsAsync<GamertagClaimException>(() => _service.AddHistory(request, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("FirstSeen is after LastSeen"));
            Assert.That(await Context.XboxUsers.SelectMany(x => x.PastGamertags).CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task RemoveHistory_OneTheAppSaw_Throws_AndKeepsIt()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(123, "CurrentName"));
        var seen = new XboxGamertagHistory(123, "OldName", source: GamertagHistorySource.RenameDetected);
        Context.Set<XboxGamertagHistory>().Add(seen);
        await Context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<GamertagClaimException>(() => _service.RemoveHistory(seen.XboxGamertagHistoryID, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("Only a past gamertag added by hand can be removed"));
            Assert.That(await Context.XboxUsers.SelectMany(x => x.PastGamertags).CountAsync(), Is.EqualTo(1));
        }
    }
}
