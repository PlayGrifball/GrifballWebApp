using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A suggestion that a placeholder (historical) user's gamertag was an Xbox account's, waiting for a sysadmin (approving merges users, which only sysadmins do):
/// approving it records the gamertag in the account's <see cref="XboxGamertagHistory"/> and merges the placeholder
/// into the account's user; rejecting it changes nothing. One pending or approved claim per kind, account and gamertag: a
/// rejected one doesn't stop the same claim being made again.
/// <para>
/// It begins pending, through one of its factories, and is decided once: <see cref="Approve"/> and <see cref="Reject"/>,
/// or a merge settling it (<see cref="PlaceholderMergedInto"/>, <see cref="ClaimantMergedInto"/>). Nothing else changes
/// its state, with one exception: an approved claim whose claimant is merged into a user with the same claim open is
/// superseded by theirs (<see cref="ClaimantMergedInto"/>). It becomes rejected, as <see cref="SameClaimAsMergeTarget"/>,
/// but keeps when and by whom it was approved: the approval happened, and only one open claim may stand.
/// </para>
/// <para>
/// A merge also moves the decision's user, when it is the merged one (<see cref="ReassignDecider"/>).
/// </para>
/// </summary>
public class GamertagClaim : AuditableEntity
{
    /// <summary>The longest rejection reason kept; a longer one is cut.</summary>
    public const int MaxRejectionReasonLength = 200;

    /// <summary>Why a merge rejected a claim its source made: the account it was merged into has the same one.</summary>
    public const string SameClaimAsMergeTarget = "The same claim was made by the account it was merged into";

    /// <summary>For EF. A claim is made by <see cref="Suggest"/>, <see cref="FromXuidLookup"/>, <see cref="FromDiscordMatch"/>
    /// or <see cref="ClaimedByUser"/>, the ways one begins.</summary>
    private GamertagClaim() { }

    /// <summary>
    /// A suggestion about an Xbox account the import or a user's new gamertag made: a roster match
    /// (<see cref="GamertagClaimKind.RosterMatch"/>, from a season and maybe a team) or a set gamertag
    /// (<see cref="GamertagClaimKind.SetGamertag"/>). Pending.
    /// </summary>
    public static GamertagClaim Suggest(GamertagClaimKind kind, long xboxUserID, string gamertag, string normalizedGamertag, int placeholderUserID, int? seasonID = null, int? teamID = null)
    {
        if (kind is not (GamertagClaimKind.RosterMatch or GamertagClaimKind.SetGamertag))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a roster match or a set gamertag is suggested about an Xbox account alone");
        return new()
        {
            Kind = kind,
            XboxUserID = xboxUserID,
            Gamertag = gamertag,
            NormalizedGamertag = normalizedGamertag,
            PlaceholderUserID = placeholderUserID,
            SeasonID = seasonID,
            TeamID = teamID,
        };
    }

    /// <summary>Xbox said the account holds the placeholder's gamertag now, as <paramref name="currentGamertag"/>, when it was
    /// looked up. Pending.</summary>
    public static GamertagClaim FromXuidLookup(long xboxUserID, string gamertag, string normalizedGamertag, int placeholderUserID, string currentGamertag, DateTime lookedUpAt) => new()
    {
        Kind = GamertagClaimKind.XuidLookup,
        XboxUserID = xboxUserID,
        Gamertag = gamertag,
        NormalizedGamertag = normalizedGamertag,
        PlaceholderUserID = placeholderUserID,
        CurrentGamertag = currentGamertag.Length > 50 ? currentGamertag[..50] : currentGamertag,
        LookedUpAt = lookedUpAt,
    };

    /// <summary>The archive gave a Discord ID the claimant has, with a gamertag that doesn't fit them. Pending.</summary>
    public static GamertagClaim FromDiscordMatch(int claimantUserID, long? xboxUserID, string gamertag, string normalizedGamertag, int placeholderUserID) => new()
    {
        Kind = GamertagClaimKind.DiscordMatch,
        ClaimantUserID = claimantUserID,
        XboxUserID = xboxUserID,
        Gamertag = gamertag,
        NormalizedGamertag = normalizedGamertag,
        PlaceholderUserID = placeholderUserID,
    };

    /// <summary>A user claimed a past gamertag's placeholder from their profile, having had it between
    /// <paramref name="firstSeen"/> and <paramref name="lastSeen"/> (either may be unknown). Pending.</summary>
    public static GamertagClaim ClaimedByUser(int claimantUserID, long? xboxUserID, string gamertag, string normalizedGamertag, int placeholderUserID, DateTime? firstSeen, DateTime? lastSeen) => new()
    {
        Kind = GamertagClaimKind.UserClaim,
        ClaimantUserID = claimantUserID,
        XboxUserID = xboxUserID,
        Gamertag = gamertag,
        NormalizedGamertag = normalizedGamertag,
        PlaceholderUserID = placeholderUserID,
        FirstSeen = firstSeen,
        LastSeen = lastSeen,
    };

