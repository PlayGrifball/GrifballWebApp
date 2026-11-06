namespace GrifballWebApp.Server.Profile;

/// <summary>A player's commendations: the Halo Infinite medals they have earned most, with a tier for each.</summary>
public class ProfileCommendationsDto
{
    public required int UserID { get; set; }
    /// <summary>Every medal the player has earned, of any kind.</summary>
    public int TotalMedals { get; set; }
    /// <summary>How many kinds of medal the player has earned.</summary>
    public int DistinctMedals { get; set; }
    /// <summary>The player's most earned medals, most first.</summary>
    public List<CommendationDto> Commendations { get; set; } = [];
}

public class CommendationDto
{
    public long MedalID { get; set; }
    public required string MedalName { get; set; }
    public string? Description { get; set; }
    public string? DifficultyName { get; set; }
    public string? TypeName { get; set; }
    /// <summary>Times earned.</summary>
    public int Count { get; set; }
    /// <summary>Games it was earned in.</summary>
    public int Matches { get; set; }
    /// <summary>Tiers reached (0 to <see cref="ProfileCommendationsService.TierThresholds"/>'s length).</summary>
    public int Tier { get; set; }
    /// <summary>The count of the next tier; null once the last is reached.</summary>
    public int? NextTierAt { get; set; }
    /// <summary>The count of the tier reached; 0 before the first.</summary>
    public int TierFloor { get; set; }
}
