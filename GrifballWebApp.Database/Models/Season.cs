#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Season
{
    public Season()
    {
        Teams = new HashSet<Team>();
        SeasonMatches = new HashSet<SeasonMatch>();
        SeasonSignups = new HashSet<SeasonSignup>();
    }
    public int SeasonID { get; set; }
    public string SeasonName { get; set; }
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
    public DateTime DraftStart { get; set; }
    public DateTime SeasonStart { get; set; }
    public DateTime SeasonEnd { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
    public virtual ICollection<SeasonMatch> SeasonMatches { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
}
