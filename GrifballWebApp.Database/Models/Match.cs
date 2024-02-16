#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace GrifballWebApp.Database.Models;
public partial class Match
{
    public Match()
    {
        MatchParticipants = new HashSet<MatchParticipant>();
    }
    public Guid MatchID { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    //[InverseProperty(nameof(MatchLink.Match))]
    public MatchLink MatchLink { get; set; }

    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; }
}
