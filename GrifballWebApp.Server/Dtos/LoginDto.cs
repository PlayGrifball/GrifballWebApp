namespace GrifballWebApp.Server.Dtos;

#nullable disable
public record LoginDto
{
    public string Username { get; set; }
    public string Password { get; set; }
}
