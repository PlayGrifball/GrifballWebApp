using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;
/// <summary>
/// A matchmaking (Late League) game: two sides of random, skill-matched players, not teams of a season. The queue's matches
/// are played in Halo Infinite (<see cref="MatchID"/>, once reported); the archive's are recorded games
/// (<see cref="RecordedGameID"/>), finished, in their <see cref="MatchmakingSeason"/>. The root of its sides, players and
/// votes: they are made and changed only through it, and it is loaded whole, through <see cref="MatchedMatchQueries"/>.
/// </summary>
[AggregateRoot]
public class MatchedMatch : AuditableEntity
{
    /// <summary>For EF. A match is made by <see cref="Start(IEnumerable{int}, IEnumerable{int}, DateTime?)"/> or
    /// <see cref="Archive"/>, the two ways one begins.</summary>
    private MatchedMatch() { }

    /// <summary>A match the queue made of its players, these users on each side: active, from <paramref name="startedAt"/>
    /// (now unless given).</summary>
    public static MatchedMatch Start(IEnumerable<int> homeUserIDs, IEnumerable<int> awayUserIDs, DateTime? startedAt = null)
        => Start(new MatchedTeam(homeUserIDs.Select(u => new MatchedPlayer(u))), new MatchedTeam(awayUserIDs.Select(u => new MatchedPlayer(u))), startedAt);

    /// <summary>The same, of sides already made: for tests, whose players carry keys and users of their own.</summary>
    internal static MatchedMatch Start(MatchedTeam home, MatchedTeam away, DateTime? startedAt = null) => new()
    {
        HomeTeam = home,
        AwayTeam = away,
        StartedAt = startedAt ?? DateTime.UtcNow,
    };

    /// <summary>An archive game of the historical import, already played: reported, its result <paramref name="game"/>, in
    /// its matchmaking season. Its players come after (<see cref="SetArchivedRoster"/>).</summary>
    public static MatchedMatch Archive(RecordedGame game, int matchmakingSeasonID, DateTime startedAt) => new()
    {
        HomeTeam = new MatchedTeam([]),
        AwayTeam = new MatchedTeam([]),
        Status = MatchedMatchStatus.Reported,
        RecordedGame = game,
        MatchmakingSeasonID = matchmakingSeasonID,
        StartedAt = startedAt,
    };

    // Nothing outside the match sets its state, its foreign keys or its navigations: only its own methods below, and EF
    // (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int Id { get; internal set; }
    public int HomeTeamId { get; private set; }
    public int AwayTeamId { get; private set; }
    public MatchedTeam HomeTeam { get; private set; } = null!;
    public MatchedTeam AwayTeam { get; private set; } = null!;
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
    // Read-only: only the match's own methods add or change a vote (EF uses the fields)
    public IReadOnlyCollection<MatchedWinnerVote> MatchedWinnerVotes => _matchedWinnerVotes;
    private readonly HashSet<MatchedWinnerVote> _matchedWinnerVotes = [];
    public IReadOnlyCollection<MatchedKickVote> MatchedKickVotes => _matchedKickVotes;
    private readonly HashSet<MatchedKickVote> _matchedKickVotes = [];

    /// <summary>A match with this user among its players, kicked or not (for queries).</summary>
    public static Expression<Func<MatchedMatch, bool>> HasPlayer(int userID)
        => m => m.HomeTeam.Players.Any(p => p.UserID == userID) || m.AwayTeam.Players.Any(p => p.UserID == userID);

    /// <summary>A match played out to a result: reported, or decided by its players' vote (not canceled, not still being
    /// played). For queries.</summary>
    public static readonly Expression<Func<MatchedMatch, bool>> PlayedToAResult
        = m => m.Status == MatchedMatchStatus.Reported || m.Status == MatchedMatchStatus.VoteDecided;

    /// <summary>A match ended by its players' vote for a winner (<see cref="DecideByVote"/>): played to a result, with no
    /// Halo match on record. For queries.</summary>
    public static readonly Expression<Func<MatchedMatch, bool>> DecidedByVote = m => m.Status == MatchedMatchStatus.VoteDecided;

    /// <summary>A queue match reported with the Halo Infinite match it was: never an archive game, which has a recorded game
    /// instead (CK_Matchmaking_MatchedMatches_OneKindOfResult). For queries.</summary>
    public static readonly Expression<Func<MatchedMatch, bool>> ReportedFromHalo = m => m.MatchID != null;

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