    /// <summary>A user's claim whose placeholder is gone already (merged or deleted), no dates given: for tests.</summary>
    internal static GamertagClaim ClaimedByUser(int claimantUserID, string gamertag, string normalizedGamertag) => new()
    {
        Kind = GamertagClaimKind.UserClaim,
        ClaimantUserID = claimantUserID,
        Gamertag = gamertag,
        NormalizedGamertag = normalizedGamertag,
    };

    // Nothing outside the claim sets its state, its foreign keys or its navigations: only its own methods, and EF (which
    // uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int GamertagClaimID { get; internal set; }
    public GamertagClaimKind Kind { get; private set; }
    /// <summary>Pending until decided, once: <see cref="Approve"/>, <see cref="Reject"/>, or a merge settling it.</summary>
    public GamertagClaimStatus Status { get; private set; } = GamertagClaimStatus.Pending;
    /// <summary>When it was approved or rejected; null while pending (CK_Event_GamertagClaims_DecidedOnceDecided). An
    /// approval a merge supersedes keeps its time.</summary>
    public DateTime? DecidedAt { get; private set; }
    /// <summary>The user who approved or rejected it, or whose merge settled it; null while pending, and when nobody signed
    /// in decided it. An approval a merge supersedes keeps its user; a merge of this user moves it
    /// (<see cref="ReassignDecider"/>).</summary>
    public int? DecidedByUserID { get; private set; }
    /// <summary>The Xbox account the gamertag would be recorded on. Null for a user's or Discord claim whose user has no
    /// Xbox account linked (nothing is recorded in a gamertag history then).</summary>
    public long? XboxUserID { get; private set; }
    /// <summary>The user the placeholder is merged into on approval, for the kinds made about a user rather than an Xbox
    /// account (<see cref="GamertagClaimKind.UserClaim"/>, <see cref="GamertagClaimKind.DiscordMatch"/>).</summary>
    public int? ClaimantUserID { get; private set; }
    /// <summary>A user's claim: when they say they had the gamertag (either may be unknown).</summary>
    public DateTime? FirstSeen { get; private set; }
    public DateTime? LastSeen { get; private set; }
    /// <summary>Why it was rejected, shown to the claimant; only a rejected claim has one
    /// (CK_Event_GamertagClaims_RejectionReasonOnlyWhenRejected).</summary>
    public string? RejectionReason { get; private set; }
    /// <summary>The placeholder's gamertag, as the archive writes it.</summary>
    public string Gamertag { get; private set; } = null!;
    public string NormalizedGamertag { get; private set; } = null!;
    /// <summary>The placeholder to merge on approval; null once it is gone (merged or deleted).</summary>
    public int? PlaceholderUserID { get; private set; }
    /// <summary>The season the suggestion comes from (roster matches); the history row's dates on approval.</summary>
    public int? SeasonID { get; private set; }
    /// <summary>The team of the season the roster match comes from.</summary>
    public int? TeamID { get; private set; }
    /// <summary>An XUID lookup: the gamertag the Xbox account has now, as Xbox reported it, and when.</summary>
    public string? CurrentGamertag { get; private set; }
    public DateTime? LookedUpAt { get; private set; }

    public virtual XboxUser? XboxUser { get; private set; }
    public virtual Team? Team { get; private set; }
    public virtual User? PlaceholderUser { get; private set; }
    public virtual User? ClaimantUser { get; private set; }
    public virtual Season? Season { get; private set; }

    /// <summary>
    /// A claim still standing: pending or approved, not rejected. One open claim per kind and account (or user) and
    /// gamertag; a rejected one doesn't stop the same claim being made again. For queries.
    /// </summary>
    public static readonly Expression<Func<GamertagClaim, bool>> Open = c => c.Status != GamertagClaimStatus.Rejected;

    private static readonly Func<GamertagClaim, bool> _isOpen = Open.Compile();

    /// <summary><see cref="Open"/>, for a loaded claim.</summary>
    public bool IsOpen => _isOpen(this);

    /// <summary>Approves a pending claim, decided by <paramref name="decidedByUserID"/> at <paramref name="decidedAt"/>.
    /// A decided claim stays as it was.</summary>
    public GamertagClaimDecision Approve(int? decidedByUserID, DateTime decidedAt)
        => Decide(GamertagClaimStatus.Approved, null, decidedByUserID, decidedAt);

    /// <summary>Rejects a pending claim; the <paramref name="reason"/>, if given, is shown to the claimant (trimmed, and
    /// cut to <see cref="MaxRejectionReasonLength"/>). A decided claim stays as it was.</summary>
    public GamertagClaimDecision Reject(string? reason, int? decidedByUserID, DateTime decidedAt)
        => Decide(GamertagClaimStatus.Rejected, reason, decidedByUserID, decidedAt);

