#nullable disable

namespace GrifballWebApp.Database.Models;

public class PlayerAlias : AuditableEntity
{
    public int PlayerAliasID { get; set; }
    public int UserID { get; set; }
    public int? GameVersionID { get; set; }

    public string Alias { get; set; }
    public string NormalizedAlias { get; set; }
    public bool IsPrimary { get; set; }
    public string Source { get; set; }

    public virtual User User { get; set; }
    public virtual GameVersion GameVersion { get; set; }
}
