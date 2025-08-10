namespace GrifballWebApp.Database.Models;
public class DiscordMessage : AuditableEntity
{
    public required long Id { get; set; }
    public required long FromDiscordUserId { get; set; }
    public required long ToDiscordUserId { get; set; }
    public required string Content { get; set; }
    public required DateTime Timestamp { get; set; }
    public DiscordUser FromDiscordUser { get; set; } = null!;
    public DiscordUser ToDiscordUser { get; set; } = null!;
}
