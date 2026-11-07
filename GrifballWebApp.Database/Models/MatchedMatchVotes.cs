namespace GrifballWebApp.Database.Models;

/// <summary>What came of a vote for a match's winner (<see cref="MatchedMatch.VoteForWinner"/>).</summary>
public enum WinnerVoteResult
{
    /// <summary>Counted: a first vote, or a change of the player's vote.</summary>
    Voted,
    MatchOver,
    NotAPlayer,
    VoterKicked,
}

/// <summary>What came of a vote to kick a player (<see cref="MatchedMatch.VoteToKick"/>).</summary>
public enum KickVoteResult
{
    /// <summary>Counted, short of a majority.</summary>
    Voted,
    /// <summary>Counted, and a majority now wants them out: they are kicked.</summary>
    Kicked,
    MatchOver,
    NotAPlayer,
    VoterKicked,
    TargetNotAPlayer,
    TargetAlreadyKicked,
    /// <summary>The voter already voted to kick them: a vote to kick is not taken back or counted twice.</summary>
    AlreadyVoted,
}

/// <summary>A vote to kick, with the two players it is between (null where the match has no such player).</summary>
public sealed record KickVote(KickVoteResult Result, MatchedPlayer? Voter, MatchedPlayer? Target);

/// <summary>What came of moving a user's player in a match to another user (<see cref="MatchedMatch.ReassignPlayer"/>).</summary>
public enum PlayerReassignment
{
    /// <summary>The source user has no player in the match: nothing to move.</summary>
    NotAPlayer,
    /// <summary>The player is the target user's now.</summary>
    Moved,
    /// <summary>The target was in the ended match too: the source's player went, with its votes.</summary>
    Dropped,
    /// <summary>Both are in a match still being played: refused, nothing changed.</summary>
    BothInActiveMatch,
}
