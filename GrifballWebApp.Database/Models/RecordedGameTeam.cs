namespace GrifballWebApp.Database.Models;

/// <summary>A side of a <see cref="RecordedGame"/>: the season's team it was, when known, and its result.</summary>
public class RecordedGameTeam : AuditableEntity
{
    public int RecordedGameTeamID { get; set; }
    public int RecordedGameID { get; set; }
    public RecordedGame RecordedGame { get; set; } = null!;
    /// <summary>The side as the source numbers it (1, 2): unique in a game.</summary>
    public int Side { get; set; }
    public int? TeamID { get; set; }
    public Team? Team { get; set; }
    /// <summary>The side's name as recorded.</summary>
    public string? Name { get; set; }
    public Outcomes Outcome { get; set; }
    public int? Score { get; set; }

    public ICollection<RecordedGameParticipant> Participants { get; set; } = new HashSet<RecordedGameParticipant>();
}
