namespace GrifballWebApp.Database.Models;
public class DiscordUser
{
    public long DiscordUserID { get; set; }
    public string DiscordUsername { get; set; }
    public long? XboxUserID { get; set; }
    public XboxUser? XboxUser { get; set; }
}
