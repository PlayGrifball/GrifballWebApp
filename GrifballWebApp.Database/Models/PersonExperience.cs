#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class PersonExperience
{
    public int PersonID { get; set; }
    public int GameVersionID { get; set; }
    public Person Person { get; set; }
    public GameVersion GameVersion { get; set; }
}
