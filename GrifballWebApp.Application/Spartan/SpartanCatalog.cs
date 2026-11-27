namespace GrifballWebApp.Application.Spartan;

/// <summary>A piece a core can wear in a region: <see cref="Name"/> is what a loadout's armor names it, null for the pieces (some kits) reached only by ids.</summary>
public sealed record SpartanCatalogPiece(int Permutation, string? Name);

/// <summary>A region of a core. <see cref="Kind"/> is REGION (named by <see cref="Key"/> in a loadout's armor) or KIT (a kit's own regions, reached by ids).</summary>
public sealed record SpartanCatalogRegion(string Kind, string Key, int Region, IReadOnlyList<SpartanCatalogPiece> Pieces);

public sealed record SpartanCatalogCore(string Name, IReadOnlyList<SpartanCatalogRegion> Regions);

/// <summary>What the render service says a loadout may name. <see cref="PendingCores"/> are still being prepared there.</summary>
public sealed record SpartanCatalog(
    IReadOnlyList<SpartanCatalogCore> Cores,
    IReadOnlyList<string> Coatings,
    IReadOnlyList<string> PendingCores,
    IReadOnlyList<string> Visors);

/// <summary>The render service's catalog, kept for a few minutes; null when the service cannot be reached.</summary>
public interface ISpartanCatalogSource
{
    Task<SpartanCatalog?> Get(CancellationToken ct = default);
}

/// <summary>A piece by the game's ids, for the pieces that have no name.</summary>
public sealed record SpartanPartChoice(int Region, int Permutation);

/// <summary>A Spartan as a player chose it: the core, pieces by region key, pieces by ids, the coating and the visor. Anything left out is the core's default.</summary>
public sealed record SpartanChoice(
    string Core,
    IReadOnlyDictionary<string, string>? Armor = null,
    IReadOnlyList<SpartanPartChoice>? Parts = null,
    string? Coating = null,
    string? Visor = null);
