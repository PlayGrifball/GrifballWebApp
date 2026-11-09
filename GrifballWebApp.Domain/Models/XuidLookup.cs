#nullable disable

namespace GrifballWebApp.Domain.Models;

/// <summary>
/// The last XUID lookup of an import placeholder's gamertag (gamertag to XUID, through the Halo API): one row per
/// placeholder, so a lookup isn't repeated too soon and its outcome can be seen.
/// </summary>
public class XuidLookup
{
    public int UserID { get; set; }
    public DateTime LookedUpAt { get; set; }
    public XuidLookupStatus Status { get; set; }
    /// <summary>The XUID found (Status Found).</summary>
    public long? XboxUserID { get; set; }
    /// <summary>Why it failed (Status Failed), to try again later.</summary>
    public string Error { get; set; }

    public virtual User User { get; set; }
}

public enum XuidLookupStatus
{
    Found = 1,
    NotFound = 2,
    Failed = 3,
}
