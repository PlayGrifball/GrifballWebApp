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

    /// <summary>A player a vote-decided match counts a win or a loss for: still in it (not kicked) when its players voted
    /// the winner. A kicked player didn't play it out, so neither wins nor loses it. For queries; for a loaded match,
    /// <see cref="MatchedMatch.VoteWinners"/> and <see cref="MatchedMatch.VoteLosers"/>.</summary>
    public static readonly Expression<Func<MatchedPlayer, bool>> CountedByVote = Models.MatchedTeam.PlayerCountedByVote;

    /// <summary>The result of a player <see cref="CountedByVote"/> (for queries).</summary>
    public static readonly Expression<Func<MatchedPlayer, MatchedVoteResult>> VoteResult = Models.MatchedTeam.PlayerVoteResult;
}

/// <summary>
/// A player's result in a match decided by its players' vote (<see cref="MatchedMatchStatus.VoteDecided"/>): a game played,
/// won or lost, with no Halo match on record, so no box score (kills, goals and the rest). Read through
/// <see cref="MatchedMatchQueries.VoteResults"/>.
/// </summary>
public sealed class MatchedVoteResult
{
    /// <summary>The match's matchmaking season; null for the queue's matches.</summary>
    public int? MatchmakingSeasonID { get; init; }
    public int UserID { get; init; }
    /// <summary>The player's gamertag now; null for a user with no Xbox account.</summary>
    public string? Gamertag { get; init; }
    /// <summary>The player's side is the one its players voted the winner.</summary>
    public bool Won { get; init; }
}
