#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A Halo game a season or a game is played in: reference data seeded with fixed IDs (GameVersionConfiguration's
/// HasData, through <see cref="Seed"/>), which the app only reads. Nothing outside it sets its fields, and EF uses the
/// private setters. Its name and slug are never blank (CK_Other_GameVersions_NameAndSlugNotBlank).
/// </summary>
[AggregateRoot]
public class GameVersion : AuditableEntity
{
    /// <summary>For EF. A game version is seeded (<see cref="Seed"/>).</summary>
    private GameVersion() { }

    /// <summary>A game version of the seed data (HasData), or of a test.</summary>
    internal static GameVersion Seed(string gameVersionName, string slug, int gameVersionID = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return new() { GameVesionID = gameVersionID, GameVersionName = gameVersionName, Slug = slug };
    }

    // The key is internal: the seed data gives it, and tests may give one
    public int GameVesionID { get; internal set; }
    public string GameVersionName { get; private set; }
    public string Slug { get; private set; }

    // Read-only: a user's experience and a season name their game (EF uses the fields)
    public IReadOnlyCollection<UserExperience> UserExperiences => _userExperiences;
    private readonly HashSet<UserExperience> _userExperiences = [];
    public IReadOnlyCollection<Season> Seasons => _seasons;
    private readonly HashSet<Season> _seasons = [];
}