    /// <summary>The players a vote-decided match counts a win for: the voted winner's side, those still in it (a kicked player
    /// didn't play it out, so neither wins nor loses it). None for a match not decided by vote. Needs its players loaded;
    /// <see cref="MatchedPlayer.CountedByVote"/> is the same rule for queries.</summary>
    public IEnumerable<MatchedPlayer> VoteWinners() => Status is MatchedMatchStatus.VoteDecided ? PlayersStillIn(WinnerSide is MatchedSide.Home ? MatchedSide.Home : MatchedSide.Away) : [];

    /// <summary>The players a vote-decided match counts a loss for: the other side's, those still in it. See <see cref="VoteWinners"/>.</summary>
    public IEnumerable<MatchedPlayer> VoteLosers() => Status is MatchedMatchStatus.VoteDecided ? PlayersStillIn(WinnerSide is MatchedSide.Home ? MatchedSide.Away : MatchedSide.Home) : [];

    /// <summary>A side's players still in the match: not kicked. A kicked player didn't play it out, so neither wins nor loses
    /// it, and their MMR is not changed. Needs its players loaded; <see cref="MatchedPlayer.InActiveMatch"/> and
    /// <see cref="MatchedPlayer.CountedByVote"/> are the same rule for queries.</summary>
    public IEnumerable<MatchedPlayer> PlayersStillIn(MatchedSide side) => (side is MatchedSide.Home ? HomeTeam : AwayTeam).Players.Where(p => !p.Kicked);

    /// <summary>Its players still in it (not kicked), home then away. See <see cref="PlayersStillIn(MatchedSide)"/>.</summary>
    public IEnumerable<MatchedPlayer> PlayersStillIn() => PlayersStillIn(MatchedSide.Home).Concat(PlayersStillIn(MatchedSide.Away));

    /// <summary>
    /// An archive game imported again: its season and the time it was played at, as the import has them now. Only for a
    /// match that is already this archive game: a queue match never becomes one.
    /// </summary>
    public void RecordArchived(RecordedGame game, int matchmakingSeasonID, DateTime startedAt)
    {
        EnsureArchiveGame();
        if (RecordedGame is { } recorded ? recorded != game : RecordedGameID != game.RecordedGameID)
            throw new InvalidOperationException($"Match {Id} is already recorded game {RecordedGameID}");
        MatchmakingSeasonID = matchmakingSeasonID;
        StartedAt = startedAt;
    }

    /// <summary>An archive game's players, these users on each side as the archive has them: added, and removed for those no
    /// longer on it. Only an archive game's: a queue match's players are those it started with. Needs its players loaded.</summary>
    public void SetArchivedRoster(IEnumerable<int> homeUserIDs, IEnumerable<int> awayUserIDs)
    {
        EnsureArchiveGame();
        HomeTeam.SetRoster(homeUserIDs);
        AwayTeam.SetRoster(awayUserIDs);
    }

    /// <summary>
    /// Readies an archive game's match to be deleted with its sides (<see cref="GrifballContext.RemoveArchivedMatch"/>): it
    /// lets go of its recorded game, which may stay (the archive moved it to a season). Never a queue match.
    /// </summary>
    internal void Discard()
    {
        EnsureArchiveGame();
        RecordedGame = null;
        RecordedGameID = null;
    }

    /// <summary>An archive game is reported with its recorded game, and never a Halo match.</summary>
    private void EnsureArchiveGame()
    {
        if (Status is not MatchedMatchStatus.Reported || MatchID is not null || (RecordedGame is null && RecordedGameID is null))
            throw new InvalidOperationException($"Match {Id} is not an archive game ({Status})");
    }

