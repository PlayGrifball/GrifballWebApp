namespace GrifballWebApp.Application.Signups;

/// <summary>A player without a gamertag signing up for a season: matches are played on Halo, and the gamertag is the name
/// the site shows them by.</summary>
public class SignupNeedsGamertagException : Exception
{
    public const string Reason = "Set your gamertag before signing up: on your profile, or with the Set Gamertag button on Discord";

    public SignupNeedsGamertagException() : base(Reason) { }
}
