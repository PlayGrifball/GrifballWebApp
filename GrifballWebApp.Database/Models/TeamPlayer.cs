#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class TeamPlayer
{
    public int TeamPlayerID { get; set; }
    public int TeamID { get; set; }
    public Team Team { get; set; }
    public int UserID { get; set; }
    public User User { get; set; }
    public Team CaptainTeam { get; set; }
    public int? DraftCaptainOrder { get; set; }
    public int? DraftRound { get; set; }
    public int? DraftPick { get; set; }
    public TeamPlayer Copy(Team newTeam)
    {
        return new TeamPlayer()
        {
            User = User,
            UserID = UserID,
            CaptainTeam = CaptainTeam is null ? null : newTeam,
            DraftCaptainOrder = DraftCaptainOrder,
            DraftRound = DraftRound,
            DraftPick = DraftPick,
        };
    }
}
