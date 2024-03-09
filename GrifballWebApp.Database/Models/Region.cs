#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Region
{
    public Region()
    {
        Persons = new List<Person>();
    }
    public int RegionID { get; set; }
    public string RegionName { get; set; }
    public virtual ICollection<Person> Persons { get; set; }
}
