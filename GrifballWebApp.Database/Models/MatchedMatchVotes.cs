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
