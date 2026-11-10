using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Signups;

/// <summary>A captain's team name longer than a team's can be (<see cref="SignupResult.TeamNameTooLong"/>): it becomes the
/// team's at the draft, so it is refused at signup rather than cut there.</summary>
public class SignupTeamNameTooLongException : Exception
{
    public static readonly string Reason = $"Team name must be {Team.TeamNameMaxLength} characters or fewer";

    public SignupTeamNameTooLongException() : base(Reason) { }
}
