#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Match
{
    public Match()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }
    public Guid MatchID { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}
