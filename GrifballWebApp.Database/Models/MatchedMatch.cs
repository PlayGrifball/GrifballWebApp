

namespace GrifballWebApp.Database.Models;
public class MatchedMatch
{
    public int Id { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public MatchedTeam HomeTeam { get; set; }
    public MatchedTeam AwayTeam { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
