#nullable disable

namespace GrifballWebApp.Database.Models;
public class GameVersion : AuditableEntity
{
    public GameVersion()
    {
        UserExperiences = new HashSet<UserExperience>();
    }
    public int GameVesionID { get; set; }
    public string GameVersionName { get; set; }
    public virtual ICollection<UserExperience> UserExperiences { get; set; }
}
