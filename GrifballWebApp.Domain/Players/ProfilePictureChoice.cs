namespace GrifballWebApp.Domain.Players;

/// <summary>Which image stands for a user on the site: their Spartan headshot, Halo emblem or gamer picture; the others are the fallback when it is not stored.</summary>
public enum ProfilePictureChoice
{
    /// <summary>The Halo emblem.</summary>
    Emblem = 0,
    GamerPicture = 1,
    /// <summary>The headshot of the Spartan rendered for the user (the new default); the emblem, then the gamer picture, when none is rendered yet.</summary>
    Spartan = 2,
}
