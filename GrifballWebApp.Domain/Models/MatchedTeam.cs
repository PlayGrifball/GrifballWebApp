using System.Linq.Expressions;

namespace GrifballWebApp.Domain.Models;
/// <summary>A side of a <see cref="MatchedMatch"/>: its players. Made and changed only by the match, which owns it.</summary>
[AggregatePart(typeof(MatchedMatch))]
public class MatchedTeam : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MatchedTeam() { }

    internal MatchedTeam(IEnumerable<MatchedPlayer> players) => _players.AddRange(players);

    public int MatchedTeamId { get; private set; }
    /// <summary>Read-only: the match picks its players (<see cref="MatchedMatch.Start(IEnumerable{int}, IEnumerable{int}, DateTime?)"/>,
    /// <see cref="MatchedMatch.SetArchivedRoster"/>).</summary>
    public IReadOnlyCollection<MatchedPlayer> Players => _players;
    private readonly List<MatchedPlayer> _players = [];

    // The match it is a side of: private, so a side is never pointed at another match (EF's fixup would repoint the match)
    private MatchedMatch? HomeMatchedMatch { get; set; }
    private MatchedMatch? AwayMatchedMatch { get; set; }
    // Their names, for the configuration, which can't see them
    internal const string HomeMatchedMatchNavigation = nameof(HomeMatchedMatch);
    internal const string AwayMatchedMatchNavigation = nameof(AwayMatchedMatch);

    /// <summary>Its players become these users: added, and removed (deleted, as orphans) for those no longer on it.</summary>
    internal void SetRoster(IEnumerable<int> userIDs)
    {
        var users = userIDs.ToHashSet();
        _players.RemoveAll(p => !users.Contains(p.UserID));
        foreach (var userID in users.Where(u => !_players.Any(p => p.UserID == u)))
            _players.Add(new MatchedPlayer(userID));
    }

    internal void Remove(MatchedPlayer player) => _players.Remove(player);

    /// <summary>See <see cref="MatchedPlayer.InActiveMatch"/>: here, where the links to the match are seen.</summary>
    internal static readonly Expression<Func<MatchedPlayer, bool>> PlayerInActiveMatch = p => p.Kicked == false
        && (p.MatchedTeam.HomeMatchedMatch!.Status == MatchedMatchStatus.Active || p.MatchedTeam.AwayMatchedMatch!.Status == MatchedMatchStatus.Active);

    /// <summary>See <see cref="MatchedPlayer.CountedByVote"/>: here, where the links to the match are seen.</summary>
    internal static readonly Expression<Func<MatchedPlayer, bool>> PlayerCountedByVote = p => p.Kicked == false
        && (p.MatchedTeam.HomeMatchedMatch!.Status == MatchedMatchStatus.VoteDecided || p.MatchedTeam.AwayMatchedMatch!.Status == MatchedMatchStatus.VoteDecided);

    /// <summary>See <see cref="MatchedPlayer.VoteResult"/>: here, where the links to the match are seen. The player's side is
    /// the one whose link is set; the other's is null, so its comparison is false.</summary>
    internal static readonly Expression<Func<MatchedPlayer, MatchedVoteResult>> PlayerVoteResult = p => new MatchedVoteResult
    {
        MatchmakingSeasonID = p.MatchedTeam.HomeMatchedMatch!.MatchmakingSeasonID ?? p.MatchedTeam.AwayMatchedMatch!.MatchmakingSeasonID,
        UserID = p.UserID,
        Won = p.MatchedTeam.HomeMatchedMatch!.WinnerSide == MatchedSide.Home || p.MatchedTeam.AwayMatchedMatch!.WinnerSide == MatchedSide.Away,
    };
}
