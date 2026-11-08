
namespace GrifballWebApp.Database.Models;

public class XboxUser : AuditableEntity
{
    public long XboxUserID { get; set; }
    public required string Gamertag { get; set; }

    public User? User { get; set; }
    // Its games' players: made only by their match (Match.AddParticipant), so read-only here (EF uses the field)
    public IReadOnlyCollection<MatchParticipant> MatchParticipants => _matchParticipants;
    private readonly HashSet<MatchParticipant> _matchParticipants = [];
}
