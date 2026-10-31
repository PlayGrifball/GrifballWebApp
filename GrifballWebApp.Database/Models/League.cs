#nullable disable

namespace GrifballWebApp.Database.Models;

public class League : AuditableEntity
{
    public League()
    {
        Seasons = new HashSet<Season>();
    }

    public int LeagueID { get; set; }
    public string LeagueName { get; set; }
    public string Slug { get; set; }

    public virtual ICollection<Season> Seasons { get; set; }
}
