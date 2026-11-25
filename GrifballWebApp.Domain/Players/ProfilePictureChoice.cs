namespace GrifballWebApp.Domain.Players;

/// <summary>Which of a user's Xbox images stands for them on the site; the other is the fallback when the account has none of it.</summary>
public enum ProfilePictureChoice
{
    /// <summary>The Halo emblem (the default).</summary>
    Emblem = 0,
    GamerPicture = 1,
}
