using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The database's half of a season match's result state (#298): it refuses the result pairs no method makes and a BYE
/// with a team, and the SeasonMatchAggregate migration settles the rows from before, alike on whichever database the
/// suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchAggregateDatabaseTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static void SetResults(EntityEntry<SeasonMatch> entry, SeasonMatchResult? home, SeasonMatchResult? away)
    {
        entry.Property(x => x.HomeTeamResult).CurrentValue = home;
        entry.Property(x => x.AwayTeamResult).CurrentValue = away;
    }

    [Test]
    public async Task The_database_refuses_an_impossible_pair_of_results_and_a_BYE_with_a_team()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var (season, home, away) = (seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID);

        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(int? homeTeam, int? awayTeam, SeasonMatchResult? homeResult, SeasonMatchResult? awayResult)
        {
            await using var ctx = _context.NewContextLike();
            SetResults(ctx.SeasonMatches.Add(SeasonMatch.Seed(season, homeTeam, awayTeam, 3)), homeResult, awayResult);
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

        const SeasonMatchResult W = SeasonMatchResult.Won, L = SeasonMatchResult.Loss, F = SeasonMatchResult.Forfeit, B = SeasonMatchResult.Bye;
        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(home, away, W, W), Is.True, "Two winners");
            Assert.That(await Refused(home, away, L, L), Is.True, "Two losers");
            Assert.That(await Refused(home, away, W, null), Is.True, "A winner without a loser");
            Assert.That(await Refused(home, away, null, L), Is.True);
            Assert.That(await Refused(home, away, L, F), Is.True, "A forfeit against a loss: nobody won");
            Assert.That(await Refused(null, away, B, L), Is.True, "A BYE beats nobody");
            Assert.That(await Refused(home, away, B, W), Is.True, "A BYE side has no team");
            Assert.That(await Refused(home, null, B, null), Is.True, "A BYE side has no team, even an undecided one");

            // The 11 legal pairs
            foreach (var (h, a, hr, ar) in new (int?, int?, SeasonMatchResult?, SeasonMatchResult?)[]
            {
                (home, away, null, null), (home, away, W, L), (home, away, L, W), (home, away, W, F), (home, away, F, W), (home, away, F, F),
                (null, away, B, null), (home, null, null, B), (null, null, B, B), (null, away, B, W), (home, null, W, B),
            })
                Assert.That(await Refused(h, a, hr, ar), Is.False, $"{hr}/{ar}");
        });
    }

    [Test]
    public async Task The_migration_settles_the_rows_from_before_and_keeps_the_legal_ones()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_SeasonMatchAggregate"))) - 1]);

        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var (season, home, away) = (seeded.Season.SeasonID, seeded.Home.TeamID, seeded.Away.TeamID);

        // Rows as the app could leave them before the checks (the migration before has none: its Down dropped them)
        var rows = new Dictionary<string, SeasonMatch>();
        void Row(string name, int? homeTeam, int? awayTeam, SeasonMatchResult? homeResult, SeasonMatchResult? awayResult, int? homeScore = null, int? awayScore = null)
        {
            var match = SeasonMatch.Seed(season, homeTeam, awayTeam, 3);
            var entry = _context.SeasonMatches.Add(match);
            SetResults(entry, homeResult, awayResult);
            entry.Property(x => x.HomeTeamScore).CurrentValue = homeScore;
            entry.Property(x => x.AwayTeamScore).CurrentValue = awayScore;
            rows[name] = match;
        }
        const SeasonMatchResult W = SeasonMatchResult.Won, L = SeasonMatchResult.Loss, F = SeasonMatchResult.Forfeit, B = SeasonMatchResult.Bye;
        Row("decided", home, away, W, L, 2, 1);
        Row("double forfeit", home, away, F, F);
        Row("BYE win", null, away, B, W);
        Row("imported, a side unknown", null, away, L, W, 0, 2);
        Row("a winner alone", home, away, W, null, 2, 0);
        Row("two winners", home, away, W, W, 2, 2);
        Row("a stale BYE, its team since sent on", home, away, B, W);
        Row("a stale BYE, its team arrived", home, null, B, null);
        await _context.SaveChangesAsync();

        // Reschedule requests left pending on decided matches (#297): closed, unless the match is reopened above
        var requester = await _context.Users.FirstAsync();
        var commissioner = (await _context.Users.OrderBy(u => u.Id).Skip(1).FirstAsync()).Id;
        var onDecided = await RescheduleTestData.AddReschedule(_context, rows["decided"].SeasonMatchID, requester.Id);
        var onReopened = await RescheduleTestData.AddReschedule(_context, rows["two winners"].SeasonMatchID, requester.Id);
        var onUndecided = await RescheduleTestData.AddReschedule(_context, seeded.SeasonMatch.SeasonMatchID, requester.Id);
        var rejectedBefore = await RescheduleTestData.AddReschedule(_context, rows["double forfeit"].SeasonMatchID, commissioner, RescheduleStatus.Rejected);

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.SeasonMatches.AsNoTracking().ToDictionaryAsync(m => m.SeasonMatchID);
        (SeasonMatchResult?, SeasonMatchResult?, int?, int?) Of(string name)
        {
            var m = after[rows[name].SeasonMatchID];
            return (m.HomeTeamResult, m.AwayTeamResult, m.HomeTeamScore, m.AwayTeamScore);
        }
        var requests = await _context.MatchReschedules.AsNoTracking().ToDictionaryAsync(x => x.MatchRescheduleID);
        Assert.Multiple(() =>
        {
            var closed = requests[onDecided.MatchRescheduleID];
            Assert.That((closed.Status, closed.ApprovedByUserID, closed.CommissionerNotes),
                Is.EqualTo((RescheduleStatus.Rejected, (int?)requester.Id, MatchReschedule.ClosedBecauseDecided)), "A decided match's pending request is closed");
            Assert.That(closed.ProcessedAt, Is.Not.Null);
            Assert.That(requests[onReopened.MatchRescheduleID].Status, Is.EqualTo(RescheduleStatus.Pending), "A reopened match keeps its request");
            Assert.That(requests[onUndecided.MatchRescheduleID].Status, Is.EqualTo(RescheduleStatus.Pending));
            var before = requests[rejectedBefore.MatchRescheduleID];
            Assert.That((before.Status, before.ApprovedByUserID, before.CommissionerNotes), Is.EqualTo((RescheduleStatus.Rejected, (int?)commissioner, (string?)null)),
                "A request decided already is left as it was");

            Assert.That(Of("decided"), Is.EqualTo(((SeasonMatchResult?)W, (SeasonMatchResult?)L, (int?)2, (int?)1)), "Legal: kept");
            Assert.That(Of("double forfeit"), Is.EqualTo(((SeasonMatchResult?)F, (SeasonMatchResult?)F, (int?)null, (int?)null)));
            Assert.That(Of("BYE win"), Is.EqualTo(((SeasonMatchResult?)B, (SeasonMatchResult?)W, (int?)null, (int?)null)));
            Assert.That(Of("imported, a side unknown"), Is.EqualTo(((SeasonMatchResult?)L, (SeasonMatchResult?)W, (int?)0, (int?)2)),
                "The import's own rows satisfy the checks");
            Assert.That(Of("a winner alone"), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null, (int?)2, (int?)0)),
                "Reopened, its score kept");
            Assert.That(Of("two winners"), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null, (int?)2, (int?)2)));
            Assert.That(Of("a stale BYE, its team since sent on"), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null, (int?)null, (int?)null)),
                "The BYE goes, and the win it gave");
            Assert.That(Of("a stale BYE, its team arrived"), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null, (int?)null, (int?)null)));
        });
    }
}
