#nullable disable

namespace GrifballWebApp.Database.Models;
public class MatchTeam
{
    public MatchTeam()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }
    public Guid MatchID { get; set; }
    public Match Match { get; set; }
    public int TeamID { get; set; }
    public int Score { get; set; }
    public Outcomes Outcome { get; set; }

    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}

public enum Outcomes
{
    // Does not appear that there is a 0 or 1? Ties also show as won
    Won = 2,
    Lost = 3
}
