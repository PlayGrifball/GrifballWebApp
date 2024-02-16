#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Team
{
    public Team()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        HomeMatches = new HashSet<SeasonMatch>();
        AwayMatches = new HashSet<SeasonMatch>();
    }
    public int SeasonID { get; set; }
    public Season Season { get; set; }
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<SeasonMatch> HomeMatches { get; set; }
    public virtual ICollection<SeasonMatch> AwayMatches { get; set; }
}
