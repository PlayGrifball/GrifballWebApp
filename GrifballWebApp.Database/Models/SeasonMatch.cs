#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace GrifballWebApp.Database.Models;
public partial class SeasonMatch
{
    public int SeasonMatchID { get; set; }
    public int SeasonID { get; set; }
    public Season Season { get; set; }
    public int? HomeTeamID { get; set; }
    public Team HomeTeam { get; set; }
    public int? AwayTeamID { get; set; }
    public Team AwayTeam { get; set; }
    public DateTime? ScheduledTime { get; set; }
    //[InverseProperty(nameof(MatchLink.SeasonMatch))]
    public MatchLink MatchLink { get; set; }
    public MatchBracketInfo BracketMatch { get; set; }
}
