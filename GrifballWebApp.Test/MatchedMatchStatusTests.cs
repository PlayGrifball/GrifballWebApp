using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.LateLeague;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>A Late League match's life (MatchedMatchStatus, #296): it ends once, one way, and the database refuses the
/// combinations that can't be.</summary>
[TestFixture]
public class MatchedMatchStatusTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static MatchedMatch NewMatch() => new() { HomeTeam = new MatchedTeam(), AwayTeam = new MatchedTeam() };

    [Test]
    public void An_archive_game_is_reported_and_stays_so_when_imported_again()
    {
        var game = new RecordedGame { SourceKey = "archive/1", GameVersionID = 1 };
        var match = NewMatch().Archived(7, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), game);
        match.RecordArchived(game, 7, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Multiple(() =>
        {
            Assert.That((match.Status, match.RecordedGame, match.MatchmakingSeasonID, match.StartedAt),
                Is.EqualTo((MatchedMatchStatus.Reported, game, (int?)7, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc))));
            Assert.That(() => match.RecordArchived(new RecordedGame { SourceKey = "archive/2", GameVersionID = 1 }, 7, DateTime.UtcNow), Throws.InvalidOperationException,
                "One recorded game a match");
            Assert.That(() => NewMatch().Canceled().RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException, "A canceled match is not an archive game");
            match.ReleaseRecordedGame();
            Assert.That((match.RecordedGame, match.RecordedGameID), Is.EqualTo(((RecordedGame?)null, (int?)null)));
        });
    }

    [Test]
    public void A_match_gets_one_thread_then_one_vote_message_in_it()
    {
        var match = NewMatch();

        Assert.Multiple(() =>
        {
            Assert.That(() => match.PostVoteMessage(1), Throws.InvalidOperationException, "No thread to post it in");
            match.AttachThread(10);
            Assert.That(() => match.AttachThread(10), Throws.Nothing, "A retry finding the same thread");
            Assert.That(() => match.AttachThread(11), Throws.InvalidOperationException);
            match.PostVoteMessage(20);
            Assert.That(() => match.PostVoteMessage(21), Throws.InvalidOperationException);
            Assert.That((match.ThreadID, match.VoteMessageID), Is.EqualTo(((ulong?)10, (ulong?)20)));
        });
    }

    /// <summary>A match of players 1, 2 (home) against 3, 4 (away).</summary>
    private static MatchedMatch FourPlayers() => new()
    {
        HomeTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = 1 }, new MatchedPlayer { UserID = 2 }] },
        AwayTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = 3 }, new MatchedPlayer { UserID = 4 }] },
    };

    [Test]
    public void Only_its_players_still_in_it_vote_for_a_winner_once_each_while_it_is_played()
    {
        var match = FourPlayers();

        Assert.Multiple(() =>
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
        });
    }

    [Test]
    public void A_majority_of_the_players_still_in_it_kicks_a_player_who_then_neither_votes_nor_counts()
    {
        var match = FourPlayers();

        Assert.Multiple(() =>
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
        });
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

        Assert.Multiple(() =>
        {
            Assert.That((reported.Status, reported.MatchID, reported.WinnerSide), Is.EqualTo((MatchedMatchStatus.Reported, (Guid?)matchID, (MatchedSide?)null)));
            Assert.That((voted.Status, voted.MatchID, voted.WinnerSide), Is.EqualTo((MatchedMatchStatus.VoteDecided, (Guid?)null, (MatchedSide?)MatchedSide.Away)));
            Assert.That((canceled.Status, canceled.WinnerSide), Is.EqualTo((MatchedMatchStatus.Canceled, (MatchedSide?)null)));
            Assert.That(() => voted.Cancel(), Throws.InvalidOperationException, "Already ended");
            Assert.That(() => canceled.Report(Guid.NewGuid()), Throws.InvalidOperationException);
            Assert.That(() => reported.DecideByVote(MatchedSide.Home), Throws.InvalidOperationException);
        });
    }

    [Test]
    public async Task The_database_refuses_a_result_on_an_active_match_or_a_winner_without_a_vote()
    {
        // The Halo match's players first
        await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);

        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(MatchedMatch m, Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<MatchedMatch>> corrupt)
        {
            await using var ctx = _context.NewContextLike();
            corrupt(ctx.MatchedMatches.Add(m));
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(NewMatch(), e => e.Property(x => x.MatchID).CurrentValue = match.MatchID), Is.True, "An active match has no result yet");
            Assert.That(await Refused(NewMatch().Canceled(), e => e.Property(x => x.WinnerSide).CurrentValue = MatchedSide.Home), Is.True,
                "Only a vote-decided match has a voted winner");
            Assert.That(await Refused(NewMatch(), e => e.Property(x => x.Status).CurrentValue = MatchedMatchStatus.VoteDecided), Is.True);
        });
    }

    [Test]
    public async Task The_queues_game_count_has_vote_decided_games_not_canceled_or_active_ones()
    {
        // The Halo match's players first
        await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        var reported = NewMatch();
        reported.Report(match.MatchID);
        var voted = NewMatch();
        voted.DecideByVote(MatchedSide.Home);
        var canceled = NewMatch();
        canceled.Cancel();
        _context.MatchedMatches.AddRange(reported, voted, canceled, NewMatch());
        await _context.SaveChangesAsync();

        var queue = await new MatchmakingStatsService(_context).GetQueueAsync();

        Assert.That(queue.GameCount, Is.EqualTo(2));
    }
}
