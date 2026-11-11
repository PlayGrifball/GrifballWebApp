using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using static GrifballWebApp.Domain.Test.GamertagClaimAggregateUnitTests;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A gamertag claim's life (#300): it begins pending, through its factories, and is decided once, through its own
/// methods, which say when and by whom; the database refuses the combinations that can't be.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimAggregateTests
{
    [Test]
    public async Task TheDatabase_RefusesADecisionThatDoesntFitTheStatus()
    {
        var context = await TestDatabase.NewGrifballContext();
        var placeholder = User.Seed(userName: "historical:Old Tag", isDummyUser: true);
        context.Users.Add(placeholder);
        context.XboxUsers.Add(XboxUser.FromXbox(10, "New Tag"));
        await context.SaveChangesAsync();
        var gamertags = 0;
        GamertagClaim New() => GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, $"Tag {++gamertags}", $"TAG {gamertags}", placeholder.Id);
        GamertagClaim Approved() { var c = New(); c.Approve(1, At); return c; }
        GamertagClaim Rejected() { var c = New(); c.Reject("why", null, At); return c; }

        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(GamertagClaim claim, Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GamertagClaim>> corrupt)
        {
            await using var ctx = context.NewContextLike();
            corrupt(ctx.GamertagClaims.Add(claim));
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        try
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Refused(New(), e => e.Property(x => x.DecidedAt).CurrentValue = At), Is.True, "A pending claim isn't decided");
                Assert.That(await Refused(New(), e => e.Property(x => x.DecidedByUserID).CurrentValue = 1), Is.True, "Nor by anyone");
                Assert.That(await Refused(Approved(), e => e.Property(x => x.DecidedAt).CurrentValue = null), Is.True, "A decided claim says when");
                Assert.That(await Refused(New(), e => e.Property(x => x.Status).CurrentValue = GamertagClaimStatus.Rejected), Is.True);
                Assert.That(await Refused(Approved(), e => e.Property(x => x.RejectionReason).CurrentValue = "why"), Is.True, "Only a rejected claim has a reason");
                Assert.That(await Refused(Rejected(), _ => { }), Is.False, "A rejection by nobody signed in, with its reason");
                Assert.That(await Refused(Approved(), _ => { }), Is.False);
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task TheService_RecordsWhoDecided_AndWhen()
    {
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            var sysadmin = User.Seed(userName: "sysadmin");
            var placeholder = User.Seed(userName: "historical:Old Tag", isDummyUser: true);
            var otherPlaceholder = User.Seed(userName: "historical:Wrong Tag", isDummyUser: true);
            context.AddRange(sysadmin, placeholder, otherPlaceholder);
            context.XboxUsers.Add(XboxUser.FromXbox(10, "New Tag"));
            await context.SaveChangesAsync();
            var approved = GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 10, "Old Tag", "OLD TAG", placeholder.Id);
            var other = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", placeholder.Id);
            var rejected = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Wrong Tag", "WRONG TAG", otherPlaceholder.Id);
            context.GamertagClaims.AddRange(approved, other, rejected);
            await context.SaveChangesAsync();
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.GetCurrentUserId().Returns(sysadmin.Id);
            var time = new FakeTimeProvider(At);
            var service = new GamertagClaimService(context, new UserMergeService(context), currentUser, time);

            await service.Approve(approved.GamertagClaimID);
            time.Advance(TimeSpan.FromHours(1));
            await service.Reject(rejected.GamertagClaimID, "Not them");

            context.ChangeTracker.Clear();
            var claims = await context.GamertagClaims.OrderBy(c => c.GamertagClaimID)
                .Select(c => new { c.Status, c.DecidedAt, c.DecidedByUserID, c.RejectionReason }).ToListAsync();
            Assert.That(claims.Select(c => (c.Status, c.DecidedAt, c.DecidedByUserID, c.RejectionReason)), Is.EqualTo(new[]
            {
                (GamertagClaimStatus.Approved, (DateTime?)At, (int?)sysadmin.Id, (string?)null),
                (GamertagClaimStatus.Rejected, (DateTime?)At, (int?)sysadmin.Id, (string?)null),
                (GamertagClaimStatus.Rejected, (DateTime?)At.AddHours(1), (int?)sysadmin.Id, (string?)"Not them"),
            }), "The other claim on the placeholder is decided with the approval; a rejection by who made it, when");
            var again = await Assert.ThrowsAsync<GamertagClaimException>(() => service.Approve(approved.GamertagClaimID));
            Assert.That(again!.Message, Is.EqualTo($"Claim {approved.GamertagClaimID} was already approved"));
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task AMerge_SettlesThePlaceholdersClaims_AsTheSignedInUser_WhenItWasMade()
    {
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            var sysadmin = User.Seed(userName: "sysadmin");
            var real = User.Seed(userName: "real");
            var placeholder = User.Seed(userName: "historical:Old Tag", isDummyUser: true);
            context.AddRange(sysadmin, real, placeholder);
            context.XboxUsers.Add(XboxUser.FromXbox(10, "New Tag"));
            await context.SaveChangesAsync();
            context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", placeholder.Id));
            await context.SaveChangesAsync();
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.GetCurrentUserId().Returns(sysadmin.Id);

            await new UserMergeService(context, new FakeTimeProvider(At), currentUser).Merge(real.Id, placeholder.Id);

            context.ChangeTracker.Clear();
            var claim = await context.GamertagClaims.SingleAsync();
            Assert.That((claim.Status, claim.DecidedAt, claim.DecidedByUserID), Is.EqualTo((GamertagClaimStatus.Rejected, (DateTime?)At, (int?)sysadmin.Id)),
                "Another account's claim, settled by the merge");
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task AMerge_SettlesClaims_AsWhoeverItSaysTriggeredIt_BeforeTheSignedInUser()
    {
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            var signedIn = User.Seed(userName: "signed-in");
            var triggeredBy = User.Seed(userName: "trigger");
            var real = User.Seed(userName: "real");
            var placeholder = User.Seed(userName: "historical:Old Tag", isDummyUser: true);
            context.AddRange(signedIn, triggeredBy, real, placeholder);
            context.XboxUsers.Add(XboxUser.FromXbox(10, "New Tag"));
            await context.SaveChangesAsync();
            // One claim about the source (its placeholder), and one the source made that the target has made too
            var about = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", placeholder.Id);
            var duplicate = GamertagClaim.ClaimedByUser(placeholder.Id, "Shared Tag", "SHARED TAG");
            var theirs = GamertagClaim.ClaimedByUser(real.Id, "Shared Tag", "SHARED TAG");
            context.GamertagClaims.AddRange(about, duplicate, theirs);
            await context.SaveChangesAsync();
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.GetCurrentUserId().Returns(signedIn.Id);

            await new UserMergeService(context, new FakeTimeProvider(At), currentUser)
                .Merge(real.Id, placeholder.Id, MergeOptions.For(MergeTrigger.Manual, triggeredByUserId: triggeredBy.Id));

            context.ChangeTracker.Clear();
            var claims = await context.GamertagClaims.ToDictionaryAsync(c => c.GamertagClaimID);
            using (Assert.EnterMultipleScope())
            {
                Assert.That((claims[about.GamertagClaimID].Status, claims[about.GamertagClaimID].DecidedAt, claims[about.GamertagClaimID].DecidedByUserID),
                    Is.EqualTo((GamertagClaimStatus.Rejected, (DateTime?)At, (int?)triggeredBy.Id)), "Settled about the placeholder");
                var settled = claims[duplicate.GamertagClaimID];
                Assert.That((settled.Status, settled.RejectionReason, settled.DecidedAt, settled.DecidedByUserID, settled.ClaimantUserID),
                    Is.EqualTo((GamertagClaimStatus.Rejected, GamertagClaim.SameClaimAsMergeTarget, (DateTime?)At, (int?)triggeredBy.Id, (int?)real.Id)),
                    "The claimant's duplicate, rejected by whoever the merge says triggered it");
                Assert.That(claims[theirs.GamertagClaimID].Status, Is.EqualTo(GamertagClaimStatus.Pending), "The target's own claim stays the open one");
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task AMerge_MovesTheDecisionsTheSourceMade_ToTheTarget()
    {
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            var oldAdmin = User.Seed(userName: "old-admin");
            var admin = User.Seed(userName: "admin");
            var someone = User.Seed(userName: "someone");
            var claimant = User.Seed(userName: "claimant");
            context.AddRange(oldAdmin, admin, someone, claimant);
            await context.SaveChangesAsync();
            var rejectedByOld = GamertagClaim.ClaimedByUser(claimant.Id, "Tag A", "TAG A");
            rejectedByOld.Reject("Not them", oldAdmin.Id, At);
            var approvedByOld = GamertagClaim.ClaimedByUser(claimant.Id, "Tag B", "TAG B");
            approvedByOld.Approve(oldAdmin.Id, At);
            var bySomeone = GamertagClaim.ClaimedByUser(claimant.Id, "Tag C", "TAG C");
            bySomeone.Reject(null, someone.Id, At);
            var pending = GamertagClaim.ClaimedByUser(claimant.Id, "Tag D", "TAG D");
            context.GamertagClaims.AddRange(rejectedByOld, approvedByOld, bySomeone, pending);
            await context.SaveChangesAsync();

            await new UserMergeService(context, new FakeTimeProvider(At.AddDays(1))).Merge(admin.Id, oldAdmin.Id);

            context.ChangeTracker.Clear();
            var claims = await context.GamertagClaims.ToDictionaryAsync(c => c.GamertagClaimID, c => (c.Status, c.DecidedAt, c.DecidedByUserID));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(claims[rejectedByOld.GamertagClaimID], Is.EqualTo((GamertagClaimStatus.Rejected, (DateTime?)At, (int?)admin.Id)),
                    "The merged user's decision is the target's, still made when it was");
                Assert.That(claims[approvedByOld.GamertagClaimID], Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)At, (int?)admin.Id)));
                Assert.That(claims[bySomeone.GamertagClaimID], Is.EqualTo((GamertagClaimStatus.Rejected, (DateTime?)At, (int?)someone.Id)), "Someone else's decision stays theirs");
                Assert.That(claims[pending.GamertagClaimID], Is.EqualTo((GamertagClaimStatus.Pending, (DateTime?)null, (int?)null)));
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task Matching_RefusesAClaimItCantMake()
    {
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            var matching = new GamertagMatching(context);

            // A user's own claim is made from their profile, not as a suggestion about them
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => matching.ClaimForUserAsync(GamertagClaimKind.UserClaim, 1, null, "Old Tag", 5, false, CancellationToken.None));
            // An XUID lookup's claim says what Xbox found
            await Assert.ThrowsAsync<ArgumentNullException>(() => matching.ClaimAsync(GamertagClaimKind.XuidLookup, 10, "Old Tag", 5, null, null, false, CancellationToken.None));
            Assert.That(context.GamertagClaims.Local, Is.Empty);

            // A rejected claim not saved yet stops the import's suggestion, not a user setting their gamertag again
            context.XboxUsers.Add(XboxUser.FromXbox(10, "New Tag"));
            var placeholder = User.Seed(userName: "historical:Old Tag", isDummyUser: true);
            context.Users.Add(placeholder);
            await context.SaveChangesAsync();
            var rejected = GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 10, "Old Tag", HistoricalKeys.Normalize("Old Tag"), placeholder.Id);
            rejected.Reject(null, null, At);
            context.GamertagClaims.Add(rejected);
            var blocked = await matching.ClaimAsync(GamertagClaimKind.SetGamertag, 10, "Old Tag", placeholder.Id, null, null, evenIfRejected: false, CancellationToken.None);
            var again = await matching.ClaimAsync(GamertagClaimKind.SetGamertag, 10, "Old Tag", placeholder.Id, null, null, evenIfRejected: true, CancellationToken.None);
            Assert.That((blocked, again?.Status), Is.EqualTo(((GamertagClaim?)null, (GamertagClaimStatus?)GamertagClaimStatus.Pending)));
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
