namespace GrifballWebApp.Database.Models;
public class DiscordUser
{
    public long DiscordUserID { get; set; }
    public string DiscordUsername { get; set; } = null!;
    public User? User { get; set; }
}
