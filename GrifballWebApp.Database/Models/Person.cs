#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Person
{
    public Person()
    {
        TeamPlayers = new List<TeamPlayer>();
    }
    public int PersonID { get; set; }
    public string Name { get; set; }
    public long XboxUserID { get; set; }
    public XboxUser XboxUser { get; set; }
    public Password Password { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
}
