

namespace GrifballWebApp.Database.Models;
public class AvailabilityOption : AuditableEntity
{
    public AvailabilityOption()
    {
        TeamAvailability = new HashSet<TeamAvailability>();
        SignupAvailability = new HashSet<SignupAvailability>();
    }
    public int AvailabilityOptionID { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public virtual ICollection<TeamAvailability> TeamAvailability { get; set; }
    public virtual ICollection<SignupAvailability> SignupAvailability { get; set; }
    // Read-only: a season sets its timeslots (Season.SetAvailability; EF uses the field)
    public IReadOnlyCollection<SeasonAvailability> SeasonAvailability => _seasonAvailability;
    private readonly HashSet<SeasonAvailability> _seasonAvailability = [];
}
