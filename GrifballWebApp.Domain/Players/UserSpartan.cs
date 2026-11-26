namespace GrifballWebApp.Domain.Players;

/// <summary>
/// A user's own choice of the Spartan their profile shows, instead of the one they have equipped in Halo Infinite
/// (<see cref="XboxUserArmor"/>). One row per user; no row means the equipped set. <see cref="ChoiceJson"/> is the choice as
/// the render service names it (core, armor by region, parts by id, coating), checked against the render service's catalog
/// when it was set.
/// </summary>
public class UserSpartan
{
    public const int MaxChoiceLength = 4000;

    public int UserId { get; set; }
    public string ChoiceJson { get; set; } = "";
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
