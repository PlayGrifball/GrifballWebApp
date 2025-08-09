namespace GrifballWebApp.Database.Models;

public abstract class AuditableEntity : IAuditable
{
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    // Navigation properties for audit relationships
    public User? CreatedBy { get; set; }
    public User? ModifiedBy { get; set; }
}