namespace GrifballWebApp.Server.UserManagement;

public record CreateUserRequestDto
{
    public string DisplayName { get; set; }
    public string Gamertag { get; set; }
    public string UserName { get; set; }
}
