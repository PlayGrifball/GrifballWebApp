
namespace GrifballWebApp.Database.Models;
/// <summary>A player's vote for a match's winner. Made and changed only by the match (<see cref="MatchedMatch.VoteForWinner"/>).</summary>
[AggregatePart(typeof(MatchedMatch))]
public class MatchedWinnerVote : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MatchedWinnerVote() { }

    internal MatchedWinnerVote(MatchedMatch match, MatchedPlayer player, WinnerVote vote)
    {
        MatchedMatch = match;
        MatchedPlayer = player;
        WinnerVote = vote;
    }

    public int MatchId { get; private set; }
    public MatchedMatch MatchedMatch { get; private set; } = null!;
    public int MatchedPlayerId { get; private set; }
    public MatchedPlayer MatchedPlayer { get; private set; } = null!;
    /// <summary>Changed only by the match, when the player votes again (<see cref="MatchedMatch.VoteForWinner"/>).</summary>
    public WinnerVote WinnerVote { get; private set; }

    internal void Change(WinnerVote vote) => WinnerVote = vote;
}

public enum WinnerVote
{
    Home,
    Away,
    Cancel,
}