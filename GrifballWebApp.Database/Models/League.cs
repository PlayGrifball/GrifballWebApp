#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A league seasons are played in. The app's own seasons are AGLA's (<see cref="DefaultLeagueID"/>); the
/// others are leagues whose seasons only come from the historical archive.</summary>
public class League : AuditableEntity
{
    /// <summary>AGLA, the league the app runs: a season made in the app is in it unless set otherwise.</summary>
    public const int DefaultLeagueID = 1;

    public League()
    {
        Seasons = new HashSet<Season>();
    }

    public int LeagueID { get; set; }
    public string LeagueName { get; set; }
    public string Slug { get; set; }

    public virtual ICollection<Season> Seasons { get; set; }
}
