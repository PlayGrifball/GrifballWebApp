#nullable disable

namespace GrifballWebApp.Domain.Models;

/// <summary>A weekly timeslot a team can play in: part of its <see cref="Models.Team"/>, copied with it (<see cref="Team.Copy"/>).</summary>
[AggregatePart(typeof(Team))]
public class TeamAvailability : AuditableEntity
{
    /// <summary>For EF. A timeslot is the team's through its team.</summary>
    private TeamAvailability() { }

    /// <summary>A timeslot of saved rows by their keys: for tests, which seed a team's timeslots.</summary>
    internal static TeamAvailability Seed(int teamID, int availabilityOptionID = 0) => new()
    {
        TeamID = teamID,
        AvailabilityOptionID = availabilityOptionID,
    };

    public int TeamID { get; private set; }
    public int AvailabilityOptionID { get; private set; }
    public Team Team { get; private set; }
    public AvailabilityOption AvailabilityOption { get; private set; }

    internal TeamAvailability Copy(Team newTeam) => new()
    {
        Team = newTeam,
        AvailabilityOption = AvailabilityOption,
        AvailabilityOptionID = AvailabilityOptionID,
    };
}
