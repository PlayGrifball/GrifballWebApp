
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
    public int UserID { get; set; }
    public bool Kicked { get; set; }
    public User User { get; set; } = null!;
    public int MatchedTeamID { get; set; }
    public MatchedTeam MatchedTeam { get; set; } = null!;
    public ICollection<MatchedWinnerVote> MatchedWinnerVotes { get; set; }
    public ICollection<MatchedKickVote> VoterMatchedKickVotes { get; set; }
    public ICollection<MatchedKickVote> KickMatchedKickVotes { get; set; }
}
