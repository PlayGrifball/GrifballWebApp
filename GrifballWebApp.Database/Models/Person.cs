#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Person
{
    public Person()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        PersonRoles = new HashSet<PersonRole>();
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
    public Password Password { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<PersonRole> PersonRoles { get; set; }
    public virtual ICollection<PersonExperience> PersonExperiences { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
}