    private GamertagClaimDecision Decide(GamertagClaimStatus status, string? reason, int? decidedByUserID, DateTime decidedAt)
    {
        switch (Status)
        {
            case GamertagClaimStatus.Approved:
                return GamertagClaimDecision.AlreadyApproved;
            case GamertagClaimStatus.Rejected:
                return GamertagClaimDecision.AlreadyRejected;
        }
        Status = status;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null
            : reason.Trim() is { Length: > MaxRejectionReasonLength } r ? r[..MaxRejectionReasonLength] : reason.Trim();
        DecidedAt = decidedAt;
        DecidedByUserID = decidedByUserID;
        return GamertagClaimDecision.Decided;
    }

    /// <summary>
    /// A merge: the pending claim's placeholder was merged into <paramref name="into"/>, so it can't stay as it is. Merged
    /// into another placeholder, it is about that one; merged into a real user, it is approved if it was that user's (made by
    /// them, or about their Xbox account: the placeholder is theirs now) and rejected otherwise (it can't be anyone else's any
    /// more), so approving it later never writes history onto the wrong account. Only a pending claim: a decided one stays
    /// the record of its decision.
    /// </summary>
    public void PlaceholderMergedInto(User into, int? decidedByUserID, DateTime decidedAt)
    {
        if (Status is not GamertagClaimStatus.Pending)
            throw new InvalidOperationException($"Claim {GamertagClaimID} was already {Status.ToString().ToLowerInvariant()}");
        if (into.IsDummyUser)
        {
            PlaceholderUserID = into.Id;
            return;
        }
        if (ClaimantUserID == into.Id || (XboxUserID is not null && into.XboxUserID == XboxUserID))
            Approve(decidedByUserID, decidedAt);
        else
            Reject(null, decidedByUserID, decidedAt);
    }

    /// <summary>
    /// A merge: the claim's claimant was merged into <paramref name="into"/>, whose claim it is now. The user's own open
    /// claims, <paramref name="theirOpenClaims"/>, stay the only ones (one open per kind, user and gamertag): this one, if
    /// open and the same as one of theirs, is rejected as <see cref="SameClaimAsMergeTarget"/>. A pending one is rejected by
    /// <paramref name="decidedByUserID"/> at <paramref name="decidedAt"/>; an approved one is superseded
    /// (<see cref="SupersedeApproval"/>), the one move out of a decided state, and keeps its approval's time and user.
    /// </summary>
    public void ClaimantMergedInto(User into, IEnumerable<GamertagClaim> theirOpenClaims, int? decidedByUserID, DateTime decidedAt)
    {
        if (ClaimantUserID is null)
            throw new InvalidOperationException($"Claim {GamertagClaimID} has no claimant");
        if (IsOpen && theirOpenClaims.Any(t => t != this && t.IsOpen && t.Kind == Kind && t.NormalizedGamertag == NormalizedGamertag))
        {
            if (Status is GamertagClaimStatus.Approved)
                SupersedeApproval(SameClaimAsMergeTarget);
            else
                Reject(SameClaimAsMergeTarget, decidedByUserID, decidedAt);
        }
        ClaimantUserID = into.Id;
    }

    /// <summary>
    /// The one re-decision: an approved claim gives way to another open claim that must be the only one, and is rejected
    /// with <paramref name="reason"/>. Its <see cref="DecidedAt"/> and <see cref="DecidedByUserID"/> stay those of the
    /// approval, which did happen (its merge and gamertag history are not undone).
    /// </summary>
    private void SupersedeApproval(string reason)
    {
        System.Diagnostics.Debug.Assert(Status is GamertagClaimStatus.Approved, "Only an approval is superseded");
        Status = GamertagClaimStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>A merge: the user who decided the claim, <paramref name="fromUserID"/>, was merged into
    /// <paramref name="toUserID"/>, who is its decider now. A claim someone else decided, or nobody yet, stays as it was.</summary>
    public void ReassignDecider(int fromUserID, int toUserID)
    {
        if (DecidedByUserID == fromUserID)
            DecidedByUserID = toUserID;
    }

    /// <summary>The import deleted the claim's team (it is no longer in its season): the claim is left without one.</summary>
    public void RemoveTeam()
    {
        TeamID = null;
        Team = null;
    }
}

/// <summary>What <see cref="GamertagClaim.Approve"/> or <see cref="GamertagClaim.Reject"/> did.</summary>
public enum GamertagClaimDecision
{
    Decided = 1,
    AlreadyApproved = 2,
    AlreadyRejected = 3,
}

public enum GamertagClaimKind
{
    /// <summary>The import paired a spreadsheet gamertag nobody matched with an unmatched XUID of the same season and team.</summary>
    RosterMatch = 1,
    /// <summary>A user set a gamertag whose placeholder is not tied to their XUID.</summary>
    SetGamertag = 2,
    /// <summary>Xbox gave the XUID that holds a placeholder's gamertag now: its current holder, who may not be the one
    /// who used it in the sheet's year.</summary>
    XuidLookup = 3,
    /// <summary>A user claimed a past gamertag's placeholder from their profile.</summary>
    UserClaim = 4,
    /// <summary>The archive gave a Discord ID a site user has, with a gamertag that doesn't fit that user.</summary>
    DiscordMatch = 5,
}

public enum GamertagClaimStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}
