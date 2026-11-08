namespace GrifballWebApp.Database.Models;

/// <summary>A game version a user played. Part of its user: made and removed only by the user's methods
/// (<see cref="User.TakeExperiencesFrom"/>).</summary>
[AggregatePart(typeof(User))]
public class UserExperience : AuditableEntity
{
    /// <summary>For EF.</summary>
    private UserExperience() { }

    /// <summary>The user's experience of a game version: the user gives its key when it is saved.</summary>
    internal UserExperience(int gameVersionID) => GameVersionID = gameVersionID;

    public int UserID { get; internal set; }
    public int GameVersionID { get; internal set; }
    public User User { get; internal set; } = null!;
    public GameVersion GameVersion { get; internal set; } = null!;
}
