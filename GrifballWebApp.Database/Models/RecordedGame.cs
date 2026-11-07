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
    /// <summary>The import's key for the game (its source file and game), what a re-import finds it by. Unique. A game
    /// entered by hand has a key of its own that nothing reads: <see cref="Origin"/> says where a game came from.</summary>
    public string SourceKey { get; set; } = null!;
    /// <summary>Where the game came from: the historical import, or entered by hand on the match page.</summary>
    public RecordedGameOrigin Origin { get; set; } = RecordedGameOrigin.HistoricalImport;
    /// <summary>A playoff game, as its source says; a game in a series is one when its series is in the bracket.</summary>
    public bool IsPlayoffs { get; set; }

    /// <summary>Its game of a season match's series: set from the match's side only (<see cref="SeasonMatch.AddGame(RecordedGame, int)"/>), which owns the link.</summary>
    public SeriesGame? SeriesGame { get; private set; }
    /// <summary>Set from the match's side only (<see cref="MatchedMatch.RecordArchived"/>), which owns the link.</summary>
    public MatchedMatch? MatchedMatch { get; private set; }
    public ICollection<RecordedGameTeam> Teams { get; set; } = new HashSet<RecordedGameTeam>();
    public ICollection<RecordedGameParticipant> Participants { get; set; } = new HashSet<RecordedGameParticipant>();
}
