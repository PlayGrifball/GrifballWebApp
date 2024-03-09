#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class PersonRole
{
    public int PersonID { get; set; }
    public int RoleID { get; set; }
    public Person Person { get; set; }
    public Role Role { get; set; }
}
