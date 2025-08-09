namespace GrifballWebApp.Database.Models;

public interface IAuditable
{
    int? CreatedByID { get; set; }
    int? ModifiedByID { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime ModifiedAt { get; set; }
}