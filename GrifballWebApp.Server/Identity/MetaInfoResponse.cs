namespace GrifballWebApp.Server.Identity;

public record MetaInfoResponse
{
    public required bool IsSysAdmin { get; set; }
    public required bool IsPlayer { get; set; }
    public required bool IsCommissioner { get; set; }
    public required string DisplayName { get; set; }
    public required int UserID { get; set; }
}
