

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
    /// <summary>Where the match is in its life; changed through <see cref="Report"/>, <see cref="DecideByVote"/> and
    /// <see cref="Cancel"/>, which only end an active match.</summary>
    public MatchedMatchStatus Status { get; private set; } = MatchedMatchStatus.Active;
    /// <summary>The side the players voted the winner of a <see cref="MatchedMatchStatus.VoteDecided"/> match; null otherwise
    /// (a reported match's winner is its Halo match's).</summary>
    public MatchedSide? WinnerSide { get; private set; }
    public Match? Match { get; set; }
    /// <summary>The Halo Infinite match it was played as, once reported (<see cref="Report"/>).</summary>
    public Guid? MatchID { get; private set; }
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

    /// <summary>Ends the match with the Halo Infinite match it was played as.</summary>
    public void Report(Guid matchID)
    {
        End(MatchedMatchStatus.Reported);
        MatchID = matchID;
    }

    /// <summary>Ends the match with the winner its players voted for, no match on record.</summary>
    public void DecideByVote(MatchedSide winner)
    {
        End(MatchedMatchStatus.VoteDecided);
        WinnerSide = winner;
    }

    /// <summary>An archive game (the historical import's): its result is its recorded game. Again for one imported before.</summary>
    public void RecordArchived()
    {
        if (Status is not MatchedMatchStatus.Reported)
            End(MatchedMatchStatus.Reported);
    }

    /// <summary>Calls the match off: nobody won.</summary>
    public void Cancel() => End(MatchedMatchStatus.Canceled);

    private void End(MatchedMatchStatus status)
    {
        if (Status is not MatchedMatchStatus.Active)
            throw new InvalidOperationException($"Match {Id} has already ended ({Status})");
        Status = status;
    }
}
