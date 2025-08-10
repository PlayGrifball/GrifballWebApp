#nullable disable

namespace GrifballWebApp.Database.Models;
public class TeamAvailability : AuditableEntity
{
    public int TeamID { get; set; }
    public int AvailabilityOptionID { get; set; }
    public Team Team { get; set; }
    public AvailabilityOption AvailabilityOption { get; set; }
    public TeamAvailability Copy()
    {
        return new TeamAvailability()
        {
            AvailabilityOption = AvailabilityOption,
            AvailabilityOptionID = AvailabilityOptionID,
        };
    }
}
