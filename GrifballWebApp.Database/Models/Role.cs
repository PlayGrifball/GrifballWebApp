#nullable disable

namespace GrifballWebApp.Database.Models;

[Flags]
public enum RoleNames
{
    Sysadmin = 1,
    Player = 2,
    EventOrganizer = 4,
}

public partial class Role
{
    public Role()
    {
        PersonRoles = new HashSet<PersonRole>();
    }
    public int RoleID { get; set; }
    public RoleNames Name { get; set; }
    public virtual ICollection<PersonRole> PersonRoles { get; set; }
}
