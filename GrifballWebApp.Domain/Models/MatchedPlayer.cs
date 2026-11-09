using System.Linq.Expressions;

namespace GrifballWebApp.Domain.Models;
/// <summary>A user on a side of a <see cref="MatchedMatch"/>. Made, moved and changed only by the match, which owns it.</summary>
[AggregatePart(typeof(MatchedMatch))]
public class MatchedPlayer : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MatchedPlayer() { }

    /// <summary>A user's player, made by the match: in it, not kicked (<see cref="Kick"/>).</summary>
    internal MatchedPlayer(int userID) => UserID = userID;

    /// <summary>The same, with its user (for tests).</summary>
    internal MatchedPlayer(User user) : this(user.Id) => User = user;

    // Nothing outside the match sets its state, its foreign keys or its navigations: only the methods below, which the match
    // calls. The key is internal: the database makes it, and tests may give one.
    public int Id { get; internal set; }
    /// <summary>Changed only by the match (<see cref="MatchedMatch.ReassignPlayer"/>, for a user merge).</summary>
    public int UserID { get; private set; }
    /// <summary>Voted out of the match by its players (<see cref="MatchedMatch.VoteToKick"/>): no longer in it.</summary>
    public bool Kicked { get; private set; }
    public User User { get; private set; } = null!;
    public int MatchedTeamID { get; private set; }
    public MatchedTeam MatchedTeam { get; private set; } = null!;

    // Read-only: a vote is made by the match (MatchedMatch.VoteForWinner, VoteToKick)
    public IReadOnlyCollection<MatchedWinnerVote> MatchedWinnerVotes => _matchedWinnerVotes;
    private readonly HashSet<MatchedWinnerVote> _matchedWinnerVotes = [];
    public IReadOnlyCollection<MatchedKickVote> VoterMatchedKickVotes => _voterMatchedKickVotes;
    private readonly HashSet<MatchedKickVote> _voterMatchedKickVotes = [];
    public IReadOnlyCollection<MatchedKickVote> KickMatchedKickVotes => _kickMatchedKickVotes;
    private readonly HashSet<MatchedKickVote> _kickMatchedKickVotes = [];

    /// <summary>The player becomes another user's (a user merge, <see cref="MatchedMatch.ReassignPlayer"/>).</summary>
    internal void ReassignTo(int userID) => UserID = userID;

    /// <summary>Out of the match: a majority of its players voted so (<see cref="MatchedMatch.DecideKick"/>). Once.</summary>
    internal void Kick()
    {
        if (Kicked)
            throw new InvalidOperationException($"Player {Id} is already kicked");
        Kicked = true;
    }

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
    /// <summary>The player's side is the one its players voted the winner.</summary>
    public bool Won { get; init; }
}
