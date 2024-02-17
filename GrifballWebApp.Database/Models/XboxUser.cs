#nullable disable

namespace GrifballWebApp.Database.Models;

public partial class XboxUser
{
    public XboxUser()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }

    public long XboxUserID { get; set; }
    public string Gamertag { get; set; }

    public Person Person { get; set; }

    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}
