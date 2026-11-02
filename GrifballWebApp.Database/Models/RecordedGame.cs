namespace GrifballWebApp.Database.Models;

/// <summary>
/// A game that is not a Halo Infinite match from Waypoint: an MCC (or older) game recorded from a carnage report or a
/// stats sheet. In a season through its <see cref="SeriesGame"/>, or a matchmaking (Late League) game through its
/// <see cref="MatchedMatch"/>.
/// </summary>
public class RecordedGame : AuditableEntity
{
    public int RecordedGameID { get; set; }
    public int GameVersionID { get; set; }
    public GameVersion GameVersion { get; set; } = null!;
    /// <summary>The season the game counts for, what the import finds a season's games by; null for a matchmaking game.</summary>
    public int? SeasonID { get; set; }
    public Season? Season { get; set; }
    public string? Map { get; set; }
    public string? Gametype { get; set; }
    public DateTime? PlayedAt { get; set; }
    /// <summary>The import's key for the game (its source file and game), what a re-import finds it by. Unique.</summary>
    public string SourceKey { get; set; } = null!;
    /// <summary>A playoff game, as its source says; a game in a series is one when its series is in the bracket.</summary>
    public bool IsPlayoffs { get; set; }

    public SeriesGame? SeriesGame { get; set; }
    public MatchedMatch? MatchedMatch { get; set; }
    public ICollection<RecordedGameTeam> Teams { get; set; } = new HashSet<RecordedGameTeam>();
    public ICollection<RecordedGameParticipant> Participants { get; set; } = new HashSet<RecordedGameParticipant>();
}
