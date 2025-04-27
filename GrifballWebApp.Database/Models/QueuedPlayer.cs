
namespace GrifballWebApp.Database.Models;
public class QueuedPlayer
{
    public long DiscordUserID { get; set; }
    public DiscordUser DiscordUser { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
