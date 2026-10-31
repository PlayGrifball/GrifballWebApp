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
    public long XboxUserID { get; set; }
    /// <summary>The placeholder's gamertag, as the archive writes it.</summary>
    public string Gamertag { get; set; }
    public string NormalizedGamertag { get; set; }
    /// <summary>The placeholder to merge on approval; null once it is gone (merged or deleted).</summary>
    public int? PlaceholderUserID { get; set; }
    /// <summary>The season the suggestion comes from (roster matches); the history row's dates on approval.</summary>
    public int? SeasonID { get; set; }
    /// <summary>The team of the season the roster match comes from.</summary>
    public int? TeamID { get; set; }

    public virtual XboxUser XboxUser { get; set; }
    public virtual Team Team { get; set; }
    public virtual User PlaceholderUser { get; set; }
    public virtual Season Season { get; set; }
}

public enum GamertagClaimKind
{
    /// <summary>The import paired a spreadsheet gamertag nobody matched with an unmatched XUID of the same season and team.</summary>
    RosterMatch = 1,
    /// <summary>A user set a gamertag whose placeholder is not tied to their XUID.</summary>
    SetGamertag = 2,
}

public enum GamertagClaimStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}
