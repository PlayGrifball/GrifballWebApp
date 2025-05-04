
namespace GrifballWebApp.Database.Models;
public class MatchedPlayer
{
    public int Id { get; set; }
    public long DiscordUserID { get; set; }
    public DiscordUser DiscordUser { get; set; }
    public int MatchedTeamID { get; set; }
    public MatchedTeam MatchedTeam { get; set; }
}
