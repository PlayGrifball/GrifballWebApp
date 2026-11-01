namespace GrifballWebApp.Database.Models;

/// <summary>
/// A game of a season match's series (its best-of): either a Halo Infinite match pulled from Waypoint (<see cref="MatchID"/>)
/// or a game recorded some other way, e.g. an MCC carnage report (<see cref="RecordedGameID"/>). Exactly one of the two is set.
/// </summary>
public class SeriesGame : AuditableEntity
{
    public int SeriesGameID { get; set; }

    public int SeasonMatchID { get; set; }
    public SeasonMatch SeasonMatch { get; set; } = null!;

    /// <summary>The game's number in the best-of series: game 1 of 3 (BestOf is on the SeasonMatch).</summary>
    public int GameNumber { get; set; }

    public Guid? MatchID { get; set; }
    public Match? Match { get; set; }

    public int? RecordedGameID { get; set; }
    public RecordedGame? RecordedGame { get; set; }
}
