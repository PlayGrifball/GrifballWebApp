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

[TestFixture]
internal sealed class GamertagClaimServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private IUserMergeService _mergeService = null!;
    private ICurrentUserService _currentUser = null!;
    private FakeTimeProvider _time = null!;
    private GamertagClaimService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mergeService = Substitute.For<IUserMergeService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.GetCurrentUserId().Returns(9);
        _time = new FakeTimeProvider(Now);
        _service = new GamertagClaimService(Context, _mergeService, _currentUser, _time);
    }

    private async Task<User> SeedPlaceholder(string gamertag = "OldName", int id = 100)
    {
        var placeholder = User.Seed(userName: $"historical:{gamertag}", displayName: gamertag, isDummyUser: true, id: id);
        Context.Users.Add(placeholder);
        await Context.SaveChangesAsync();
        return placeholder;
    }

    private async Task SeedXboxUser(long xuid, string gamertag)
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(xuid, gamertag));
        await Context.SaveChangesAsync();
    }

    /// <summary>A roster-match suggestion, with the Xbox account and the placeholder it points at (both foreign keys
    /// SQLite enforces) seeded alongside it.</summary>
    private async Task<GamertagClaim> SeedRosterMatchClaim(long xboxUserID, string gamertag, int placeholderUserID)
    {
        await SeedXboxUser(xboxUserID, gamertag);
        await SeedPlaceholder(gamertag, placeholderUserID);
        var claim = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, xboxUserID, gamertag, gamertag.ToUpperInvariant(), placeholderUserID);
        Context.GamertagClaims.Add(claim);
        await Context.SaveChangesAsync();
        return claim;
    }

    [Test]
    public async Task List_FiltersByStatus()
    {
        var pending = await SeedRosterMatchClaim(1, "Alpha", 100);
        var approved = await SeedRosterMatchClaim(2, "Bravo", 101);
        approved.Approve(9, Now);
        await Context.SaveChangesAsync();

        var result = await _service.List(GamertagClaimStatus.Pending, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Gamertag, Is.EqualTo(pending.Gamertag));
    }

    [Test]
    public async Task List_NoFilter_ReturnsAll()
    {
        await SeedRosterMatchClaim(1, "Alpha", 100);

        var result = await _service.List(null, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Reject_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Reject(999, "nope", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public async Task Reject_Pending_Rejects()
    {
        var claim = await SeedRosterMatchClaim(1, "Alpha", 100);

        await _service.Reject(claim.GamertagClaimID, "not a match", CancellationToken.None);

        var reread = await Context.GamertagClaims.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reread.Status, Is.EqualTo(GamertagClaimStatus.Rejected));
            Assert.That(reread.RejectionReason, Is.EqualTo("not a match"));
            Assert.That(reread.DecidedByUserID, Is.EqualTo(9));
        }
    }

    [Test]
    public async Task Reject_AlreadyRejected_Throws()
    {
        var claim = await SeedRosterMatchClaim(1, "Alpha", 100);
        await _service.Reject(claim.GamertagClaimID, null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Reject(claim.GamertagClaimID, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("already rejected"));
    }

    [Test]
    public async Task Approve_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Approve(999, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public async Task Approve_AlreadyApproved_Throws()
    {
        var claim = await SeedRosterMatchClaim(1, "Alpha", 100);
        claim.Approve(9, Now);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Approve(claim.GamertagClaimID, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("already approved"));
    }

    [Test]
    public async Task Approve_UserClaim_MergesPlaceholderIntoClaimant()
    {
        var placeholder = await SeedPlaceholder("OldName", 100);
        var claimant = User.Seed(userName: "claimant", displayName: "Claimant", id: 5);
        Context.Users.Add(claimant);
        var claim = GamertagClaim.ClaimedByUser(claimantUserID: 5, xboxUserID: null, gamertag: "OldName", normalizedGamertag: "OLDNAME",
            placeholderUserID: 100, firstSeen: null, lastSeen: null);
        Context.GamertagClaims.Add(claim);
        await Context.SaveChangesAsync();

        await _service.Approve(claim.GamertagClaimID, CancellationToken.None);

        var reread = await Context.GamertagClaims.SingleAsync();
        Assert.That(reread.Status, Is.EqualTo(GamertagClaimStatus.Approved));
        await _mergeService.Received().Merge(5, 100, Arg.Is<MergeOptions>(o => o.Trigger == MergeTrigger.UserClaim), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Approve_XuidLookup_NoOwner_TiesPlaceholderToAccount()
    {
        var xbox = XboxUser.FromXbox(777, "NewHolder");
        Context.XboxUsers.Add(xbox);
        var placeholder = await SeedPlaceholder("OldName", 100);
        var claim = GamertagClaim.FromXuidLookup(777, "OldName", "OLDNAME", 100, "NewHolder", Now);
        Context.GamertagClaims.Add(claim);
        await Context.SaveChangesAsync();

        await _service.Approve(claim.GamertagClaimID, CancellationToken.None);

        var reread = await Context.GamertagClaims.SingleAsync();
        Assert.That(reread.Status, Is.EqualTo(GamertagClaimStatus.Approved));
        var reloadedPlaceholder = await Context.Users.FindAsync(100);
        Assert.That(reloadedPlaceholder!.XboxUserID, Is.EqualTo(777L));
        await _mergeService.DidNotReceiveWithAnyArgs().Merge(default, default, default, default);
    }

    [Test]
    public async Task AddHistory_NoSuchAccount_Throws()
    {
        var request = new AddGamertagHistoryRequest { XboxUserID = 123, Gamertag = "Gamertag" };

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.AddHistory(request, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("123"));
    }

    [Test]
    public async Task AddHistory_Success_RecordsPastGamertag()
    {
        var xbox = XboxUser.FromXbox(123, "CurrentName");
        Context.XboxUsers.Add(xbox);
        await Context.SaveChangesAsync();
        var request = new AddGamertagHistoryRequest { XboxUserID = 123, Gamertag = "OldName", Note = "known from season 1" };

        await _service.AddHistory(request, CancellationToken.None);

        var past = await _service.PastGamertags(123, null, CancellationToken.None);
        Assert.That(past!.PastGamertags.Single().Gamertag, Is.EqualTo("OldName"));
    }

    [Test]
    public async Task PastGamertags_NoSuchAccount_ReturnsNull()
    {
        var result = await _service.PastGamertags(999, null, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RemoveHistory_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.RemoveHistory(999, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public async Task RemoveHistory_Manual_Removes()
    {
        var xbox = XboxUser.FromXbox(123, "CurrentName");
        Context.XboxUsers.Add(xbox);
        await Context.SaveChangesAsync();
        await _service.AddHistory(new AddGamertagHistoryRequest { XboxUserID = 123, Gamertag = "OldName" }, CancellationToken.None);
        var historyId = (await _service.PastGamertags(123, null, CancellationToken.None))!.PastGamertags.Single().XboxGamertagHistoryID;

        await _service.RemoveHistory(historyId, CancellationToken.None);

        var past = await _service.PastGamertags(123, null, CancellationToken.None);
        Assert.That(past!.PastGamertags, Is.Empty);
    }
}
