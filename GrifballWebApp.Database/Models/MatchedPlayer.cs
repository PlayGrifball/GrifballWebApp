
namespace GrifballWebApp.Database.Models;
public class MatchedPlayer
{
    public MatchedPlayer()
    {
        MatchedWinnerVotes = new HashSet<MatchedWinnerVote>();
        VoterMatchedKickVotes = new HashSet<MatchedKickVote>();
        KickMatchedKickVotes = new HashSet<MatchedKickVote>();
    }
    public int Id { get; set; }
    public long DiscordUserID { get; set; }
    public bool Kicked { get; set; }
    public DiscordUser DiscordUser { get; set; }
    public int MatchedTeamID { get; set; }
    public MatchedTeam MatchedTeam { get; set; }
    public ICollection<MatchedWinnerVote> MatchedWinnerVotes { get; set; }
    public ICollection<MatchedKickVote> VoterMatchedKickVotes { get; set; }
    public ICollection<MatchedKickVote> KickMatchedKickVotes { get; set; }
}
