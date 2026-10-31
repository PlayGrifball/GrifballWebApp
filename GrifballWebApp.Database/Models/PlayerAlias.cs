#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// Another Xbox account (a different XUID) a player has also played on. An alias exists only when a player genuinely
/// had more than one Xbox account. It is not a record of gamertag changes: a renamed gamertag is still the same account,
/// tracked by the user's <see cref="XboxUser"/> (keyed by XUID, its stored gamertag updated when it is looked up), so
/// renames never get a row here, and the historical import creates none.
/// </summary>
public class PlayerAlias : AuditableEntity
{
    public int PlayerAliasID { get; set; }
    public int UserID { get; set; }
    public int? GameVersionID { get; set; }

    /// <summary>The other account's gamertag, as written.</summary>
    public string Alias { get; set; }
    /// <summary><see cref="Alias"/> trimmed, inner whitespace collapsed and upper-cased: what lookups compare.</summary>
    public string NormalizedAlias { get; set; }
    public bool IsPrimary { get; set; }
    public string Source { get; set; }

    public virtual User User { get; set; }
    public virtual GameVersion GameVersion { get; set; }
}
