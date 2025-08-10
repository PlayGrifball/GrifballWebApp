
namespace GrifballWebApp.Database.Models;
public class MatchedTeam : AuditableEntity
{
    public int MatchedTeamId { get; set; }
    public List<MatchedPlayer> Players { get; set; } = new List<MatchedPlayer>();

    public MatchedMatch? HomeMatchedMatch { get; set; }
    public MatchedMatch? AwayMatchedMatch { get; set; }
}
