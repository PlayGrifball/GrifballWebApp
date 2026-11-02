using System.ComponentModel.DataAnnotations;

namespace GrifballWebApp.Database.Models;

public class PasswordResetLink : AuditableEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    /// <summary>The SHA-256 of the link's token, hex; the token itself is only in the link given out.</summary>
    [Required]
    [StringLength(255)]
    public string Token { get; set; } = null!;
    
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    public bool IsUsed { get; set; } = false;
}