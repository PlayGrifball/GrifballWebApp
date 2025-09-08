using System.ComponentModel.DataAnnotations;

namespace GrifballWebApp.Database.Models;

public class PasswordResetLink : IAuditable
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    [Required]
    [StringLength(255)]
    public string Token { get; set; } = null!;
    
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    public bool IsUsed { get; set; } = false;
    
    // IAuditable properties
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}