namespace GrifballWebApp.Database.Models;
public class MatchedKickVote
{
    public int MatchId { get; set; }
    public MatchedMatch MatchedMatch { get; set; } = null!;
    public int VoterMatchedPlayerId { get; set; }
    public MatchedPlayer VoterMatchedPlayer { get; set; } = null!;
    public int KickMatchedPlayerId { get; set; }
    public MatchedPlayer KickMatchedPlayer { get; set; } = null!;
}
