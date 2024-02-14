#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Season
{
    public Season()
    {
        Teams = new HashSet<Team>();
    }
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
}
