#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Team
{
    public Team()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
    }
    public int SeasonID { get; set; }
    public Season Season { get; set; }
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
}
