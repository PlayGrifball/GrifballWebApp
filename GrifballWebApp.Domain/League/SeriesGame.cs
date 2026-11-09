using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Shared;
namespace GrifballWebApp.Domain.League;

/// <summary>
/// A game of a season match's series (its best-of): either a Halo Infinite match pulled from Waypoint (<see cref="MatchID"/>)
/// or a game recorded some other way, e.g. an MCC carnage report (<see cref="RecordedGameID"/>). Exactly one of the two is set.
/// Made, numbered and taken out only through its season match (<see cref="SeasonMatch.AddGame(Match, int)"/>,
/// <see cref="SeasonMatch.NumberGame"/>, <see cref="SeasonMatch.RemoveGame"/>); internal, so tests may seed one directly.
/// </summary>
[AggregatePart(typeof(SeasonMatch))]
public class SeriesGame : AuditableEntity
{
    internal SeriesGame() { }

    public int SeriesGameID { get; internal set; }

    public int SeasonMatchID { get; internal set; }
    public SeasonMatch SeasonMatch { get; internal set; } = null!;

    /// <summary>The game's number in the best-of series: game 1 of 3 (BestOf is on the SeasonMatch).</summary>
    public int GameNumber { get; internal set; }

    public Guid? MatchID { get; internal set; }
    public Match? Match { get; internal set; }

    public int? RecordedGameID { get; internal set; }
    public RecordedGame? RecordedGame { get; internal set; }
}
