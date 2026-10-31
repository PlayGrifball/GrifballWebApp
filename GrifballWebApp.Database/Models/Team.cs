#nullable disable

namespace GrifballWebApp.Database.Models;
public class Team : AuditableEntity
{
    /// <summary>The longest a team name can be (its column's length).</summary>
    public const int TeamNameMaxLength = 30;
    /// <summary>The team name column's length: longer than the app lets a name be (<see cref="TeamNameMaxLength"/>), for
    /// the names of teams imported from the historical archive, which are kept as the sheets wrote them.</summary>
    public const int TeamNameColumnLength = 50;

    public Team()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        HomeMatches = new HashSet<SeasonMatch>();
        AwayMatches = new HashSet<SeasonMatch>();
        TeamAvailability = new HashSet<TeamAvailability>();
        PlayerSeasonStats = new HashSet<PlayerSeasonStat>();
        PlayerChampionships = new HashSet<PlayerChampionship>();
    }
    public int SeasonID { get; set; }
    public Season Season { get; set; }
    public int TeamID { get; set; }
    public string TeamName { get; set; }
    public int? CaptainID { get; set; }
    /// <summary>The team's regular-season record as its source spreadsheet gives it, for a season whose matches are not in
    /// the app (imported from the historical archive). Null for a team whose record comes from its matches.</summary>
    public int? RecordedWins { get; set; }
    /// <inheritdoc cref="RecordedWins"/>
    public int? RecordedLosses { get; set; }
    public TeamPlayer Captain { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<SeasonMatch> HomeMatches { get; set; }
    public virtual ICollection<SeasonMatch> AwayMatches { get; set; }
    public virtual ICollection<TeamAvailability> TeamAvailability { get; set; }
    public virtual ICollection<PlayerSeasonStat> PlayerSeasonStats { get; set; }
    public virtual ICollection<PlayerChampionship> PlayerChampionships { get; set; }
    /// <summary>
    /// Copies team and team players, note that captains cannot be copied and must be set after initial save. It is returned as a out parameters to be set by caller after initial save changes
    /// </summary>
    public Team Copy(out TeamPlayer captain)
    {
        var teamPlayers = TeamPlayers.Select(x => x.Copy(this)).ToArray();
        captain = teamPlayers.FirstOrDefault(x => x.CaptainTeam is not null);

        if (captain is not null)
        {
            captain.CaptainTeam = null;
        }

        return new Team()
        {
           TeamName = TeamName,
           TeamPlayers = teamPlayers,
           TeamAvailability = TeamAvailability.Select(x => x.Copy()).ToArray(),
        };
    }
}
