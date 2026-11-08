namespace GrifballWebApp.Database.Models;

/// <summary>A side of a <see cref="RecordedGame"/>: the season's team it was, when known, and its result. Part of its game:
/// made, changed and removed only by the game's methods (<see cref="RecordedGame.SetSides"/>).</summary>
[AggregatePart(typeof(RecordedGame))]
public class RecordedGameTeam : AuditableEntity
{
    /// <summary>For EF.</summary>
    private RecordedGameTeam() { }

    /// <summary>A side of its game: the game gives its key when it is saved.</summary>
    internal RecordedGameTeam(int side) => Side = side;

    public int RecordedGameTeamID { get; internal set; }
    public int RecordedGameID { get; private set; }
    public RecordedGame RecordedGame { get; private set; } = null!;
    /// <summary>The side as the source numbers it (1, 2): unique in a game.</summary>
    public int Side { get; private set; }
    public int? TeamID { get; private set; }
    public Team? Team { get; private set; }
    /// <summary>The side's name as recorded.</summary>
    public string? Name { get; private set; }
    public Outcomes Outcome { get; private set; }
    public int? Score { get; private set; }

    // Read-only: a line is put on its side by the game (RecordedGame.SetPlayers); EF uses the field
    public IReadOnlyCollection<RecordedGameParticipant> Participants => _participants;
    private readonly HashSet<RecordedGameParticipant> _participants = [];

    internal void Record(RecordedSideFacts side)
    {
        // The key only: EF points the navigation at the team by it
        TeamID = side.TeamID;
        Name = side.Name;
        Outcome = side.Outcome;
        Score = side.Score;
    }

    internal void ClearTeam()
    {
        Team = null;
        TeamID = null;
    }
}
