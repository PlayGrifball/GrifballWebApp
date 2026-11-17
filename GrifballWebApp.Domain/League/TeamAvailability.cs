using GrifballWebApp.Domain.Shared;
#nullable disable

namespace GrifballWebApp.Domain.League;

/// <summary>A weekly timeslot a team can play in: part of its <see cref="Team"/>, copied with it (<see cref="Team.Copy"/>).</summary>
[AggregatePart(typeof(Team))]
public class TeamAvailability : AuditableEntity
{
    /// <summary>For EF. A timeslot is the team's through its team.</summary>
    private TeamAvailability() { }

    /// <summary><paramref name="team"/>'s timeslot at the option <paramref name="availabilityOptionID"/>, as its captain set it.</summary>
    internal static TeamAvailability Of(Team team, int availabilityOptionID) => new()
    {
        Team = team,
        TeamID = team.TeamID,
        AvailabilityOptionID = availabilityOptionID,
    };

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
