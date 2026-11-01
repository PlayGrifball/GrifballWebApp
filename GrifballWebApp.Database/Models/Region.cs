#nullable disable

namespace GrifballWebApp.Database.Models;
public class Region : AuditableEntity
{
    public Region()
    {
        Users = new List<User>();
    }
    public int RegionID { get; set; }
    public string RegionName { get; set; }
    public virtual ICollection<User> Users { get; set; }
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
