namespace GrifballWebApp.Application.Spartan;

/// <summary>A piece a core can wear in a region: <see cref="Name"/> is what a loadout's armor names it, null for the pieces (some kits) reached only by ids.</summary>
public sealed record SpartanCatalogPiece(int Permutation, string? Name);

/// <summary>A region of a core. <see cref="Kind"/> is REGION (named by <see cref="Key"/> in a loadout's armor) or KIT (a kit's own regions, reached by ids).</summary>
public sealed record SpartanCatalogRegion(string Kind, string Key, int Region, IReadOnlyList<SpartanCatalogPiece> Pieces);

public sealed record SpartanCatalogCore(string Name, IReadOnlyList<SpartanCatalogRegion> Regions);

/// <summary>
/// A weapon a stance can hold: <see cref="Tags"/> are the action pose weaponTagIds that mean it, <see cref="CoreType"/> the Halo weapon
/// core (e.g. WeaponCommando) whose equipped theme gives its coating (null when it has none), <see cref="Coatings"/> its coating ids.
/// </summary>
public sealed record SpartanCatalogWeapon(string Name, string? CoreType, IReadOnlyList<int> Tags, IReadOnlyList<string> Coatings);

/// <summary>A pose the render service can add to a model as a clip named <see cref="Name"/>: its stance id, and the weapon it holds (null for none).</summary>
public sealed record SpartanCatalogPose(string Name, int Stance, string? Weapon);

/// <summary>What the render service says a loadout may name. <see cref="PendingCores"/> are still being prepared there.</summary>
public sealed record SpartanCatalog(
    IReadOnlyList<SpartanCatalogCore> Cores,
    IReadOnlyList<string> Coatings,
    IReadOnlyList<string> PendingCores,
    IReadOnlyList<string> Visors,
    IReadOnlyList<SpartanCatalogWeapon>? Weapons = null,
    IReadOnlyList<SpartanCatalogPose>? Poses = null)
{
    /// <summary>The weapon an action pose's weaponTagId means, or null (no tag, or one no weapon has).</summary>
    public SpartanCatalogWeapon? WeaponOf(int? weaponTagId)
        => weaponTagId is int tag ? (Weapons ?? []).FirstOrDefault(w => w.Tags.Contains(tag)) : null;
}

/// <summary>The render service's catalog, kept for a few minutes; null when the service cannot be reached.</summary>
public interface ISpartanCatalogSource
{
    Task<SpartanCatalog?> Get(CancellationToken ct = default);
}

/// <summary>A piece by the game's ids, for the pieces that have no name.</summary>
public sealed record SpartanPartChoice(int Region, int Permutation);

/// <summary>A Spartan as a player chose it: the core, pieces by region key, pieces by ids, the coating, the visor and whether the damage and grime layer is left off (it is, unless chosen otherwise), and per weapon (by name) the coating it wears when a stance holds it. Anything left out is the core's default (a weapon's: the coating equipped in Halo).</summary>
public sealed record SpartanChoice(
    string Core,
    IReadOnlyDictionary<string, string>? Armor = null,
    IReadOnlyList<SpartanPartChoice>? Parts = null,
    string? Coating = null,
    string? Visor = null,
    bool DisableDamage = true,
    IReadOnlyDictionary<string, string>? WeaponCoatings = null);
