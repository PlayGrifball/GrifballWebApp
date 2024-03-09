#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class GameVersion
{
    public GameVersion()
    {
        PersonExperiences = new HashSet<PersonExperience>();
    }
    public int GameVesionID { get; set; }
    public string GameVersionName { get; set; }
    public virtual ICollection<PersonExperience> PersonExperiences { get; set; }
}
