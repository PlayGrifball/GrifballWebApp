#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class SeasonSignup
{
    public int PersonID { get; set; }
    public int SeasonID { get; set; }
    public DateTime Timestamp { get; set; }
    // TODO: Availablity
    public string TeamName { get; set; }
    public bool WillCaptain { get; set; }
    public bool RequiresAssistanceDrafting { get; set; }
    public Person Person { get; set; }
    public Season Season { get; set; }
}
