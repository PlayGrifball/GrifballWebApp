#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Password
{
    public int PersonID { get; set; }
    public string Salt { get; set; }
    public string Hash { get; set; }
    public Person Person { get; set; }
}
