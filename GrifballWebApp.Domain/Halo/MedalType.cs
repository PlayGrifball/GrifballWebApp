using GrifballWebApp.Domain.Shared;
#nullable disable

namespace GrifballWebApp.Domain.Halo;

/// <summary>
/// A medal's type (spree, mode, multikill...), as Halo Infinite lists them: reference data the medal import writes once
/// (<see cref="Imported"/>, DataPullService.DownloadMedals). Nothing changes it after: nothing outside it sets its fields,
/// and EF uses the private setters.
/// </summary>
[AggregateRoot]
public class MedalType : AuditableEntity
{
    /// <summary>For EF. A type begins through <see cref="Imported"/>.</summary>
    private MedalType() { }

    /// <summary>A type from Halo's medal list, keyed by its place in that list (index + 1, as Halo gives no ID).</summary>
    public static MedalType Imported(int medalTypeID, string medalTypeName) => new() { MedalTypeID = medalTypeID, MedalTypeName = medalTypeName };

    // The key is internal: it comes from Halo's list, and tests may give one
    public int MedalTypeID { get; internal set; }
    public string MedalTypeName { get; private set; }

    // Read-only: a medal names its type (EF uses the field)
    public IReadOnlyCollection<Medal> Medals => _medals;
    private readonly HashSet<Medal> _medals = [];
}
