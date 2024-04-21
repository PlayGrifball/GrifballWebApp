#nullable disable

using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Database.Models;
public partial class Person// : IdentityUser
{
    public Person()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        PersonExperiences = new HashSet<PersonExperience>();
        SeasonSignups = new HashSet<SeasonSignup>();
    }
    public int PersonID { get; set; }
    public int? RegionID { get; set; }
    public Region Region { get; set; }
    public string Name { get; set; }
    // TODO: Availablity
    public long XboxUserID { get; set; }
    public XboxUser XboxUser { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<PersonExperience> PersonExperiences { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
}
