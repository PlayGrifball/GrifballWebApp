namespace GrifballWebApp.Database.Models;

/// <summary>Where a <see cref="MatchedMatch"/> is in its life. It moves only out of <see cref="Active"/>, through the
/// match's own methods (<see cref="MatchedMatch.Report"/>, <see cref="MatchedMatch.DecideByVote"/>,
/// <see cref="MatchedMatch.Cancel"/>).</summary>
public enum MatchedMatchStatus
{
    /// <summary>Being played: its players can vote, and can't queue again until it ends.</summary>
    Active = 1,
    /// <summary>Its result is on record: the Halo Infinite match it was (<see cref="MatchedMatch.MatchID"/>), or, for the
    /// archive, its recorded game (<see cref="MatchedMatch.RecordedGameID"/>).</summary>
    Reported = 2,
    /// <summary>Ended by its players' vote for a winner (<see cref="MatchedMatch.WinnerSide"/>), with no match on record.</summary>
    VoteDecided = 3,
    /// <summary>Called off: by vote, for running too long, or by a commissioner. Nobody won.</summary>
    Canceled = 4,
}

/// <summary>A side of a <see cref="MatchedMatch"/>.</summary>
public enum MatchedSide
{
    Home = 1,
    Away = 2,
}
