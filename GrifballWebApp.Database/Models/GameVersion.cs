#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class GameVersion
{
    public GameVersion()
    {
        UserExperiences = new HashSet<UserExperience>();
    }
    public int GameVesionID { get; set; }
    public string GameVersionName { get; set; }
    public virtual ICollection<UserExperience> UserExperiences { get; set; }
}
