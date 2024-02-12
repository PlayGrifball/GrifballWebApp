#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class MedalEarned
{
    public long MedalID { get; set; }
    // Composite FK MatchID + XboxUserID
    public Guid MatchID { get; set; }
    public long XboxUserID { get; set; }
    public int Count { get; set; }
    public int TotalPersonalScoreAwarded { get; set; }

    public Medal Medal { get; set; }
    public MatchParticipant MatchParticipant { get; set; }
}
