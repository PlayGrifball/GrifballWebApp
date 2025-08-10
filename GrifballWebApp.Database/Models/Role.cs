using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Database.Models;
public class Role : IdentityRole<int>, IAuditable
{
    public Role()
    {
        UserRoles = new HashSet<UserRole>();
    }

    public virtual ICollection<UserRole> UserRoles { get; set; }
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
