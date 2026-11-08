namespace GrifballWebApp.Database.Models;

/// <summary>
/// A player's line of a <see cref="RecordedGame"/>. The player is always a user (a placeholder until someone claims
/// the gamertag); what a game's source doesn't record is null. Part of its game: made, changed and removed only by the
/// game's methods (<see cref="RecordedGame.SetPlayers"/>, <see cref="RecordedGame.ReassignPlayer"/>).
/// </summary>
[AggregatePart(typeof(RecordedGame))]
public class RecordedGameParticipant : AuditableEntity
{
    /// <summary>For EF.</summary>
    private RecordedGameParticipant() { }

    /// <summary>A line of its game: the game gives its key when it is saved.</summary>
    internal RecordedGameParticipant(int userID) => UserID = userID;

    public int RecordedGameParticipantID { get; internal set; }
    public int RecordedGameID { get; private set; }
    public RecordedGame RecordedGame { get; private set; } = null!;
    public int? RecordedGameTeamID { get; private set; }
    public RecordedGameTeam? RecordedGameTeam { get; private set; }
    public int UserID { get; private set; }
    public User User { get; private set; } = null!;
    /// <summary>The gamertag as recorded.</summary>
    public string Gamertag { get; private set; } = null!;

    public int Kills { get; private set; }
    public int Deaths { get; private set; }
    public int Assists { get; private set; }
    public int? Score { get; private set; }
    public int? Goals { get; private set; }
    public int? CarrierKills { get; private set; }
    public int? Betrayals { get; private set; }
    public int? Suicides { get; private set; }
    public int? HammerKills { get; private set; }
    public int? SwordKills { get; private set; }
    public int? BallKills { get; private set; }
    public int? Punches { get; private set; }

    internal void Record(RecordedPlayerLine line, RecordedGameTeam? side)
    {
        ArgumentNullException.ThrowIfNull(line.Gamertag);
        RecordedGameTeam = side;
        if (side is null)
            RecordedGameTeamID = null;
        Gamertag = line.Gamertag;
        Kills = line.Kills;
        Deaths = line.Deaths;
        Assists = line.Assists;
        Score = line.Score;
        Goals = line.Goals;
        CarrierKills = line.CarrierKills;
        Betrayals = line.Betrayals;
        Suicides = line.Suicides;
        HammerKills = line.HammerKills;
        SwordKills = line.SwordKills;
        BallKills = line.BallKills;
        Punches = line.Punches;
    }

    /// <summary>The line is another user's now (a merge): EF points the navigation at them by the key.</summary>
    internal void MoveTo(int userID) => UserID = userID;
}
