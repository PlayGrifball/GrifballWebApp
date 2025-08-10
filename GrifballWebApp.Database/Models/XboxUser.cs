
namespace GrifballWebApp.Database.Models;

public class XboxUser : AuditableEntity
{
    public XboxUser()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }

    public long XboxUserID { get; set; }
    public required string Gamertag { get; set; }

    public User? User { get; set; }
    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}
