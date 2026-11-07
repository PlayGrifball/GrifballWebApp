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
        var match = NewMatch().Archived();
        match.RecordArchived();

        Assert.Multiple(() =>
        {
            Assert.That(match.Status, Is.EqualTo(MatchedMatchStatus.Reported));
            Assert.That(() => NewMatch().Canceled().RecordArchived(), Throws.InvalidOperationException, "A canceled match is not an archive game");
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
