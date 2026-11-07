using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// A gamertag claim's life (#300): it begins pending, through its factories, and is decided once, through its own
/// methods, which say when and by whom; the database refuses the combinations that can't be.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimAggregateTests
{
    private static readonly DateTime At = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private static GamertagClaim Pending(long xuid = 10, int placeholder = 5) => GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, xuid, "Old Tag", "OLD TAG", placeholder);

    [Test]
    public void Approve_DecidesAPendingClaimOnce()
    {
        var claim = Pending();

        var first = claim.Approve(7, At);
        var again = claim.Approve(8, At.AddDays(1));
        var reject = claim.Reject("no", 8, At.AddDays(1));

        Assert.Multiple(() =>
        {
            Assert.That((first, again, reject), Is.EqualTo((GamertagClaimDecision.Decided, GamertagClaimDecision.AlreadyApproved, GamertagClaimDecision.AlreadyApproved)));
            Assert.That((claim.Status, claim.DecidedAt, claim.DecidedByUserID, claim.RejectionReason),
                Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)At, (int?)7, (string?)null)), "The first decision stands");
        });
    }

    [Test]
    public void Reject_KeepsATrimmedReason_CutToItsLength_AndDecidesOnce()
    {
        var claim = Pending();
        var blank = Pending();
        var nobody = Pending();

        var first = claim.Reject("  " + new string('x', 250) + "  ", null, At);
        blank.Reject("   ", 7, At);
        nobody.Reject(null, 7, At);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(GamertagClaimDecision.Decided));
            Assert.That((claim.Status, claim.RejectionReason, claim.DecidedAt, claim.DecidedByUserID),
                Is.EqualTo((GamertagClaimStatus.Rejected, new string('x', GamertagClaim.MaxRejectionReasonLength), (DateTime?)At, (int?)null)),
                "Nobody signed in may decide it");
            Assert.That((blank.RejectionReason, nobody.RejectionReason), Is.EqualTo(((string?)null, (string?)null)));
            Assert.That(claim.Approve(7, At), Is.EqualTo(GamertagClaimDecision.AlreadyRejected));
            Assert.That(claim.Reject("again", 7, At), Is.EqualTo(GamertagClaimDecision.AlreadyRejected));
            Assert.That(claim.Status, Is.EqualTo(GamertagClaimStatus.Rejected));
        });
    }

    [Test]
    public void Open_IsPendingOrApproved_InQueriesAndOnLoadedClaims()
    {
        var pending = Pending();
        var approved = Pending();
        approved.Approve(1, At);
        var rejected = Pending();
        rejected.Reject(null, 1, At);
        var claims = new[] { pending, approved, rejected };

        Assert.Multiple(() =>
        {
            Assert.That(claims.Select(c => c.IsOpen), Is.EqualTo(new[] { true, true, false }));
            Assert.That(claims.AsQueryable().Where(GamertagClaim.Open), Is.EqualTo(new[] { pending, approved }));
        });
    }

    [Test]
    public void Factories_BeginPending_EachKindInItsOwnWay()
    {
        var suggested = GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 10, "Old Tag", "OLD TAG", 5, 3, 4);
        var lookup = GamertagClaim.FromXuidLookup(10, "Old Tag", "OLD TAG", 5, new string('n', 60), At);
        var discord = GamertagClaim.FromDiscordMatch(6, null, "Old Tag", "OLD TAG", 5);
        var user = GamertagClaim.ClaimedByUser(6, 10, "Old Tag", "OLD TAG", 5, At.AddYears(-8), At.AddYears(-7));

        Assert.Multiple(() =>
        {
            Assert.That(new[] { suggested, lookup, discord, user }.Select(c => (c.Status, c.DecidedAt)), Is.All.EqualTo((GamertagClaimStatus.Pending, (DateTime?)null)));
            Assert.That((suggested.Kind, suggested.XboxUserID, suggested.PlaceholderUserID, suggested.SeasonID, suggested.TeamID),
                Is.EqualTo((GamertagClaimKind.SetGamertag, (long?)10, (int?)5, (int?)3, (int?)4)));
            Assert.That((lookup.Kind, lookup.CurrentGamertag, lookup.LookedUpAt), Is.EqualTo((GamertagClaimKind.XuidLookup, new string('n', 50), (DateTime?)At)),
                "Xbox's gamertag cut to the column");
            Assert.That((discord.Kind, discord.ClaimantUserID, discord.XboxUserID), Is.EqualTo((GamertagClaimKind.DiscordMatch, (int?)6, (long?)null)));
            Assert.That((user.Kind, user.ClaimantUserID, user.FirstSeen, user.LastSeen), Is.EqualTo((GamertagClaimKind.UserClaim, (int?)6, (DateTime?)At.AddYears(-8), (DateTime?)At.AddYears(-7))));
            foreach (var kind in new[] { GamertagClaimKind.XuidLookup, GamertagClaimKind.UserClaim, GamertagClaimKind.DiscordMatch })
                Assert.That(() => GamertagClaim.Suggest(kind, 10, "Old Tag", "OLD TAG", 5), Throws.InstanceOf<ArgumentOutOfRangeException>(), $"{kind} has its own factory");
        });
    }

    [Test]
    public void PlaceholderMergedInto_MovesToAnotherPlaceholder_OrIsSettledForARealUser()
    {
        var intoPlaceholder = Pending();
        var theirs = Pending(xuid: 10);
        var claimedByThem = GamertagClaim.FromDiscordMatch(9, null, "Old Tag", "OLD TAG", 5);
        var someoneElses = Pending(xuid: 20);
        var decided = Pending();
        decided.Reject(null, 1, At);

        intoPlaceholder.PlaceholderMergedInto(new User { Id = 6, IsDummyUser = true }, 7, At);
        var real = new User { Id = 9, XboxUserID = 10 };
        theirs.PlaceholderMergedInto(real, 7, At);
        claimedByThem.PlaceholderMergedInto(real, 7, At);
        someoneElses.PlaceholderMergedInto(real, 7, At);

        Assert.Multiple(() =>
        {
            Assert.That((intoPlaceholder.Status, intoPlaceholder.PlaceholderUserID, intoPlaceholder.DecidedAt), Is.EqualTo((GamertagClaimStatus.Pending, (int?)6, (DateTime?)null)));
            Assert.That((theirs.Status, theirs.DecidedAt, theirs.DecidedByUserID), Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)At, (int?)7)), "About their Xbox account");
            Assert.That(claimedByThem.Status, Is.EqualTo(GamertagClaimStatus.Approved), "Made by them");
            Assert.That((someoneElses.Status, someoneElses.RejectionReason, someoneElses.DecidedByUserID), Is.EqualTo((GamertagClaimStatus.Rejected, (string?)null, (int?)7)));
            Assert.That(() => decided.PlaceholderMergedInto(real, 7, At), Throws.InvalidOperationException, "A decided claim stays the record of its decision");
        });
    }

    [Test]
    public void ClaimantMergedInto_MovesTheClaim_AndRejectsOneTheTargetHasOpen()
    {
        var into = new User { Id = 9 };
        var theirs = GamertagClaim.ClaimedByUser(9, "Shared Tag", "SHARED TAG");
        var theirRejected = GamertagClaim.ClaimedByUser(9, "Other Tag", "OTHER TAG");
        theirRejected.Reject(null, 1, At);
        var same = GamertagClaim.ClaimedByUser(8, "Shared Tag", "SHARED TAG");
        var approvedSame = GamertagClaim.ClaimedByUser(8, "Shared Tag", "SHARED TAG");
        approvedSame.Approve(1, At.AddDays(-1));
        var other = GamertagClaim.ClaimedByUser(8, "Other Tag", "OTHER TAG");
        var rejected = GamertagClaim.ClaimedByUser(8, "Shared Tag", "SHARED TAG");
        rejected.Reject("before", 1, At.AddDays(-1));

        foreach (var claim in new[] { same, approvedSame, other, rejected })
            claim.ClaimantMergedInto(into, [theirs, theirRejected], 7, At);

        Assert.Multiple(() =>
        {
            Assert.That(new[] { same, approvedSame, other, rejected }.Select(c => c.ClaimantUserID), Is.All.EqualTo(9), "All are the target's now");
            Assert.That((same.Status, same.RejectionReason, same.DecidedAt, same.DecidedByUserID),
                Is.EqualTo((GamertagClaimStatus.Rejected, GamertagClaim.SameClaimAsMergeTarget, (DateTime?)At, (int?)7)));
            Assert.That((approvedSame.Status, approvedSame.RejectionReason), Is.EqualTo((GamertagClaimStatus.Rejected, GamertagClaim.SameClaimAsMergeTarget)),
                "One open claim per kind, user and gamertag");
            Assert.That((other.Status, other.DecidedAt), Is.EqualTo((GamertagClaimStatus.Pending, (DateTime?)null)), "The target's rejected claim stops nothing");
            Assert.That((rejected.RejectionReason, rejected.DecidedAt), Is.EqualTo(("before", (DateTime?)At.AddDays(-1))), "A rejected claim keeps its decision");
            Assert.That(() => Pending().ClaimantMergedInto(into, [], 7, At), Throws.InvalidOperationException, "A claim about an account has no claimant");
        });
    }

    [Test]
    public void RemoveTeam_LeavesTheClaimWithoutOne()
    {
        var claim = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", 5, 3, 4);

        claim.RemoveTeam();

        Assert.That((claim.TeamID, claim.Team, claim.SeasonID), Is.EqualTo(((int?)null, (Team?)null, (int?)3)));
    }

    [Test]
    public async Task TheDatabase_RefusesADecisionThatDoesntFitTheStatus()
    {
        var context = await SetUpFixture.NewGrifballContext();
        var placeholder = new User { UserName = "historical:Old Tag", IsDummyUser = true };
        context.Users.Add(placeholder);
        context.XboxUsers.Add(new XboxUser { XboxUserID = 10, Gamertag = "New Tag" });
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
            Assert.Multiple(async () =>
            {
                Assert.That(await Refused(New(), e => e.Property(x => x.DecidedAt).CurrentValue = At), Is.True, "A pending claim isn't decided");
                Assert.That(await Refused(New(), e => e.Property(x => x.DecidedByUserID).CurrentValue = 1), Is.True, "Nor by anyone");
                Assert.That(await Refused(Approved(), e => e.Property(x => x.DecidedAt).CurrentValue = null), Is.True, "A decided claim says when");
                Assert.That(await Refused(New(), e => e.Property(x => x.Status).CurrentValue = GamertagClaimStatus.Rejected), Is.True);
                Assert.That(await Refused(Approved(), e => e.Property(x => x.RejectionReason).CurrentValue = "why"), Is.True, "Only a rejected claim has a reason");
                Assert.That(await Refused(Rejected(), _ => { }), Is.False, "A rejection by nobody signed in, with its reason");
                Assert.That(await Refused(Approved(), _ => { }), Is.False);
            });
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task TheService_RecordsWhoDecided_AndWhen()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var sysadmin = new User { UserName = "sysadmin" };
            var placeholder = new User { UserName = "historical:Old Tag", IsDummyUser = true };
            context.AddRange(sysadmin, placeholder);
            context.XboxUsers.Add(new XboxUser { XboxUserID = 10, Gamertag = "New Tag" });
            await context.SaveChangesAsync();
            var approved = GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 10, "Old Tag", "OLD TAG", placeholder.Id);
            var other = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", placeholder.Id);
            context.GamertagClaims.AddRange(approved, other);
            await context.SaveChangesAsync();
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.GetCurrentUserId().Returns(sysadmin.Id);
            var service = new GamertagClaimService(context, new UserMergeService(context), currentUser, new FakeTimeProvider(At));

            await service.Approve(approved.GamertagClaimID);

            context.ChangeTracker.Clear();
            var claims = await context.GamertagClaims.OrderBy(c => c.GamertagClaimID).Select(c => new { c.Status, c.DecidedAt, c.DecidedByUserID }).ToListAsync();
            Assert.That(claims.Select(c => (c.Status, c.DecidedAt, c.DecidedByUserID)), Is.EqualTo(new[]
            {
                (GamertagClaimStatus.Approved, (DateTime?)At, (int?)sysadmin.Id),
                (GamertagClaimStatus.Rejected, (DateTime?)At, (int?)sysadmin.Id),
            }), "The other claim on the placeholder is decided with it");
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task Matching_RefusesAClaimItCantMake()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var matching = new GamertagMatching(context);

            // A user's own claim is made from their profile, not as a suggestion about them
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => matching.ClaimForUserAsync(GamertagClaimKind.UserClaim, 1, null, "Old Tag", 5, false, CancellationToken.None));
            // An XUID lookup's claim says what Xbox found
            await Assert.ThrowsAsync<ArgumentNullException>(() => matching.ClaimAsync(GamertagClaimKind.XuidLookup, 10, "Old Tag", 5, null, null, false, CancellationToken.None));
            Assert.That(context.GamertagClaims.Local, Is.Empty);
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
