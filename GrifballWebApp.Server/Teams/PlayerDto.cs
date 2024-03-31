namespace GrifballWebApp.Server.Teams;

public class PlayerDto
{
    public int PersonID { get; set; }
    public string Name { get; set; }
    public int? Round { get; set; }
    public int? Pick { get; set; }
}
