

namespace GrifballWebApp.Database.Models;
public partial class AvailabilityOption
{
    public AvailabilityOption()
    {
        TeamAvailability = new HashSet<TeamAvailability>();
        SignupAvailability = new HashSet<SignupAvailability>();
        SeasonAvailability = new HashSet<SeasonAvailability>();
    }
    public int AvailabilityOptionID { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public virtual ICollection<TeamAvailability> TeamAvailability { get; set; }
    public virtual ICollection<SignupAvailability> SignupAvailability { get; set; }
    public virtual ICollection<SeasonAvailability> SeasonAvailability { get; set; }
}
