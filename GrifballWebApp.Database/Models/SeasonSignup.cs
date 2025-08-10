#nullable disable

namespace GrifballWebApp.Database.Models;
public class SeasonSignup : AuditableEntity
{
    public SeasonSignup()
    {
        SignupAvailability = new HashSet<SignupAvailability>();
    }
    public int SeasonSignupID { get; set; }
    public int UserID { get; set; }
    public int SeasonID { get; set; }
    public DateTime Timestamp { get; set; }
    public string TeamName { get; set; }
    public bool WillCaptain { get; set; }
    public bool RequiresAssistanceDrafting { get; set; }
    // TODO: Remove this?
    public bool Approved { get; set; }
    public User User { get; set; }
    public Season Season { get; set; }
    public ICollection<SignupAvailability> SignupAvailability { get; set; }

    public SeasonSignup Copy()
    {
        return new SeasonSignup()
        {
            UserID = UserID,
            User = User,
            Timestamp = Timestamp,
            TeamName = TeamName,
            WillCaptain = WillCaptain,
            RequiresAssistanceDrafting = RequiresAssistanceDrafting,
            Approved = Approved,
            SignupAvailability = SignupAvailability.Select(x => x.Copy()).ToArray(),
        };
    }
}
