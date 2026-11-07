namespace GrifballWebApp.Database.Models;
/// <summary>A player's vote to kick another from a match. Made only by the match (<see cref="MatchedMatch.VoteToKick"/>).</summary>
[AggregatePart(typeof(MatchedMatch))]
public class MatchedKickVote : AuditableEntity
{
    internal MatchedKickVote() { }

    public int MatchId { get; internal set; }
    public MatchedMatch MatchedMatch { get; internal set; } = null!;
    public int VoterMatchedPlayerId { get; internal set; }
    public MatchedPlayer VoterMatchedPlayer { get; internal set; } = null!;
    public int KickMatchedPlayerId { get; internal set; }
    public MatchedPlayer KickMatchedPlayer { get; internal set; } = null!;
}
