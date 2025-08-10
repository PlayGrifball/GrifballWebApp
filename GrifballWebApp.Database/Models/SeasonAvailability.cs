#nullable disable

namespace GrifballWebApp.Database.Models;
public class SeasonAvailability : AuditableEntity
{
    public int SeasonID { get; set; }
    public int AvailabilityOptionID { get; set; }
    public Season Season { get; set; }
    public AvailabilityOption AvailabilityOption { get; set; }
}
