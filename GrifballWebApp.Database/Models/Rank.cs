namespace GrifballWebApp.Database.Models;
public class Rank
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int MmrThreshold { get; set; }
    public string Color { get; set; } = null!;
    public string Description { get; set; } = null!;
    public byte[] Icon { get; set; } = null!;
}
