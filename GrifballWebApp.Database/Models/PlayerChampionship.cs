#nullable disable

namespace GrifballWebApp.Database.Models;

public class PlayerChampionship : AuditableEntity
{
    public int PlayerChampionshipID { get; set; }
    public int UserID { get; set; }
    public int GameVersionID { get; set; }

    public string SeasonName { get; set; }
    public int Year { get; set; }
    public string LeagueName { get; set; }
    public string Tier { get; set; }
    public string TeamName { get; set; }
    public bool IsCaptain { get; set; }
    public string GamertagUsed { get; set; }

    public virtual User User { get; set; }
    public virtual GameVersion GameVersion { get; set; }
}
