

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

    /// <summary>A match that started at <paramref name="startedAt"/> (a new queue match starts now).</summary>
    public MatchedMatch(DateTime startedAt) : this()
    {
        StartedAt = startedAt;
    }

    // Nothing outside the match sets its state, its foreign keys or its navigations: only its own methods below, and EF
    // (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int Id { get; internal set; }
    public int HomeTeamId { get; private set; }
    public int AwayTeamId { get; private set; }
    public MatchedTeam HomeTeam { get; init; } = null!;
    public MatchedTeam AwayTeam { get; init; } = null!;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    /// <summary>Where the match is in its life; changed through <see cref="Report"/>, <see cref="DecideByVote"/> and
    /// <see cref="Cancel"/>, which only end an active match.</summary>
    public MatchedMatchStatus Status { get; private set; } = MatchedMatchStatus.Active;
    /// <summary>The side the players voted the winner of a <see cref="MatchedMatchStatus.VoteDecided"/> match; null otherwise
    /// (a reported match's winner is its Halo match's).</summary>
    public MatchedSide? WinnerSide { get; private set; }
    public Match? Match { get; private set; }
    /// <summary>The Halo Infinite match it was played as, once reported (<see cref="Report"/>).</summary>
    public Guid? MatchID { get; private set; }
    /// <summary>The recorded game it is (an archive Late League game); unique where set.</summary>
    public int? RecordedGameID { get; private set; }
    public RecordedGame? RecordedGame { get; private set; }
    /// <summary>The matchmaking season it counts for; null for the queue's matches.</summary>
    public int? MatchmakingSeasonID { get; private set; }
    public MatchmakingSeason? MatchmakingSeason { get; private set; }
    /// <summary>The Discord thread its players talk and vote in, once made (<see cref="AttachThread"/>).</summary>
    public ulong? ThreadID { get; private set; }
    /// <summary>The vote message in its thread, once posted (<see cref="PostVoteMessage"/>): only then can its players vote.</summary>
    public ulong? VoteMessageID { get; private set; }
    public ICollection<MatchedWinnerVote> MatchedWinnerVotes { get; private set; }
    public ICollection<MatchedKickVote> MatchedKickVotes { get; private set; }

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

    /// <summary>
    /// An archive game (the historical import's) of a matchmaking season, played at <paramref name="startedAt"/>: its result
    /// is <paramref name="game"/>. Again for one imported before, which keeps its game and takes the season and time given.
    /// </summary>
    public void RecordArchived(RecordedGame game, int matchmakingSeasonID, DateTime startedAt)
    {
        if (RecordedGame is { } recorded && recorded != game)
            throw new InvalidOperationException($"Match {Id} is already recorded game {recorded.RecordedGameID}");
        if (Status is not MatchedMatchStatus.Reported)
            End(MatchedMatchStatus.Reported);
        RecordedGame = game;
        MatchmakingSeasonID = matchmakingSeasonID;
        StartedAt = startedAt;
    }

    /// <summary>Lets go of its recorded game, before the match is deleted while the game stays (the archive moved it to a
    /// season).</summary>
    public void ReleaseRecordedGame()
    {
        RecordedGame = null;
        RecordedGameID = null;
    }

    /// <summary>The Discord thread made for the match. Once: a retry finding the same thread is fine, another is not.</summary>
    public void AttachThread(ulong threadID)
    {
        if (ThreadID is { } attached && attached != threadID)
            throw new InvalidOperationException($"Match {Id} already has thread {attached}");
        ThreadID = threadID;
    }

    /// <summary>The vote message posted in the match's thread, which needs the thread first. Once.</summary>
    public void PostVoteMessage(ulong voteMessageID)
    {
        if (ThreadID is null)
            throw new InvalidOperationException($"Match {Id} has no thread to post its vote in");
        if (VoteMessageID is { } posted && posted != voteMessageID)
            throw new InvalidOperationException($"Match {Id} already has vote message {posted}");
        VoteMessageID = voteMessageID;
    }

    /// <summary>Its players, home then away (the teams and their players loaded).</summary>
    public IEnumerable<MatchedPlayer> Players => HomeTeam.Players.Concat(AwayTeam.Players);

    /// <summary>
    /// A player's vote for the winner (or to cancel), changing their earlier one. Only a player of the match who is still in
    /// it (not kicked), while it is being played. Needs its players and winner votes loaded.
    /// </summary>
    public WinnerVoteResult VoteForWinner(int userID, WinnerVote vote)
    {
        if (Status is not MatchedMatchStatus.Active)
            return WinnerVoteResult.MatchOver;
        var voter = PlayerOf(userID);
        if (voter is null)
            return WinnerVoteResult.NotAPlayer;
        if (voter.Kicked)
            return WinnerVoteResult.VoterKicked;

        var earlier = MatchedWinnerVotes.FirstOrDefault(v => Is(v.MatchedPlayer, v.MatchedPlayerId, voter));
        if (earlier is null)
            MatchedWinnerVotes.Add(new MatchedWinnerVote { MatchedMatch = this, MatchedPlayer = voter, WinnerVote = vote });
        else
            earlier.WinnerVote = vote;
        return WinnerVoteResult.Voted;
    }

    /// <summary>
    /// What the players still in the match voted for, when more than half of them agree; null until then. Needs its players
    /// and winner votes loaded.
    /// </summary>
    public WinnerVote? VotedWinner()
    {
        var majority = Players.Count(p => !p.Kicked) / 2;
        return MatchedWinnerVotes
            .Where(v => VoterOf(v.MatchedPlayer, v.MatchedPlayerId) is { Kicked: false })
            .GroupBy(v => v.WinnerVote)
            .Where(g => g.Count() > majority)
            .Select(g => (WinnerVote?)g.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// A player's vote to kick another, both still in the match, while it is being played; once each. Past half of the
    /// players still in it voting so, the player is kicked: out of the match, and free to queue again. Needs its players and
    /// kick votes loaded.
    /// </summary>
    public KickVote VoteToKick(int voterUserID, int targetUserID)
    {
        var voter = PlayerOf(voterUserID);
        var target = PlayerOf(targetUserID);
        KickVote Result(KickVoteResult result) => new(result, voter, target);

        if (Status is not MatchedMatchStatus.Active)
            return Result(KickVoteResult.MatchOver);
        if (voter is null)
            return Result(KickVoteResult.NotAPlayer);
        if (voter.Kicked)
            return Result(KickVoteResult.VoterKicked);
        if (target is null)
            return Result(KickVoteResult.TargetNotAPlayer);
        if (target.Kicked)
            return Result(KickVoteResult.TargetAlreadyKicked);
        if (MatchedKickVotes.Any(v => Is(v.VoterMatchedPlayer, v.VoterMatchedPlayerId, voter) && Is(v.KickMatchedPlayer, v.KickMatchedPlayerId, target)))
            return Result(KickVoteResult.AlreadyVoted);

        MatchedKickVotes.Add(new MatchedKickVote { MatchedMatch = this, VoterMatchedPlayer = voter, KickMatchedPlayer = target });

        var majority = Players.Count(p => !p.Kicked) / 2;
        var against = MatchedKickVotes.Count(v => Is(v.KickMatchedPlayer, v.KickMatchedPlayerId, target)
            && VoterOf(v.VoterMatchedPlayer, v.VoterMatchedPlayerId) is { Kicked: false });
        if (against <= majority)
            return Result(KickVoteResult.Voted);
        target.Kicked = true;
        return Result(KickVoteResult.Kicked);
    }

    private MatchedPlayer? PlayerOf(int userID) => Players.FirstOrDefault(p => p.UserID == userID);

    /// <summary>A vote's player: its navigation when loaded, else the match's player of that ID.</summary>
    private MatchedPlayer? VoterOf(MatchedPlayer? player, int playerID) => player ?? Players.FirstOrDefault(p => p.Id == playerID);

    /// <summary>A vote's player is this one: by reference, or by ID once saved.</summary>
    private static bool Is(MatchedPlayer? player, int playerID, MatchedPlayer other) => player == other || (playerID != 0 && playerID == other.Id);

    /// <summary>Calls the match off: nobody won.</summary>
    public void Cancel() => End(MatchedMatchStatus.Canceled);

    private void End(MatchedMatchStatus status)
    {
        if (Status is not MatchedMatchStatus.Active)
            throw new InvalidOperationException($"Match {Id} has already ended ({Status})");
        Status = status;
    }
}
