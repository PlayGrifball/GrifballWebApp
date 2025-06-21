using Microsoft.AspNetCore.Identity;

#nullable disable

namespace GrifballWebApp.Database.Models;
public class UserRole : IdentityUserRole<int>
{
    public User User { get; set; }
    public Role Role { get; set; }
}

public class UserLogin : IdentityUserLogin<int>
{
    public User User { get; set; }
}

public class UserClaim : IdentityUserClaim<int>
{
    public User User { get; set; }
}
