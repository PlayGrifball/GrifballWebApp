

namespace GrifballWebApp.Database.Models;
public partial class AvailabilityGridOption
{
    public AvailabilityGridOption()
    {
        TeamAvailability = new HashSet<TeamAvailability>();
    }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public virtual ICollection<TeamAvailability> TeamAvailability { get; set; }
}
