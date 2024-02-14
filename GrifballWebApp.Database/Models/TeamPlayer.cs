#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class TeamPlayer
{
    public int TeamPlayerID { get; set; }
    public int TeamID { get; set; }
    public Team Team { get; set; }
    public bool IsCaptain { get; set; }
    public long? XboxUserID { get; set; }
    public XboxUser XboxUser { get; set; }
}
