namespace GrifballWebApp.Database.Models;

/// <summary>A team a VOD shows: what puts it on the team's page (and its players' profiles). Part of its VOD: made and
/// removed only by the VOD's methods (<see cref="Vod.Rematch"/>, <see cref="Vod.Confirm"/>).</summary>
[AggregatePart(typeof(Vod))]
public class VodTeam : AuditableEntity
{
    /// <summary>For EF.</summary>
    private VodTeam() { }

    /// <summary>A link of its VOD to a team: the VOD gives its key when it is saved.</summary>
    internal VodTeam(int teamID) => TeamID = teamID;

    public int VodID { get; internal set; }
    public Vod Vod { get; internal set; } = null!;
    public int TeamID { get; internal set; }
    public Team Team { get; internal set; } = null!;
}
