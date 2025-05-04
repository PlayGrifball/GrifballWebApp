
namespace GrifballWebApp.Database.Models;
public class MatchedWinnerVote
{
    public int MatchId { get; set; }
    public MatchedMatch MatchedMatch { get; set; } = null!;
    public long DiscordUserId { get; set; }
    public DiscordUser DiscordUser { get; set; } = null!;
    public WinnerVote WinnerVote { get; set; }
}

public enum WinnerVote
{
    Home,
    Away,
    Cancel,
}