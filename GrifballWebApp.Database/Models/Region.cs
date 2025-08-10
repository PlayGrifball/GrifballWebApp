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
