#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace GrifballWebApp.Database.Models;
public partial class MatchLink
{
    //public Guid MatchLinkID { get; set; }
    public Guid MatchID { get; set; }
    //[ForeignKey(nameof(MatchID))]
    //[InverseProperty(nameof(Match.MatchLink))]
    public Match Match { get; set; }



    public int SeasonMatchID { get; set; }

    //[ForeignKey(nameof(SeasonMatchID))]
    //[InverseProperty(nameof(SeasonMatch.MatchLink))]
    public SeasonMatch SeasonMatch { get; set; }
}
