namespace GrifballWebApp.Domain.Models;

/// <summary>A timeslot a signed-up player can play in. Part of its signup: made and removed only by the signup's methods
/// (<see cref="SeasonSignup.SignUp"/>, <see cref="SeasonSignup.Change"/>, <see cref="SeasonSignup.Copy"/>).</summary>
[AggregatePart(typeof(SeasonSignup))]
public class SignupAvailability : AuditableEntity
{
    /// <summary>For EF.</summary>
    private SignupAvailability() { }

    /// <summary>A timeslot of its signup: the signup gives its key when it is saved.</summary>
    internal SignupAvailability(int availabilityOptionID) => AvailabilityOptionID = availabilityOptionID;

    public int SeasonSignupID { get; internal set; }
    public int AvailabilityOptionID { get; internal set; }
    public SeasonSignup SeasonSignup { get; internal set; } = null!;
    public AvailabilityOption AvailabilityOption { get; internal set; } = null!;

    internal SignupAvailability Copy() => new(AvailabilityOptionID) { AvailabilityOption = AvailabilityOption };
}
