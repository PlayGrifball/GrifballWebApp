
namespace GrifballWebApp.Database.Models;
public class QueuedPlayer
{
    public int UserID { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
