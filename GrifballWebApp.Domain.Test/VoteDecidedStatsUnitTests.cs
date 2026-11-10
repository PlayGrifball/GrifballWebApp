using GrifballWebApp.Domain.LateLeague;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Domain.Test;

/// <summary>VoteDecidedStatsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class VoteDecidedStatsUnitTests
{
    private static readonly int[] ExpectedVoteWinners = [3];
    private static readonly int[] ExpectedVoteLosers = [1, 2];
    private static readonly int[] ExpectedHomeStillIn = [1, 2];
    private static readonly int[] ExpectedAwayStillIn = [3];
    private static readonly int[] ExpectedAllStillIn = [1, 2, 3];

    [Test]
    public void A_vote_decided_match_counts_the_winners_and_losers_still_in_it()
    {
        var match = MatchedMatch.Start([1, 2], [3, 4]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.VoteWinners(), Is.Empty, "Not decided yet");
            Assert.That(match.VoteLosers(), Is.Empty);
        }
        match.VoteToKick(1, 4);
        match.VoteToKick(2, 4);
        match.VoteToKick(3, 4);
        match.DecideByVote(MatchedSide.Away);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.VoteWinners().Select(p => p.UserID), Is.EqualTo(ExpectedVoteWinners), "4 was kicked: no win for them");
            Assert.That(match.VoteLosers().Select(p => p.UserID), Is.EqualTo(ExpectedVoteLosers));
            var home = MatchedMatch.Start([1], [2]);
            home.DecideByVote(MatchedSide.Home);
            Assert.That((home.VoteWinners().Single().UserID, home.VoteLosers().Single().UserID), Is.EqualTo((1, 2)), "Home the winner");
            Assert.That(MatchedMatch.Start([1], [2]).Canceled().VoteWinners(), Is.Empty, "A canceled match has no winner");
            Assert.That(MatchedMatch.Start([1], [2]).Reported(Guid.NewGuid()).VoteLosers(), Is.Empty, "A reported one's result is its Halo match's");
        }
    }

    [Test]
    public void A_match_s_players_still_in_it_leave_out_the_kicked_one_on_each_side()
    {
        var match = MatchedMatch.Start([1, 2], [3, 4]);
        match.VoteToKick(1, 4);
        match.VoteToKick(2, 4);
        match.VoteToKick(3, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.PlayersStillIn(MatchedSide.Home).Select(p => p.UserID), Is.EqualTo(ExpectedHomeStillIn));
            Assert.That(match.PlayersStillIn(MatchedSide.Away).Select(p => p.UserID), Is.EqualTo(ExpectedAwayStillIn), "4 was kicked");
            Assert.That(match.PlayersStillIn().Select(p => p.UserID), Is.EqualTo(ExpectedAllStillIn), "Home then away");
            Assert.That(match.VotesNeeded, Is.EqualTo(2), "More than half of the 3 still in it");
        }
    }
}
