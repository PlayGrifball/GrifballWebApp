#nullable disable

namespace GrifballWebApp.Database.Models;
public class Team : AuditableEntity
{
    public Team()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        HomeMatches = new HashSet<SeasonMatch>();
        AwayMatches = new HashSet<SeasonMatch>();
        TeamAvailability = new HashSet<TeamAvailability>();
    }
    public int SeasonID { get; set; }
    public Season Season { get; set; }
    public int TeamID { get; set; }
    public string TeamName { get; set; }
    public int? CaptainID { get; set; }
    public TeamPlayer Captain { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<SeasonMatch> HomeMatches { get; set; }
    public virtual ICollection<SeasonMatch> AwayMatches { get; set; }
    public virtual ICollection<TeamAvailability> TeamAvailability { get; set; }
    /// <summary>
    /// Copies team and team players, note that captains cannot be copied and must be set after initial save. It is returned as a out parameters to be set by caller after initial save changes
    /// </summary>
    public Team Copy(out TeamPlayer captain)
    {
        var teamPlayers = TeamPlayers.Select(x => x.Copy(this)).ToArray();
        captain = teamPlayers.FirstOrDefault(x => x.CaptainTeam is not null);

        if (captain is not null)
        {
            captain.CaptainTeam = null;
        }

        return new Team()
        {
           TeamName = TeamName,
           TeamPlayers = teamPlayers,
           TeamAvailability = TeamAvailability.Select(x => x.Copy()).ToArray(),
        };
    }
}
