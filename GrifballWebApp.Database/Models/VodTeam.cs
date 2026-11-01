#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A team a VOD shows: what puts it on the team's page (and its players' profiles).</summary>
public class VodTeam : AuditableEntity
{
    public int VodID { get; set; }
    public Vod Vod { get; set; }
    public int TeamID { get; set; }
    public Team Team { get; set; }
}
