using Microsoft.AspNetCore.Identity;

#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class UserRole : IdentityUserRole<int>
{
    public User User { get; set; }
    public Role Role { get; set; }
}
