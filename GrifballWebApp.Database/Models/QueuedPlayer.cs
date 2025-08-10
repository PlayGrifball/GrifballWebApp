
namespace GrifballWebApp.Database.Models;
public class QueuedPlayer : AuditableEntity
{
    public int UserID { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
