#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class MedalType
{
    public MedalType()
    {
        Medals = new HashSet<Medal>();
    }

    public int MedalTypeID { get; set; }
    public string MedalTypeName { get; set; }

    public virtual ICollection<Medal> Medals { get; set; }
}
