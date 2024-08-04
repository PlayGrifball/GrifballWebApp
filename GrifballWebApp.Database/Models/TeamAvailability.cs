#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class TeamAvailability
{
    public int TeamID { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }

    public Team Team { get; set; }
    public AvailabilityGridOption AvailabilityGridOption { get; set; }
}
