namespace GrifballWebApp.Application.UserManagement;

public record CreateUserRequestDto
{
    public required string DisplayName { get; set; }
    public required string Gamertag { get; set; }
    public required string UserName { get; set; }
}
