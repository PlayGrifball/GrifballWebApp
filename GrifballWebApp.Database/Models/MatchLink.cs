#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class MatchLink : AuditableEntity
{
    public int MatchLinkID { get; set; }

    public Guid MatchID { get; set; }
    public Match Match { get; set; }



    public int SeasonMatchID { get; set; }
    public SeasonMatch SeasonMatch { get; set; }

    /// <summary>
    /// The match number in a best of series. Ex: MatchNumber 1 of 3 (BestOf found on SeasonMatch)
    /// </summary>
    public int MatchNumber { get; set; }
}
