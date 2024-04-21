using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Database.Models;
public partial class Role : IdentityRole<int>
{
    public Role()
    {
        UserRoles = new HashSet<UserRole>();
    }

    public virtual ICollection<UserRole> UserRoles { get; set; }
}
