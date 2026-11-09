using GrifballWebApp.Domain.Shared;
#nullable disable

namespace GrifballWebApp.Domain.Halo;

/// <summary>
/// A Halo Infinite medal: reference data the medal import writes once from Halo's medal list (<see cref="Imported"/>,
/// DataPullService.DownloadMedals), keyed by Halo's own ID. Nothing changes it after: nothing outside it sets its fields,
/// and EF uses the private setters. The medals a player earned are their match's (<see cref="MedalEarned"/>).
/// </summary>
[AggregateRoot]
public class Medal : AuditableEntity
{
    /// <summary>For EF. A medal begins through <see cref="Imported"/>.</summary>
    private Medal() { }

    /// <summary>A medal from Halo's medal list, of a type and a difficulty imported with it.</summary>
    public static Medal Imported(long medalID, string medalName, string description, int spriteIndex, int sortingWeight,
        int medalDifficultyID, int medalTypeID, int personalScore) => new()
    {
        MedalID = medalID,
        MedalName = medalName,
        Description = description,
        SpriteIndex = spriteIndex,
        SortingWeight = sortingWeight,
        MedalDifficultyID = medalDifficultyID,
        MedalTypeID = medalTypeID,
        PersonalScore = personalScore,
    };

    // The key is internal: it comes from Halo, and tests may give one
    public long MedalID { get; internal set; }
    public string MedalName { get; private set; }
    public string Description { get; private set; }
    public int SpriteIndex { get; private set; }
    public int SortingWeight { get; private set; }
    public int MedalDifficultyID { get; private set; }
    public int MedalTypeID { get; private set; }
    public int PersonalScore { get; private set; }

    public MedalDifficulty MedalDifficulty { get; private set; }
    public MedalType MedalType { get; private set; }

    // Earned in matches: made only by their match (Match.AddParticipant), so read-only here (EF uses the field)
    public IReadOnlyCollection<MedalEarned> MedalEarned => _medalEarned;
    private readonly HashSet<MedalEarned> _medalEarned = new();
}
