#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Region
{
    public Region()
    {
        Users = new List<User>();
    }
    public int RegionID { get; set; }
    public string RegionName { get; set; }
    public virtual ICollection<User> Users { get; set; }
}