    /// <summary>
    /// A user merge: the source user's player in the match becomes the target user's. When the target is in it too (one
    /// person twice), the match must be over: then the source's player goes, with its votes and the kick votes against it
    /// (a match that ended counts them no more), and the target's stays. While it is being played that is refused: it
    /// would change who votes and how many votes win. Load it through <see cref="MatchedMatchQueries"/>.
    /// </summary>
    public PlayerReassignment ReassignPlayer(int fromUserID, int toUserID)
    {
        var from = PlayerOf(fromUserID);
        if (from is null)
            return PlayerReassignment.NotAPlayer;
        if (PlayerOf(toUserID) is null)
        {
            from.UserID = toUserID;
            return PlayerReassignment.Moved;
        }
        if (Status is MatchedMatchStatus.Active)
            return PlayerReassignment.BothInActiveMatch;

        _matchedWinnerVotes.RemoveWhere(v => Is(v.MatchedPlayer, v.MatchedPlayerId, from));
        _matchedKickVotes.RemoveWhere(v => Is(v.VoterMatchedPlayer, v.VoterMatchedPlayerId, from) || Is(v.KickMatchedPlayer, v.KickMatchedPlayerId, from));
        HomeTeam.Remove(from);
        AwayTeam.Remove(from);
        return PlayerReassignment.Dropped;
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
    /// it (not kicked), while it is being played. Load it through <see cref="MatchedMatchQueries"/>.
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

        var earlier = _matchedWinnerVotes.FirstOrDefault(v => Is(v.MatchedPlayer, v.MatchedPlayerId, voter));
        if (earlier is null)
            _matchedWinnerVotes.Add(new MatchedWinnerVote { MatchedMatch = this, MatchedPlayer = voter, WinnerVote = vote });
        else
            earlier.WinnerVote = vote;
        return WinnerVoteResult.Voted;
    }

    /// <summary>The votes for the winner that count: those of its players still in it (not kicked). Load it through
    /// <see cref="MatchedMatchQueries"/>.</summary>
    public IEnumerable<MatchedWinnerVote> CountedWinnerVotes => _matchedWinnerVotes.Where(v => VoterOf(v.MatchedPlayer, v.MatchedPlayerId) is { Kicked: false });

    /// <summary>How many agreeing votes decide it: more than half of its players still in it.</summary>
    public int VotesNeeded => PlayersStillIn().Count() / 2 + 1;

    /// <summary>What the players still in the match voted for, once <see cref="VotesNeeded"/> agree; null until then.</summary>
    public WinnerVote? VotedWinner() => CountedWinnerVotes
        .GroupBy(v => v.WinnerVote)
        .Where(g => g.Count() >= VotesNeeded)
        .Select(g => (WinnerVote?)g.Key)
        .FirstOrDefault();

    /// <summary>
    /// A player's vote to kick another, both still in the match, while it is being played; once for each player they want
    /// out. Past half of the players still in it voting so, the player is kicked: out of the match, and free to queue
    /// again. Load it through <see cref="MatchedMatchQueries"/>.
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
        if (_matchedKickVotes.Any(v => Is(v.VoterMatchedPlayer, v.VoterMatchedPlayerId, voter) && Is(v.KickMatchedPlayer, v.KickMatchedPlayerId, target)))
            return Result(KickVoteResult.AlreadyVoted);

        _matchedKickVotes.Add(new MatchedKickVote { MatchedMatch = this, VoterMatchedPlayer = voter, KickMatchedPlayer = target });
        return Result(DecideKick(targetUserID) is KickDecision.Kicked ? KickVoteResult.Kicked : KickVoteResult.Voted);
    }

    /// <summary>
    /// Whether the kick votes against a player make a majority now: past half of the players still in the match, they are
    /// kicked. <see cref="VoteToKick"/> decides it with the vote it adds. Others may vote at the same moment, each counting
    /// from the votes it loaded, so a vote that fell short is decided again once it is saved, on the votes saved by then:
    /// the last to save sees them all. Load it through <see cref="MatchedMatchQueries"/>.
    /// </summary>
    public KickDecision DecideKick(int targetUserID)
    {
        if (Status is not MatchedMatchStatus.Active)
            return KickDecision.MatchOver;
        var target = PlayerOf(targetUserID);
        if (target is null)
            return KickDecision.NotAPlayer;
        if (target.Kicked)
            return KickDecision.AlreadyKicked;

        var majority = PlayersStillIn().Count() / 2;
        var against = _matchedKickVotes.Count(v => Is(v.KickMatchedPlayer, v.KickMatchedPlayerId, target)
            && VoterOf(v.VoterMatchedPlayer, v.VoterMatchedPlayerId) is { Kicked: false });
        if (against <= majority)
            return KickDecision.ShortOfMajority;
        target.Kicked = true;
        return KickDecision.Kicked;
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
