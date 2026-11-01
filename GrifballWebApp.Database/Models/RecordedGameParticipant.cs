namespace GrifballWebApp.Database.Models;

/// <summary>
/// A player's line of a <see cref="RecordedGame"/>. The player is always a user (a placeholder until someone claims
/// the gamertag); what a game's source doesn't record is null.
/// </summary>
public class RecordedGameParticipant : AuditableEntity
{
    public int RecordedGameParticipantID { get; set; }
    public int RecordedGameID { get; set; }
    public RecordedGame RecordedGame { get; set; } = null!;
    public int? RecordedGameTeamID { get; set; }
    public RecordedGameTeam? RecordedGameTeam { get; set; }
    public int UserID { get; set; }
    public User User { get; set; } = null!;
    /// <summary>The gamertag as recorded.</summary>
    public string Gamertag { get; set; } = null!;

    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int? Score { get; set; }
    public int? Goals { get; set; }
    public int? CarrierKills { get; set; }
    public int? Betrayals { get; set; }
    public int? Suicides { get; set; }
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }
}
