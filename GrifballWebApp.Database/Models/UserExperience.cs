#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class UserExperience
{
    public int UserID { get; set; }
    public int GameVersionID { get; set; }
    public User User { get; set; }
    public GameVersion GameVersion { get; set; }
}
