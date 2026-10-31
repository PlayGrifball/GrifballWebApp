#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A player on a championship-winning roster: one row per (season, tier, player). Written by
/// ChampionshipService, both when a season's playoff bracket is decided in the app and by the historical import.</summary>
public class PlayerChampionship : AuditableEntity
{
    public int PlayerChampionshipID { get; set; }
    public int UserID { get; set; }
    public int SeasonID { get; set; }
    /// <summary>The game the title was won on, which can differ from the season's (a season with Halo 3 and Reach
    /// tiers). Null when the record names none the app knows (e.g. "MCC Split").</summary>
    public int? GameVersionID { get; set; }

    /// <summary>The division the title was won in, e.g. Pro, Amateur, Open: its season's <see cref="Season.Division"/>
    /// for a season of the app's (ChampionshipService), the champions record's tier for an imported one. Kept on the
    /// title because one archive season can have titles in more than one tier (its divisions shared a season name and
    /// schedule); the league is the season's.</summary>
    public string Tier { get; set; }
    /// <summary>The team that won the title.</summary>
    public int TeamID { get; set; }
    public bool IsCaptain { get; set; }
    /// <summary>The gamertag the record lists the player under.</summary>
    public string GamertagUsed { get; set; }

    public virtual User User { get; set; }
    public virtual Season Season { get; set; }
    public virtual Team Team { get; set; }
    public virtual GameVersion GameVersion { get; set; }
}
