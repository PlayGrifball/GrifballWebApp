#nullable disable

using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Database.Models;
public partial class User : IdentityUser<int>
{
    public User()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        PersonExperiences = new HashSet<UserExperience>();
        SeasonSignups = new HashSet<SeasonSignup>();
        UserRoles = new HashSet<UserRole>();
        SecurityStamp = Guid.NewGuid().ToString();
    }
    public int? RegionID { get; set; }
    public Region Region { get; set; }
    public string DisplayName { get; set; }
    // TODO: Availablity
    public long? XboxUserID { get; set; }
    public bool IsDummyUser { get; set; }
    public XboxUser XboxUser { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<UserExperience> PersonExperiences { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }
}
