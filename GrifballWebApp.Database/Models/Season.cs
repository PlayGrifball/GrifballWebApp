#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Season
{
    public Season()
    {
        Teams = new HashSet<Team>();
        SeasonMatches = new HashSet<SeasonMatch>();
    }
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
    public virtual ICollection<SeasonMatch> SeasonMatches { get; set; }
}
