using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using static GrifballWebApp.Domain.Test.MatchedMatchStatusTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>MatchedMatchStatusTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class MatchedMatchStatusUnitTests
{
    private static readonly int[] ExpectedPlayersBefore = [1, 2, 3, 4];
    private static readonly int[] ExpectedPlayersAfterTheMove = [1, 2, 3, 5];
    private static readonly int[] ExpectedPlayersAfterTheDrop = [1, 2, 5];
    private static readonly int[] ExpectedMatchedWinnerVotes = [1];

    [Test]
    public void An_archive_game_is_reported_and_only_an_archive_game_is_imported_again()
    {
        var game = RecordedGame.Seed("archive/1");
        var match = MatchedMatch.Archive(game, 7, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        match.RecordArchived(game, 8, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.Status, match.RecordedGame, match.MatchmakingSeasonID, match.StartedAt),
                Is.EqualTo((MatchedMatchStatus.Reported, game, (int?)8, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc))));
            Assert.That(() => match.RecordArchived(RecordedGame.Seed("archive/2"), 7, DateTime.UtcNow), Throws.InvalidOperationException,
                "One recorded game a match");
            Assert.That(() => NewMatch().RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException, "An active queue match never becomes one");
            Assert.That(() => NewMatch().Reported(Guid.NewGuid()).RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException,
                "Nor one reported with a Halo match");
            Assert.That(() => NewMatch().Canceled().RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException, "Nor a canceled one");
        }
    }

    [Test]
    public void A_match_gets_one_thread_then_one_vote_message_in_it()
    {
        var match = NewMatch();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => match.PostVoteMessage(1), Throws.InvalidOperationException, "No thread to post it in");
            match.AttachThread(10);
            Assert.That(() => match.AttachThread(10), Throws.Nothing, "A retry finding the same thread");
            Assert.That(() => match.AttachThread(11), Throws.InvalidOperationException);
            match.PostVoteMessage(20);
            Assert.That(() => match.PostVoteMessage(21), Throws.InvalidOperationException);
            Assert.That((match.ThreadID, match.VoteMessageID), Is.EqualTo(((ulong?)10, (ulong?)20)));
        }
    }

    [Test]
    public void Only_its_players_still_in_it_vote_for_a_winner_once_each_while_it_is_played()
    {
        var match = FourPlayers();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.VoteForWinner(9, WinnerVote.Home), Is.EqualTo(WinnerVoteResult.NotAPlayer));
            Assert.That(match.VoteForWinner(1, WinnerVote.Away), Is.EqualTo(WinnerVoteResult.Voted));
            Assert.That(match.VoteForWinner(1, WinnerVote.Home), Is.EqualTo(WinnerVoteResult.Voted), "A change of mind");
            Assert.That(match.MatchedWinnerVotes.Select(v => (v.MatchedPlayer.UserID, v.WinnerVote)), Is.EqualTo(new[] { (1, WinnerVote.Home) }));
            Assert.That(match.VotedWinner(), Is.Null, "1 of 4 is no majority");
            match.VoteForWinner(2, WinnerVote.Home);
            Assert.That(match.VotedWinner(), Is.Null, "2 of 4 is not more than half");
            match.VoteForWinner(3, WinnerVote.Home);
            Assert.That(match.VotedWinner(), Is.EqualTo(WinnerVote.Home));
            match.Cancel();
            Assert.That(match.VoteForWinner(4, WinnerVote.Away), Is.EqualTo(WinnerVoteResult.MatchOver));
        }
    }

    [Test]
    public void A_majority_of_the_players_still_in_it_kicks_a_player_who_then_neither_votes_nor_counts()
    {
        var match = FourPlayers();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.VoteToKick(9, 4).Result, Is.EqualTo(KickVoteResult.NotAPlayer));
            Assert.That(match.VoteToKick(1, 9).Result, Is.EqualTo(KickVoteResult.TargetNotAPlayer));
            Assert.That(match.VoteToKick(1, 4).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteToKick(1, 4).Result, Is.EqualTo(KickVoteResult.AlreadyVoted), "Once each");
            Assert.That(match.VoteToKick(2, 4).Result, Is.EqualTo(KickVoteResult.Voted), "2 of 4 is not more than half");
            var kick = match.VoteToKick(3, 4);
            Assert.That((kick.Result, kick.Voter!.UserID, kick.Target!.UserID, kick.Target.Kicked), Is.EqualTo((KickVoteResult.Kicked, 3, 4, true)));
            Assert.That(match.VoteToKick(1, 4).Result, Is.EqualTo(KickVoteResult.TargetAlreadyKicked));
            Assert.That(match.VoteToKick(4, 1).Result, Is.EqualTo(KickVoteResult.VoterKicked));
            Assert.That(match.VoteForWinner(4, WinnerVote.Away), Is.EqualTo(WinnerVoteResult.VoterKicked));
            // Three left: two of them are a majority, and the kicked player's vote no longer counts
            match.VoteForWinner(1, WinnerVote.Away);
            Assert.That(match.VotedWinner(), Is.Null);
            match.VoteForWinner(2, WinnerVote.Away);
            Assert.That(match.VotedWinner(), Is.EqualTo(WinnerVote.Away));
            match.Cancel();
            Assert.That(match.VoteToKick(1, 2).Result, Is.EqualTo(KickVoteResult.MatchOver));
        }
    }

    [Test]
    public void DecideKick_kicks_past_half_of_the_players_still_in_it_once_and_only_while_it_is_played()
    {
        var match = FourPlayers();
        match.VoteToKick(1, 4);
        match.VoteToKick(2, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.DecideKick(9), Is.EqualTo(KickDecision.NotAPlayer));
            Assert.That(match.DecideKick(3), Is.EqualTo(KickDecision.ShortOfMajority), "No votes against them");
            Assert.That(match.DecideKick(4), Is.EqualTo(KickDecision.ShortOfMajority), "2 of 4 is not more than half");
            Assert.That(match.Players.Single(p => p.UserID == 4).Kicked, Is.False);

            // Another player kicked: three still in it, and the same two votes are a majority of them
            match.VoteToKick(1, 3);
            match.VoteToKick(2, 3);
            Assert.That(match.VoteToKick(4, 3).Result, Is.EqualTo(KickVoteResult.Kicked));
            Assert.That(match.DecideKick(4), Is.EqualTo(KickDecision.Kicked));
            Assert.That(match.Players.Single(p => p.UserID == 4).Kicked, Is.True);
            Assert.That(match.DecideKick(4), Is.EqualTo(KickDecision.AlreadyKicked), "Decided once");

            match.Cancel();
            Assert.That(match.DecideKick(1), Is.EqualTo(KickDecision.MatchOver));
        }
    }

    [Test]
    public void A_merge_moves_a_player_and_drops_a_second_one_of_the_same_person_only_once_the_match_ended()
    {
        var match = FourPlayers();
        match.VoteForWinner(1, WinnerVote.Home);
        match.VoteForWinner(3, WinnerVote.Home);
        match.VoteToKick(3, 2);
        match.VoteToKick(2, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.ReassignPlayer(9, 1), Is.EqualTo(PlayerReassignment.NotAPlayer));
            Assert.That(match.ReassignPlayer(3, 1), Is.EqualTo(PlayerReassignment.BothInActiveMatch), "Both playing it: refused");
            Assert.That(match.Players.Select(p => p.UserID), Is.EqualTo(ExpectedPlayersBefore), "Nothing changed");
            Assert.That(match.ReassignPlayer(4, 5), Is.EqualTo(PlayerReassignment.Moved));
            Assert.That(match.Players.Select(p => p.UserID), Is.EqualTo(ExpectedPlayersAfterTheMove));

            match.DecideByVote(MatchedSide.Home);
            Assert.That(match.ReassignPlayer(3, 2), Is.EqualTo(PlayerReassignment.Dropped), "Over: the source's player goes");
            Assert.That(match.Players.Select(p => p.UserID), Is.EqualTo(ExpectedPlayersAfterTheDrop));
            Assert.That(match.MatchedWinnerVotes.Select(v => v.MatchedPlayer.UserID), Is.EqualTo(ExpectedMatchedWinnerVotes), "With its vote");
            Assert.That(match.MatchedKickVotes.Select(v => (v.VoterMatchedPlayer.UserID, v.KickMatchedPlayer.UserID)), Is.EqualTo(new[] { (2, 5) }),
                "And its kick votes; the target's own stay");
        }
    }

    [Test]
    public void A_match_ends_once_by_report_vote_or_cancel()
    {
        var reported = NewMatch();
        var matchID = Guid.NewGuid();
        reported.Report(matchID);
        var voted = NewMatch();
        voted.DecideByVote(MatchedSide.Away);
        var canceled = NewMatch();
        canceled.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((reported.Status, reported.MatchID, reported.WinnerSide), Is.EqualTo((MatchedMatchStatus.Reported, (Guid?)matchID, (MatchedSide?)null)));
            Assert.That((voted.Status, voted.MatchID, voted.WinnerSide), Is.EqualTo((MatchedMatchStatus.VoteDecided, (Guid?)null, (MatchedSide?)MatchedSide.Away)));
            Assert.That((canceled.Status, canceled.WinnerSide), Is.EqualTo((MatchedMatchStatus.Canceled, (MatchedSide?)null)));
            Assert.That(() => voted.Cancel(), Throws.InvalidOperationException, "Already ended");
            Assert.That(() => canceled.Report(Guid.NewGuid()), Throws.InvalidOperationException);
            Assert.That(() => reported.DecideByVote(MatchedSide.Home), Throws.InvalidOperationException);
        }
    }
}

/// <summary>MatchedMatchStatusTests' helpers, shared with its integration tests.</summary>
internal static class MatchedMatchStatusTestData
{
    internal static MatchedMatch NewMatch() => MatchedMatch.Start([], []);

    /// <summary>A match of players 1, 2 (home) against 3, 4 (away).</summary>
    internal static MatchedMatch FourPlayers() => MatchedMatch.Start([1, 2], [3, 4]);
}
