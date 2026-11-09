using Microsoft.AspNetCore.Identity;

#nullable disable

namespace GrifballWebApp.Database.Models;
public class UserRole : IdentityUserRole<int>, IAuditStamp
{
    public User User { get; set; }
    public Role Role { get; set; }
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class UserLogin : IdentityUserLogin<int>, IAuditStamp
{
    public User User { get; set; }
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class UserClaim : IdentityUserClaim<int>, IAuditStamp
{
    public User User { get; set; }
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
