namespace GrifballWebApp.Database.Models;
/// <summary>A player's vote to kick another from a match. Made only by the match (<see cref="MatchedMatch.VoteToKick"/>).</summary>
[AggregatePart(typeof(MatchedMatch))]
public class MatchedKickVote : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MatchedKickVote() { }

    internal MatchedKickVote(MatchedMatch match, MatchedPlayer voter, MatchedPlayer target)
    {
        MatchedMatch = match;
        VoterMatchedPlayer = voter;
        KickMatchedPlayer = target;
    }

    // Never changed: a vote to kick is not taken back
    public int MatchId { get; private set; }
    public MatchedMatch MatchedMatch { get; private set; } = null!;
    public int VoterMatchedPlayerId { get; private set; }
    public MatchedPlayer VoterMatchedPlayer { get; private set; } = null!;
    public int KickMatchedPlayerId { get; private set; }
    public MatchedPlayer KickMatchedPlayer { get; private set; } = null!;
}
