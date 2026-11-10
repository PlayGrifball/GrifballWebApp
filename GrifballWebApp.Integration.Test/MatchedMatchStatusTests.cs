using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Queries;
using static GrifballWebApp.Domain.Test.MatchedMatchStatusTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>A Late League match's life (MatchedMatchStatus, #296): it ends once, one way, and the database refuses the
/// combinations that can't be.</summary>
[TestFixture]
public class MatchedMatchStatusTests
{
    private GrifballContext _context = null!;
    private static readonly int[] expected = new[] { 1, 4 };
    private static readonly int[] expectedArray = new[] { 3 };

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void Only_an_archive_games_roster_is_set_and_only_it_is_discarded()
    {
        var match = MatchedMatch.Archive(RecordedGame.Seed("archive/1"), 7, DateTime.UtcNow);
        match.SetArchivedRoster([1, 2], [3]);
        var kept = match.HomeTeam.Players.Single(p => p.UserID == 1);
        match.SetArchivedRoster([1, 4], [3]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.HomeTeam.Players.Select(p => p.UserID), Is.EqualTo(expected), "2 left the side, 4 joined");
            Assert.That(match.HomeTeam.Players, Does.Contain(kept), "A player still on it stays the same row");
            Assert.That(match.AwayTeam.Players.Select(p => p.UserID), Is.EqualTo(expectedArray));
            Assert.That(() => FourPlayers().SetArchivedRoster([1], [2]), Throws.InvalidOperationException, "A queue match's players are those it started with");
            Assert.That(() => _context.RemoveArchivedMatch(FourPlayers()), Throws.InvalidOperationException, "Never a queue match");
        }
    }

    [Test]
    public async Task An_archive_game_is_removed_with_its_sides_and_players_and_its_recorded_game_stays()
    {
        var season = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.MatchedMatch, Is.Null, "The game let go of it");
            Assert.That(await fresh.RecordedGames.CountAsync(), Is.EqualTo(1));
            Assert.That((await fresh.MatchedMatches.CountAsync(), await fresh.Set<MatchedTeam>().CountAsync(), await fresh.Set<MatchedPlayer>().CountAsync()), Is.EqualTo((0, 0, 0)));
        }
    }

    [Test]
    public async Task An_archive_game_loaded_without_its_recorded_game_is_known_by_its_key()
    {
        var season = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.MatchmakingSeasons.Add(season);
        await _context.SaveChangesAsync();
        var game = RecordedGame.Seed("archive/late/1");
        _context.MatchedMatches.Add(MatchedMatch.Archive(game, season.MatchmakingSeasonID, season.StartsAt));
        await _context.SaveChangesAsync();

        await using var ctx = _context.NewContextLike();
        var match = await ctx.MatchedMatches.AsNoTracking().SingleAsync();
        match.RecordArchived(game, season.MatchmakingSeasonID, season.EndsAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.RecordedGame, match.StartedAt), Is.EqualTo(((RecordedGame?)null, season.EndsAt)));
            Assert.That(() => match.RecordArchived(RecordedGame.Seed("archive/late/2").WithID(game.RecordedGameID + 1), season.MatchmakingSeasonID, season.EndsAt),
                Throws.InvalidOperationException, "Another game, by its key");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bySecond.VoteToKick(users[1].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(byThird.VoteToKick(users[2].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
        }
        await second.SaveChangesAsync();
        await third.SaveChangesAsync();

        // Decided again, as the handler does once its vote is saved: the last to save sees all three
        third.ChangeTracker.Clear();
        var again = await third.MatchedMatches.WithPlayersAndVotes().SingleAsync();
        Assert.That(again.DecideKick(users[3].Id), Is.EqualTo(KickDecision.Kicked));
        await third.SaveChangesAsync();

        await using var fresh = _context.NewContextLike();
        Assert.That(await fresh.Set<MatchedPlayer>().Where(p => p.Kicked).Select(p => p.UserID).ToListAsync(), Is.EqualTo(new[] { users[3].Id }));
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.MatchedWinnerVotes.All(v => v.MatchedPlayer is null), Is.True, "Loaded without their players");
            Assert.That(match.VoteForWinner(users[0].Id, WinnerVote.Home), Is.EqualTo(WinnerVoteResult.Voted), "The first player's vote, found by ID, changed");
            Assert.That(match.MatchedWinnerVotes, Has.Count.EqualTo(2));
            Assert.That(match.VotedWinner(), Is.Null);
            match.VoteForWinner(users[2].Id, WinnerVote.Away);
            Assert.That(match.VotedWinner(), Is.Null, "Away has 2 of 4, Home 1");
            // A vote against the third player is not one against the fourth, and the first player may vote against both
            Assert.That(match.VoteToKick(users[0].Id, users[2].Id).Result, Is.EqualTo(KickVoteResult.AlreadyVoted));
            Assert.That(match.VoteToKick(users[1].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteToKick(users[0].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteToKick(users[2].Id, users[3].Id).Result, Is.EqualTo(KickVoteResult.Kicked));
        }
    }

    [Test]
    public async Task A_reported_and_an_archive_match_are_found_from_both_ends_of_their_links()
    {
        await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var halo = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        var season = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported.Match!.MatchID, Is.EqualTo(halo.MatchID));
            Assert.That((reported.HomeTeamId, reported.AwayTeamId), Is.Not.EqualTo((0, 0)));
            Assert.That(loadedHalo.MatchedMatch.Id, Is.EqualTo(reported.Id));
            Assert.That(loadedSeason.MatchedMatches.Select(m => (m.Status, m.RecordedGame!.SourceKey, m.RecordedGame.MatchedMatch!.Id)),
                Is.EqualTo(new[] { (MatchedMatchStatus.Reported, "archive/late/1", archive.Id) }));
        }
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

        using (Assert.EnterMultipleScope())
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
        }
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
