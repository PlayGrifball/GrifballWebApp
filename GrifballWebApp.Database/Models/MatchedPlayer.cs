using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;
/// <summary>A user on a side of a <see cref="MatchedMatch"/>. Made, moved and changed only by the match, which owns it.</summary>
public class MatchedPlayer : AuditableEntity
{
    /// <summary>For EF, and the match (tests may make one too).</summary>
    internal MatchedPlayer() { }

    internal MatchedPlayer(int userID) => UserID = userID;

    public int Id { get; internal set; }
    /// <summary>Changed only by the match (<see cref="MatchedMatch.ReassignPlayer"/>, for a user merge).</summary>
    public int UserID { get; internal set; }
    /// <summary>Voted out of the match by its players (<see cref="MatchedMatch.VoteToKick"/>): no longer in it.</summary>
    public bool Kicked { get; internal set; }
    public User User { get; internal set; } = null!;
    public int MatchedTeamID { get; private set; }
    public MatchedTeam MatchedTeam { get; private set; } = null!;

    // Read-only: a vote is made by the match (MatchedMatch.VoteForWinner, VoteToKick)
    public IReadOnlyCollection<MatchedWinnerVote> MatchedWinnerVotes => _matchedWinnerVotes;
    private readonly HashSet<MatchedWinnerVote> _matchedWinnerVotes = [];
    public IReadOnlyCollection<MatchedKickVote> VoterMatchedKickVotes => _voterMatchedKickVotes;
    private readonly HashSet<MatchedKickVote> _voterMatchedKickVotes = [];
    public IReadOnlyCollection<MatchedKickVote> KickMatchedKickVotes => _kickMatchedKickVotes;
    private readonly HashSet<MatchedKickVote> _kickMatchedKickVotes = [];

    /// <summary>A player still in a match being played: not kicked from it. What keeps them off the queue (for queries;
    /// it reads the match through its team's links to it, which only the team sees).</summary>
    public static readonly Expression<Func<MatchedPlayer, bool>> InActiveMatch = Models.MatchedTeam.PlayerInActiveMatch;
}
