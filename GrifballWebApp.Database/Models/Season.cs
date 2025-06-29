#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Season
{
    public Season()
    {
        Teams = new HashSet<Team>();
        SeasonMatches = new HashSet<SeasonMatch>();
        SeasonSignups = new HashSet<SeasonSignup>();
        SeasonAvailability = new HashSet<SeasonAvailability>();
    }
    public int SeasonID { get; set; }
    public string SeasonName { get; set; }
    public bool CaptainsLocked { get; set; }
    /// <summary>
    /// The date and time when the season is/was made public. If this date has not passed then the season will only be visible to admins and commissioner.
    /// </summary>
    public DateTime PublicAt { get; set; }
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
    public DateTime DraftStart { get; set; }
    public DateTime SeasonStart { get; set; }
    public DateTime SeasonEnd { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
    public virtual ICollection<SeasonMatch> SeasonMatches { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
    public virtual ICollection<SeasonAvailability> SeasonAvailability { get; set; }
}
