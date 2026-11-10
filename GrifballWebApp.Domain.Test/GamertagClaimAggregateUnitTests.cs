using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using static GrifballWebApp.Domain.Test.GamertagClaimAggregateTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>GamertagClaimAggregateTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimAggregateUnitTests
{
    private static readonly bool[] ExpectedClaims = [true, true, false];

    internal static readonly DateTime At = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Approve_DecidesAPendingClaimOnce()
    {
        var claim = Pending();

        var first = claim.Approve(7, At);
        var again = claim.Approve(8, At.AddDays(1));
        var reject = claim.Reject("no", 8, At.AddDays(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((first, again, reject), Is.EqualTo((GamertagClaimDecision.Decided, GamertagClaimDecision.AlreadyApproved, GamertagClaimDecision.AlreadyApproved)));
            Assert.That((claim.Status, claim.DecidedAt, claim.DecidedByUserID, claim.RejectionReason),
                Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)At, (int?)7, (string?)null)), "The first decision stands");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(GamertagClaimDecision.Decided));
            Assert.That((claim.Status, claim.RejectionReason, claim.DecidedAt, claim.DecidedByUserID),
                Is.EqualTo((GamertagClaimStatus.Rejected, new string('x', GamertagClaim.MaxRejectionReasonLength), (DateTime?)At, (int?)null)),
                "Nobody signed in may decide it");
            Assert.That((blank.RejectionReason, nobody.RejectionReason), Is.EqualTo(((string?)null, (string?)null)));
            Assert.That(claim.Approve(7, At), Is.EqualTo(GamertagClaimDecision.AlreadyRejected));
            Assert.That(claim.Reject("again", 7, At), Is.EqualTo(GamertagClaimDecision.AlreadyRejected));
            Assert.That(claim.Status, Is.EqualTo(GamertagClaimStatus.Rejected));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(claims.Select(c => c.IsOpen), Is.EqualTo(ExpectedClaims));
            Assert.That(claims.AsQueryable().Where(GamertagClaim.Open), Is.EqualTo(new[] { pending, approved }));
        }
    }

    [Test]
    public void Factories_BeginPending_EachKindInItsOwnWay()
    {
        var suggested = GamertagClaim.Suggest(GamertagClaimKind.SetGamertag, 10, "Old Tag", "OLD TAG", 5, 3, 4);
        var lookup = GamertagClaim.FromXuidLookup(10, "Old Tag", "OLD TAG", 5, new string('n', 60), At);
        var discord = GamertagClaim.FromDiscordMatch(6, null, "Old Tag", "OLD TAG", 5);
        var user = GamertagClaim.ClaimedByUser(6, 10, "Old Tag", "OLD TAG", 5, At.AddYears(-8), At.AddYears(-7));

        using (Assert.EnterMultipleScope())
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
        }
    }

    [Test]
    public void PlaceholderMergedInto_MovesToAnotherPlaceholder_OrIsSettledForARealUser()
    {
        var intoPlaceholder = Pending();
        var theirs = Pending(xuid: 10);
        var claimedByThem = GamertagClaim.FromDiscordMatch(9, null, "Old Tag", "OLD TAG", 5);
        var someoneElses = Pending(xuid: 20);
        var noAccount = GamertagClaim.FromDiscordMatch(8, null, "Old Tag", "OLD TAG", 5);
        var decided = Pending();
        decided.Reject(null, 1, At);

        intoPlaceholder.PlaceholderMergedInto(User.Seed(id: 6, isDummyUser: true), 7, At);
        var real = User.Seed(id: 9, xboxUserID: 10);
        theirs.PlaceholderMergedInto(real, 7, At);
        claimedByThem.PlaceholderMergedInto(real, 7, At);
        someoneElses.PlaceholderMergedInto(real, 7, At);
        noAccount.PlaceholderMergedInto(real, 7, At);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((intoPlaceholder.Status, intoPlaceholder.PlaceholderUserID, intoPlaceholder.DecidedAt), Is.EqualTo((GamertagClaimStatus.Pending, (int?)6, (DateTime?)null)));
            Assert.That((theirs.Status, theirs.DecidedAt, theirs.DecidedByUserID), Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)At, (int?)7)), "About their Xbox account");
            Assert.That(claimedByThem.Status, Is.EqualTo(GamertagClaimStatus.Approved), "Made by them");
            Assert.That((someoneElses.Status, someoneElses.RejectionReason, someoneElses.DecidedByUserID), Is.EqualTo((GamertagClaimStatus.Rejected, (string?)null, (int?)7)));
            Assert.That(noAccount.Status, Is.EqualTo(GamertagClaimStatus.Rejected), "Another user's, about no Xbox account");
            Assert.That(() => decided.PlaceholderMergedInto(real, 7, At), Throws.InvalidOperationException, "A decided claim stays the record of its decision");
        }
    }

    [TestCase(GamertagClaimStatus.Pending)]
    [TestCase(GamertagClaimStatus.Rejected)]
    public void OnlyAnApproval_IsSuperseded(GamertagClaimStatus status)
    {
        var claim = GamertagClaim.ClaimedByUser(9, "Shared Tag", "SHARED TAG");
        if (status is GamertagClaimStatus.Rejected)
            claim.Reject("no", 1, At);

        Assert.Throws<InvalidOperationException>(() => claim.SupersedeApproval(GamertagClaim.SameClaimAsMergeTarget));
        Assert.That(claim.Status, Is.EqualTo(status));
    }

    [Test]
    public void ClaimantMergedInto_MovesTheClaim_AndRejectsOneTheTargetHasOpen()
    {
        var into = User.Seed(id: 9);
        var theirs = GamertagClaim.ClaimedByUser(9, "Shared Tag", "SHARED TAG");
        var theirRejected = GamertagClaim.ClaimedByUser(9, "Other Tag", "OTHER TAG");
        theirRejected.Reject(null, 1, At);
        var same = GamertagClaim.ClaimedByUser(8, "Shared Tag", "SHARED TAG");
        var approvedSame = GamertagClaim.ClaimedByUser(8, "Shared Tag", "SHARED TAG");
        approvedSame.Approve(1, At.AddDays(-1));
        var other = GamertagClaim.ClaimedByUser(8, "Other Tag", "OTHER TAG");
        var theirDiscord = GamertagClaim.FromDiscordMatch(9, null, "Discord Tag", "DISCORD TAG", 5);
        var otherKind = GamertagClaim.ClaimedByUser(8, "Discord Tag", "DISCORD TAG");
        var rejected = GamertagClaim.ClaimedByUser(8, "Shared Tag", "SHARED TAG");
        rejected.Reject("before", 1, At.AddDays(-1));

        foreach (var claim in new[] { same, approvedSame, other, rejected, otherKind })
            claim.ClaimantMergedInto(into, [theirs, theirRejected, theirDiscord], 7, At);
        // A claim among the target's already (a retry) isn't a duplicate of itself
        var already = GamertagClaim.ClaimedByUser(9, "Own Tag", "OWN TAG");
        already.ClaimantMergedInto(into, [already], 7, At);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { same, approvedSame, other, rejected }.Select(c => c.ClaimantUserID), Is.All.EqualTo(9), "All are the target's now");
            Assert.That((same.Status, same.RejectionReason, same.DecidedAt, same.DecidedByUserID),
                Is.EqualTo((GamertagClaimStatus.Rejected, GamertagClaim.SameClaimAsMergeTarget, (DateTime?)At, (int?)7)));
            Assert.That((approvedSame.Status, approvedSame.RejectionReason), Is.EqualTo((GamertagClaimStatus.Rejected, GamertagClaim.SameClaimAsMergeTarget)),
                "One open claim per kind, user and gamertag");
            Assert.That((approvedSame.DecidedAt, approvedSame.DecidedByUserID), Is.EqualTo(((DateTime?)At.AddDays(-1), (int?)1)),
                "A superseded approval keeps when and by whom it was approved, not the merge's");
            Assert.That((other.Status, other.DecidedAt), Is.EqualTo((GamertagClaimStatus.Pending, (DateTime?)null)), "The target's rejected claim stops nothing");
            Assert.That((otherKind.Status, already.Status), Is.EqualTo((GamertagClaimStatus.Pending, GamertagClaimStatus.Pending)), "A claim of another kind, or the claim itself, is no duplicate");
            Assert.That((rejected.RejectionReason, rejected.DecidedAt), Is.EqualTo(("before", (DateTime?)At.AddDays(-1))), "A rejected claim keeps its decision");
            Assert.That(() => Pending().ClaimantMergedInto(into, [], 7, At), Throws.InvalidOperationException, "A claim about an account has no claimant");
        }
    }

    [Test]
    public void ReassignDecider_MovesOnlyTheMergedUsersDecisions()
    {
        var theirs = Pending();
        theirs.Reject(null, 3, At);
        var someoneElses = Pending();
        someoneElses.Approve(5, At);
        var nobodys = Pending();
        nobodys.Reject(null, null, At);
        var pending = Pending();

        foreach (var claim in new[] { theirs, someoneElses, nobodys, pending })
            claim.ReassignDecider(3, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((theirs.DecidedByUserID, theirs.DecidedAt), Is.EqualTo(((int?)4, (DateTime?)At)), "The merged user's decision is the target's, still made then");
            Assert.That(new[] { someoneElses.DecidedByUserID, nobodys.DecidedByUserID, pending.DecidedByUserID }, Is.EqualTo(new int?[] { 5, null, null }));
            Assert.That(pending.Status, Is.EqualTo(GamertagClaimStatus.Pending), "Moving the decider decides nothing");
        }
    }

    [Test]
    public void RemoveTeam_LeavesTheClaimWithoutOne()
    {
        var claim = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", 5, 3, 4);

        claim.RemoveTeam();

        Assert.That((claim.TeamID, claim.Team, claim.SeasonID), Is.EqualTo(((int?)null, (Team?)null, (int?)3)));
    }
}

/// <summary>GamertagClaimAggregateTests' helpers, shared with its integration tests.</summary>
internal static class GamertagClaimAggregateTestData
{
    internal static GamertagClaim Pending(long xuid = 10, int placeholder = 5) => GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, xuid, "Old Tag", "OLD TAG", placeholder);
}
