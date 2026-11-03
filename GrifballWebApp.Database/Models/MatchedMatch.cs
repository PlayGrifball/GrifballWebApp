

namespace GrifballWebApp.Database.Models;
/// <summary>
/// A matchmaking (Late League) game: two sides of random, skill-matched players, not teams of a season. The queue's matches
/// are played in Halo Infinite (<see cref="MatchID"/>, once reported); the archive's are recorded games
/// (<see cref="RecordedGameID"/>), finished, in their <see cref="MatchmakingSeason"/>.
/// </summary>
public class MatchedMatch : AuditableEntity
{
    public MatchedMatch()
    {
        MatchedWinnerVotes = new HashSet<MatchedWinnerVote>();
        MatchedKickVotes = new HashSet<MatchedKickVote>();
    }
    public int Id { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public MatchedTeam HomeTeam { get; set; } = null!;
    public MatchedTeam AwayTeam { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
    public Match? Match { get; set; }
    public Guid? MatchID { get; set; }
    /// <summary>The recorded game it is (an archive Late League game); unique where set.</summary>
    public int? RecordedGameID { get; set; }
    public RecordedGame? RecordedGame { get; set; }
    /// <summary>The matchmaking season it counts for; null for the queue's matches.</summary>
    public int? MatchmakingSeasonID { get; set; }
    public MatchmakingSeason? MatchmakingSeason { get; set; }
    public ulong? ThreadID { get; set; }
    public ulong? VoteMessageID { get; set; }
    public ICollection<MatchedWinnerVote> MatchedWinnerVotes { get; set; }
    public ICollection<MatchedKickVote> MatchedKickVotes { get; set; }
}
