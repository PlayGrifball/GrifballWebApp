using System.ComponentModel.DataAnnotations;

namespace GrifballWebApp.Server.Dtos;

public class GeneratePasswordResetLinkRequestDto
{
    [Required]
    public string Username { get; set; } = null!;
}

public class GeneratePasswordResetLinkResponseDto
{
    public string ResetLink { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

public class UsePasswordResetLinkRequestDto
{
    [Required]
    public string Token { get; set; } = null!;
    
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = null!;
}