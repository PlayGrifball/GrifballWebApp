﻿
namespace GrifballWebApp.Database.Models;

public partial class XboxUser
{
    public XboxUser()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }

    public long XboxUserID { get; set; }
    public string Gamertag { get; set; }

    public User? User { get; set; }
    public DiscordUser? DiscordUser { get; set; }

    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}
