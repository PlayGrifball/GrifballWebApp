namespace GrifballWebApp.Database.Models;

public abstract class AuditableEntity : IAuditable
{
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}