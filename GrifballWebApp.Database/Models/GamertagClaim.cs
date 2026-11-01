#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A suggestion that a placeholder (historical) user's gamertag was an Xbox account's, waiting for a sysadmin (approving merges users, which only sysadmins do):
/// approving it records the gamertag in the account's <see cref="XboxGamertagHistory"/> and merges the placeholder
/// into the account's user; rejecting it changes nothing. One pending or approved claim per kind, account and gamertag: a
/// rejected one doesn't stop the same claim being made again.
/// </summary>
public class GamertagClaim : AuditableEntity
{
    public int GamertagClaimID { get; set; }
    public GamertagClaimKind Kind { get; set; }
    public GamertagClaimStatus Status { get; set; } = GamertagClaimStatus.Pending;
    /// <summary>The Xbox account the gamertag would be recorded on. Null for a user's or Discord claim whose user has no
    /// Xbox account linked (nothing is recorded in a gamertag history then).</summary>
    public long? XboxUserID { get; set; }
    /// <summary>The user the placeholder is merged into on approval, for the kinds made about a user rather than an Xbox
    /// account (<see cref="GamertagClaimKind.UserClaim"/>, <see cref="GamertagClaimKind.DiscordMatch"/>).</summary>
    public int? ClaimantUserID { get; set; }
    /// <summary>A user's claim: when they say they had the gamertag (either may be unknown).</summary>
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    /// <summary>Why a sysadmin rejected it, shown to the claimant.</summary>
    public string RejectionReason { get; set; }
    /// <summary>The placeholder's gamertag, as the archive writes it.</summary>
    public string Gamertag { get; set; }
    public string NormalizedGamertag { get; set; }
    /// <summary>The placeholder to merge on approval; null once it is gone (merged or deleted).</summary>
    public int? PlaceholderUserID { get; set; }
    /// <summary>The season the suggestion comes from (roster matches); the history row's dates on approval.</summary>
    public int? SeasonID { get; set; }
    /// <summary>The team of the season the roster match comes from.</summary>
    public int? TeamID { get; set; }
    /// <summary>An XUID lookup: the gamertag the Xbox account has now, as Xbox reported it, and when.</summary>
    public string CurrentGamertag { get; set; }
    public DateTime? LookedUpAt { get; set; }

    public virtual XboxUser XboxUser { get; set; }
    public virtual Team Team { get; set; }
    public virtual User PlaceholderUser { get; set; }
    public virtual User ClaimantUser { get; set; }
    public virtual Season Season { get; set; }
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
