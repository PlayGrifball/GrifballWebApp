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

    private static MatchedMatch NewMatch() => MatchedMatch.Start([], []);

    [Test]
    public void An_archive_game_is_reported_and_only_an_archive_game_is_imported_again()
    {
        var game = RecordedGame.Seed("archive/1");
        var match = MatchedMatch.Archive(game, 7, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        match.RecordArchived(game, 8, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Multiple(() =>
        {
            Assert.That((match.Status, match.RecordedGame, match.MatchmakingSeasonID, match.StartedAt),
                Is.EqualTo((MatchedMatchStatus.Reported, game, (int?)8, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc))));
            Assert.That(() => match.RecordArchived(RecordedGame.Seed("archive/2"), 7, DateTime.UtcNow), Throws.InvalidOperationException,
                "One recorded game a match");
            Assert.That(() => NewMatch().RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException, "An active queue match never becomes one");
            Assert.That(() => NewMatch().Reported(Guid.NewGuid()).RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException,
                "Nor one reported with a Halo match");
            Assert.That(() => NewMatch().Canceled().RecordArchived(game, 7, DateTime.UtcNow), Throws.InvalidOperationException, "Nor a canceled one");
        });
    }

    [Test]
    public void Only_an_archive_games_roster_is_set_and_only_it_is_discarded()
    {
        var match = MatchedMatch.Archive(RecordedGame.Seed("archive/1"), 7, DateTime.UtcNow);
        match.SetArchivedRoster([1, 2], [3]);
        var kept = match.HomeTeam.Players.Single(p => p.UserID == 1);
        match.SetArchivedRoster([1, 4], [3]);

        Assert.Multiple(() =>
        {
            Assert.That(match.HomeTeam.Players.Select(p => p.UserID), Is.EqualTo(new[] { 1, 4 }), "2 left the side, 4 joined");
            Assert.That(match.HomeTeam.Players, Does.Contain(kept), "A player still on it stays the same row");
            Assert.That(match.AwayTeam.Players.Select(p => p.UserID), Is.EqualTo(new[] { 3 }));
            Assert.That(() => FourPlayers().SetArchivedRoster([1], [2]), Throws.InvalidOperationException, "A queue match's players are those it started with");
            Assert.That(() => _context.RemoveArchivedMatch(FourPlayers()), Throws.InvalidOperationException, "Never a queue match");
        });
    }

    [Test]
    public async Task An_archive_game_is_removed_with_its_sides_and_players_and_its_recorded_game_stays()
    {
        var season = new MatchmakingSeason { Name = "Late League Season 8 (Halo 3)", StartsAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndsAt = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        var one = await _context.AddUser("one");
        var two = await _context.AddUser("two");
        _context.MatchmakingSeasons.Add(season);
        await _context.SaveChangesAsync();
        _context.MatchedMatches.Add(MatchedMatchTestExtensions.ArchiveGame(season.MatchmakingSeasonID, season.StartsAt, [one.Id], [two.Id],
            RecordedGame.Seed("archive/late/1")));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var game = await _context.RecordedGames.Include(g => g.MatchedMatch!).ThenInclude(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(g => g.MatchedMatch!).ThenInclude(m => m.AwayTeam).ThenInclude(t => t.Players)
            .SingleAsync();
        _context.RemoveArchivedMatch(game.MatchedMatch!);
        await _context.SaveChangesAsync();

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(game.MatchedMatch, Is.Null, "The game let go of it");
            Assert.That(await fresh.RecordedGames.CountAsync(), Is.EqualTo(1));
            Assert.That((await fresh.MatchedMatches.CountAsync(), await fresh.MatchedTeams.CountAsync(), await fresh.MatchedPlayers.CountAsync()), Is.EqualTo((0, 0, 0)));
        });
    }

    [Test]
    public async Task An_archive_game_loaded_without_its_recorded_game_is_known_by_its_key()
    {
        var season = new MatchmakingSeason { Name = "Late League Season 8 (Halo 3)", StartsAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndsAt = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        _context.MatchmakingSeasons.Add(season);
        await _context.SaveChangesAsync();
        var game = RecordedGame.Seed("archive/late/1");
        _context.MatchedMatches.Add(MatchedMatch.Archive(game, season.MatchmakingSeasonID, season.StartsAt));
        await _context.SaveChangesAsync();

        await using var ctx = _context.NewContextLike();
        var match = await ctx.MatchedMatches.AsNoTracking().SingleAsync();
        match.RecordArchived(game, season.MatchmakingSeasonID, season.EndsAt);

        Assert.Multiple(() =>
        {
            Assert.That((match.RecordedGame, match.StartedAt), Is.EqualTo(((RecordedGame?)null, season.EndsAt)));
            Assert.That(() => match.RecordArchived(RecordedGame.Seed("archive/late/2").WithID(game.RecordedGameID + 1), season.MatchmakingSeasonID, season.EndsAt),
                Throws.InvalidOperationException, "Another game, by its key");
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
    private static MatchedMatch FourPlayers() => MatchedMatch.Start([1, 2], [3, 4]);

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
    public void DecideKick_kicks_past_half_of_the_players_still_in_it_once_and_only_while_it_is_played()
    {
        var match = FourPlayers();
        match.VoteToKick(1, 4);
        match.VoteToKick(2, 4);

        Assert.Multiple(() =>
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
        });
    }

    [Test]
    public async Task Two_kick_votes_cast_at_once_each_short_of_a_majority_kick_once_decided_again_on_the_saved_votes()
    {
        var users = new List<User>();
        for (var i = 1; i <= 4; i++)
            users.Add(await _context.AddUser($"player{i}"));
        var first = MatchedMatch.Start(users.Take(2).Select(u => u.Id), users.Skip(2).Select(u => u.Id));
        _context.MatchedMatches.Add(first);
        await _context.SaveChangesAsync();
        first.VoteToKick(users[0].Id, users[3].Id);
        await _context.SaveChangesAsync();

        // The second and third players vote against the fourth at the same moment, each in a context of its own, each loading
        // the first vote only: 2 of 4 each, no majority
        await using var second = _context.NewContextLike();
        await using var third = _context.NewContextLike();
        var bySecond = await second.MatchedMatches.WithPlayersAndVotes().SingleAsync();
        var byThird = await third.MatchedMatches.WithPlayersAndVotes().SingleAsync();
        Assert.That(bySecond.VoteToKick(users[1].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
        Assert.That(byThird.VoteToKick(users[2].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
        await second.SaveChangesAsync();
        await third.SaveChangesAsync();

        // Decided again, as the handler does once its vote is saved: the last to save sees all three
        third.ChangeTracker.Clear();
        var again = await third.MatchedMatches.WithPlayersAndVotes().SingleAsync();
        Assert.That(again.DecideKick(users[3].Id), Is.EqualTo(KickDecision.Kicked));
        await third.SaveChangesAsync();

        await using var fresh = _context.NewContextLike();
        Assert.That(await fresh.MatchedPlayers.Where(p => p.Kicked).Select(p => p.UserID).ToListAsync(), Is.EqualTo(new[] { users[3].Id }));
    }

    [Test]
    public async Task Votes_count_by_their_players_id_when_loaded_without_them_and_only_against_their_own_target()
    {
        var users = new List<User>();
        for (var i = 1; i <= 4; i++)
            users.Add(await _context.AddUser($"player{i}"));
        var saved = MatchedMatch.Start(users.Take(2).Select(u => u.Id), users.Skip(2).Select(u => u.Id));
        _context.MatchedMatches.Add(saved);
        await _context.SaveChangesAsync();
        saved.VoteForWinner(users[0].Id, WinnerVote.Away);
        saved.VoteForWinner(users[1].Id, WinnerVote.Away);
        saved.VoteToKick(users[0].Id, users[2].Id);
        await _context.SaveChangesAsync();

        // Each player's own side of them, read-only: the votes it cast and those against it, and the user's players
        var (first, third) = (saved.HomeTeam.Players.First(), saved.AwayTeam.Players.First());
        Assert.That((first.MatchedWinnerVotes.Count, first.VoterMatchedKickVotes.Count, third.KickMatchedKickVotes.Count, users[0].MatchedPlayers.Single()),
            Is.EqualTo((1, 1, 1, first)));

        // Untracked, its votes come without their players: found by ID only
        await using var ctx = _context.NewContextLike();
        var match = await ctx.MatchedMatches.AsNoTracking()
            .Include(m => m.HomeTeam.Players).Include(m => m.AwayTeam.Players)
            .Include(m => m.MatchedWinnerVotes).Include(m => m.MatchedKickVotes)
            .AsSplitQuery()
            .SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(match.MatchedWinnerVotes.All(v => v.MatchedPlayer is null), Is.True, "Loaded without their players");
            Assert.That(match.VoteForWinner(users[0].Id, WinnerVote.Home), Is.EqualTo(WinnerVoteResult.Voted), "The first player's vote, found by ID, changed");
            Assert.That(match.MatchedWinnerVotes.Count, Is.EqualTo(2));
            Assert.That(match.VotedWinner(), Is.Null);
            match.VoteForWinner(users[2].Id, WinnerVote.Away);
            Assert.That(match.VotedWinner(), Is.Null, "Away has 2 of 4, Home 1");
            // A vote against the third player is not one against the fourth, and the first player may vote against both
            Assert.That(match.VoteToKick(users[0].Id, users[2].Id).Result, Is.EqualTo(KickVoteResult.AlreadyVoted));
            Assert.That(match.VoteToKick(users[1].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteToKick(users[0].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteToKick(users[2].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Kicked));
        });
    }

    [Test]
    public void A_merge_moves_a_player_and_drops_a_second_one_of_the_same_person_only_once_the_match_ended()
    {
        var match = FourPlayers();
        match.VoteForWinner(1, WinnerVote.Home);
        match.VoteForWinner(3, WinnerVote.Home);
        match.VoteToKick(3, 2);
        match.VoteToKick(2, 4);

        Assert.Multiple(() =>
        {
            Assert.That(match.ReassignPlayer(9, 1), Is.EqualTo(PlayerReassignment.NotAPlayer));
            Assert.That(match.ReassignPlayer(3, 1), Is.EqualTo(PlayerReassignment.BothInActiveMatch), "Both playing it: refused");
            Assert.That(match.Players.Select(p => p.UserID), Is.EqualTo(new[] { 1, 2, 3, 4 }), "Nothing changed");
            Assert.That(match.ReassignPlayer(4, 5), Is.EqualTo(PlayerReassignment.Moved));
            Assert.That(match.Players.Select(p => p.UserID), Is.EqualTo(new[] { 1, 2, 3, 5 }));

            match.DecideByVote(MatchedSide.Home);
            Assert.That(match.ReassignPlayer(3, 2), Is.EqualTo(PlayerReassignment.Dropped), "Over: the source's player goes");
            Assert.That(match.Players.Select(p => p.UserID), Is.EqualTo(new[] { 1, 2, 5 }));
            Assert.That(match.MatchedWinnerVotes.Select(v => v.MatchedPlayer.UserID), Is.EqualTo(new[] { 1 }), "With its vote");
            Assert.That(match.MatchedKickVotes.Select(v => (v.VoterMatchedPlayer.UserID, v.KickMatchedPlayer.UserID)), Is.EqualTo(new[] { (2, 5) }),
                "And its kick votes; the target's own stay");
        });
    }

    [Test]
    public async Task A_reported_and_an_archive_match_are_found_from_both_ends_of_their_links()
    {
        await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var halo = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        var season = new MatchmakingSeason { Name = "Late League Season 8 (Halo 3)", StartsAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndsAt = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        _context.MatchmakingSeasons.Add(season);
        await _context.SaveChangesAsync();
        var game = RecordedGame.Seed("archive/late/1");
        var archive = MatchedMatch.Archive(game, season.MatchmakingSeasonID, season.StartsAt);
        _context.MatchedMatches.AddRange(NewMatch().Reported(halo.MatchID), archive);
        await _context.SaveChangesAsync();

        await using var ctx = _context.NewContextLike();
        var reported = await ctx.MatchedMatches.Include(m => m.Match).SingleAsync(m => m.MatchID == halo.MatchID);
        var loadedSeason = await ctx.MatchmakingSeasons.Include(s => s.MatchedMatches).ThenInclude(m => m.RecordedGame).SingleAsync();
        var loadedHalo = await ctx.Matches.Include(m => m.MatchedMatch).SingleAsync(m => m.MatchID == halo.MatchID);

        Assert.Multiple(() =>
        {
            Assert.That(reported.Match!.MatchID, Is.EqualTo(halo.MatchID));
            Assert.That((reported.HomeTeamId, reported.AwayTeamId), Is.Not.EqualTo((0, 0)));
            Assert.That(loadedHalo.MatchedMatch.Id, Is.EqualTo(reported.Id));
            Assert.That(loadedSeason.MatchedMatches.Select(m => (m.Status, m.RecordedGame!.SourceKey, m.RecordedGame.MatchedMatch!.Id)),
                Is.EqualTo(new[] { (MatchedMatchStatus.Reported, "archive/late/1", archive.Id) }));
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

        var game = RecordedGame.Seed("archive/1");
        _context.RecordedGames.Add(game);
        await _context.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(NewMatch(), e => e.Property(x => x.MatchID).CurrentValue = match.MatchID), Is.True, "An active match has no result yet");
            Assert.That(await Refused(NewMatch().Canceled(), e => e.Property(x => x.WinnerSide).CurrentValue = MatchedSide.Home), Is.True,
                "Only a vote-decided match has a voted winner");
            Assert.That(await Refused(NewMatch(), e => e.Property(x => x.Status).CurrentValue = MatchedMatchStatus.VoteDecided), Is.True);
            Assert.That(await Refused(NewMatch().Reported(match.MatchID), e => e.Property(x => x.RecordedGameID).CurrentValue = game.RecordedGameID), Is.True,
                "A Halo match or a recorded game, not both");
            Assert.That(await Refused(NewMatch().Canceled(), e => e.Property(x => x.Status).CurrentValue = MatchedMatchStatus.Reported), Is.True,
                "A reported match has a result");
            Assert.That(await Refused(NewMatch(), e => e.Property(x => x.VoteMessageID).CurrentValue = 7UL), Is.True,
                "A vote message is posted in the match's thread");
            Assert.That(await Refused(NewMatch().Announced(5, 7), _ => { }), Is.False, "With its thread it is fine");
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
