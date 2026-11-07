
namespace GrifballWebApp.Database.Models;
/// <summary>A player's vote for a match's winner. Made and changed only by the match (<see cref="MatchedMatch.VoteForWinner"/>).</summary>
public class MatchedWinnerVote : AuditableEntity
{
    internal MatchedWinnerVote() { }

    public int MatchId { get; internal set; }
    public MatchedMatch MatchedMatch { get; internal set; } = null!;
    public int MatchedPlayerId { get; internal set; }
    public MatchedPlayer MatchedPlayer { get; internal set; } = null!;
    public WinnerVote WinnerVote { get; internal set; }
}

public enum WinnerVote
{
    Home,
    Away,
    Cancel,
}