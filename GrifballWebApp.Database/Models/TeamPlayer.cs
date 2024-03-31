#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class TeamPlayer
{
    public int TeamPlayerID { get; set; }
    public int TeamID { get; set; }
    public Team Team { get; set; }
    public int PlayerID { get; set; }
    public Person Person { get; set; }
    public Team CaptainTeam { get; set; }
    public int? DraftCaptainOrder { get; set; }
    public int? DraftRound { get; set; }
    public int? DraftPick { get; set; }
}
