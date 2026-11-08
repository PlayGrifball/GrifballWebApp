#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A medal's difficulty, as Halo Infinite lists them: reference data the medal import writes once
/// (<see cref="Imported"/>, DataPullService.DownloadMedals). Nothing changes it after: nothing outside it sets its fields,
/// and EF uses the private setters.
/// </summary>
[AggregateRoot]
public class MedalDifficulty : AuditableEntity
{
    /// <summary>For EF. A difficulty begins through <see cref="Imported"/>.</summary>
    private MedalDifficulty() { }

    /// <summary>A difficulty from Halo's medal list, keyed by its place in that list (index + 1, as Halo gives no ID).</summary>
    public static MedalDifficulty Imported(int medalDifficultyID, string medalDifficultyName) => new() { MedalDifficultyID = medalDifficultyID, MedalDifficultyName = medalDifficultyName };

    // The key is internal: it comes from Halo's list, and tests may give one
    public int MedalDifficultyID { get; internal set; }
    public string MedalDifficultyName { get; private set; }

    // Read-only: a medal names its difficulty (EF uses the field)
    public IReadOnlyCollection<Medal> Medals => _medals;
    private readonly HashSet<Medal> _medals = [];
}
