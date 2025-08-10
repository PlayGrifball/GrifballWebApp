namespace GrifballWebApp.Database.Models;
public class DiscordUser : AuditableEntity
{
    public DiscordUser()
    {
        ReceivedMessages = [];
        SentMessages = [];
    }
    public long DiscordUserID { get; set; }
    public string DiscordUsername { get; set; } = null!;
    public User? User { get; set; }
    public List<DiscordMessage> ReceivedMessages { get; set; }
    public List<DiscordMessage> SentMessages { get; set; }

}
