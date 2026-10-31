#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A gamertag ONE Xbox account (XUID) had before: its renames. Another Xbox account of the same person is not modelled
/// (a user has one Xbox account). <see cref="FirstSeen"/> and <see cref="LastSeen"/> bound when the account is known to
/// have used it (UTC; null when unknown), which is what lets a historical season's gamertag be matched to the account
/// that had it then rather than to whoever holds it now.
/// </summary>
public class XboxGamertagHistory : AuditableEntity
{
    public int XboxGamertagHistoryID { get; set; }
    public long XboxUserID { get; set; }
    public string Gamertag { get; set; }
    /// <summary>The gamertag trimmed, inner whitespace collapsed and upper-cased: what lookups compare.</summary>
    public string NormalizedGamertag { get; set; }
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public GamertagHistorySource Source { get; set; }

    public virtual XboxUser XboxUser { get; set; }
}

/// <summary>How a past gamertag became known.</summary>
public enum GamertagHistorySource
{
    /// <summary>The Halo API returned a new gamertag for a known XUID (or the XboxUsers row history shows one).</summary>
    RenameDetected = 1,
    /// <summary>The historical import tied a season's gamertag to the XUID that played its Waypoint matches.</summary>
    HistoricalImport = 2,
    /// <summary>A commissioner approved a roster match suggested by the historical import.</summary>
    RosterMatch = 3,
    /// <summary>Added or approved by a commissioner or sysadmin.</summary>
    Manual = 4,
}
