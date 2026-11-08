#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A region a player plays from: reference data seeded with fixed IDs (RegionConfiguration's HasData, through
/// <see cref="Seed"/>), which the app only reads. Nothing outside it sets its fields, and EF uses the private setters.
/// Its name is never blank (CK_Other_Regions_NameNotBlank).
/// </summary>
[AggregateRoot]
public class Region : AuditableEntity
{
    /// <summary>For EF. A region is seeded (<see cref="Seed"/>).</summary>
    private Region() { }

    /// <summary>A region of the seed data (HasData), or of a test.</summary>
    internal static Region Seed(string regionName, int regionID = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionName);
        return new() { RegionID = regionID, RegionName = regionName };
    }

    // The key is internal: the seed data gives it, and tests may give one
    public int RegionID { get; internal set; }
    public string RegionName { get; private set; }

    // Read-only: a user names their region (User.ChooseRegion; EF uses the field)
    public IReadOnlyCollection<User> Users => _users;
    private readonly List<User> _users = [];
}

/// <summary>Who set a user's region.</summary>
public enum RegionSource
{
    /// <summary>The user, on their profile or at sign-up.</summary>
    User = 1,
    /// <summary>A sysadmin, in user management.</summary>
    Sysadmin = 2,
    /// <summary>The historical import, from the archive's player lists; only ever set on a user with no region.</summary>
    Import = 3,
}
