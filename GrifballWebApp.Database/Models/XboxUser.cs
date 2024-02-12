#nullable disable
using System.ComponentModel.DataAnnotations;

namespace GrifballWebApp.Database.Models;

public partial class XboxUser
{
    public XboxUser()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }

    public long XUID { get; set; }
    public string Gamertag { get; set; }

    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}
