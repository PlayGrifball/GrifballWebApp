#nullable disable

namespace GrifballWebApp.Database.Models;
public class UserExperience : AuditableEntity
{
    public int UserID { get; set; }
    public int GameVersionID { get; set; }
    public User User { get; set; }
    public GameVersion GameVersion { get; set; }
}
