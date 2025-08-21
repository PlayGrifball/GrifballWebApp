namespace GrifballWebApp.Server.Dtos;

public record CustomSeedDto
{
    public required int TeamID { get; set; }
    public required int TeamID { get; init; }
    public required int Seed { get; init; }
}