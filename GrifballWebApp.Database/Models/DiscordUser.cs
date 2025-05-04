namespace GrifballWebApp.Database.Models;
public class DiscordUser
{
    public DiscordUser()
    {
        MatchedPlayers = new HashSet<MatchedPlayer>();
    }
    public long DiscordUserID { get; set; }
    public string DiscordUsername { get; set; }
    public long? XboxUserID { get; set; }
    public XboxUser? XboxUser { get; set; }
    public QueuedPlayer? QueuedPlayer { get; set; }
    public ICollection<MatchedPlayer> MatchedPlayers { get; set; }
    public int MMR { get; set; } = 1000; // Default MMR for new players
    public int WinStreak { get; set; } = 0;
    public int LossStreak { get; set; } = 0;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
}
