namespace GrifballWebApp.Server.Dtos;

#nullable disable
public record RegisterDto
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string Gamertag { get; set; }
}
