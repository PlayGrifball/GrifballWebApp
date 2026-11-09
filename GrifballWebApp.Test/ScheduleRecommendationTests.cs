using GrifballWebApp.Database;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// A schedule recommendation is an aggregate: made whole with its matches (<see cref="ScheduleRecommendation.Propose"/>),
/// which refuses what the job can't produce, applied once (<see cref="ScheduleRecommendation.Apply"/>), and held to the
/// same by the database's check constraints.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScheduleRecommendationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc);

    private static ScheduleRecommendation Proposed(int seasonID = 1) => ScheduleRecommendation.Propose(seasonID, "commish", 0.5, 12,
    [
        new ProposedMatchTime(1, At, Overlaps: false, HomeTeamAvailable: true, AwayTeamAvailable: true),
        new ProposedMatchTime(2, At.AddHours(1), Overlaps: true, HomeTeamAvailable: true, AwayTeamAvailable: true),
        new ProposedMatchTime(3, At.AddDays(1), Overlaps: false, HomeTeamAvailable: true, AwayTeamAvailable: false),
    ]);

    [Test]
    public void Propose_MakesItWithItsMatches_NotApplied()
    {
        var recommendation = Proposed();

        Assert.Multiple(() =>
        {
            Assert.That((recommendation.SeasonID, recommendation.RequestedBy, recommendation.Fitness, recommendation.Generations), Is.EqualTo((1, "commish", 0.5, 12)));
            Assert.That((recommendation.IsApplied, recommendation.AppliedAt, recommendation.AppliedBy), Is.EqualTo((false, (DateTime?)null, (string?)null)));
            Assert.That(recommendation.Matches.Select(m => m.SeasonMatchID), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(recommendation.Matches.All(m => m.ScheduleRecommendation == recommendation), Is.True);
            Assert.That(recommendation.Matches.Select(m => m.HasConflict), Is.EqualTo(new[] { false, true, true }));
            Assert.That(recommendation.ConflictCount, Is.EqualTo(2));
            Assert.That(recommendation.ProposedTimes(), Is.EqualTo(new Dictionary<int, DateTime> { [1] = At, [2] = At.AddHours(1), [3] = At.AddDays(1) }));
        });
    }

    [Test]
    public void Propose_RefusesWhatTheJobCantProduce()
    {
        var match = new ProposedMatchTime(1, At, false, true, true);

        Assert.Multiple(() =>
        {
            Assert.That(() => ScheduleRecommendation.Propose(1, null, -0.1, 1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Fitness below 0");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, 1.1, 1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Fitness above 1");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, double.NaN, 1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "No fitness");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, 1, -1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Negative generations");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, 1, 1, [match, match with { ScheduledTime = At.AddHours(2) }]),
                Throws.ArgumentException.With.Message.Contains("proposed twice"), "One time per match");
            Assert.That(ScheduleRecommendation.Propose(1, null, 0, 0, []).Matches, Is.Empty, "A season with no matches yet");
        });
    }

    [Test]
    public void Apply_StampsIt_Once()
    {
        var recommendation = Proposed();

        var first = recommendation.Apply("commish", At);
        var again = recommendation.Apply("other", At.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That((first, again), Is.EqualTo((ScheduleRecommendationApply.Applied, ScheduleRecommendationApply.AlreadyApplied)));
            Assert.That((recommendation.IsApplied, recommendation.AppliedAt, recommendation.AppliedBy), Is.EqualTo((true, (DateTime?)At, "commish")), "Kept the first");
            Assert.That(Proposed().Apply(null, At), Is.EqualTo(ScheduleRecommendationApply.Applied), "Nobody signed in");
        });
    }

    [Test]
    public async Task Unapplied_MatchesTheLoadedRule_InTheDatabase()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var season = await AddSeason(context);
            var open = Proposed(season.SeasonID);
            var applied = Proposed(season.SeasonID);
            applied.Apply("commish", At);
            context.ScheduleRecommendations.AddRange(open, applied);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var unapplied = await context.ScheduleRecommendations.Where(ScheduleRecommendation.Unapplied).Select(r => r.ScheduleRecommendationID).ToListAsync();
            var loaded = await context.ScheduleRecommendations.AsNoTracking().Include(r => r.Matches).ToListAsync();
            var children = await context.Set<ScheduleRecommendationMatch>().CountAsync();

            Assert.Multiple(() =>
            {
                Assert.That(unapplied, Is.EqualTo(new[] { open.ScheduleRecommendationID }));
                Assert.That(loaded.Where(r => !r.IsApplied).Select(r => r.ScheduleRecommendationID), Is.EqualTo(unapplied));
                Assert.That(loaded.Select(r => r.Matches.Count), Is.All.EqualTo(3), "Saved with the recommendation");
                Assert.That(children, Is.EqualTo(6));
            });
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task TheDatabase_RefusesImpossibleRecommendations()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var season = await AddSeason(context);

            // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
            async Task<bool> Refused(Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ScheduleRecommendation>> corrupt)
            {
                await using var ctx = context.NewContextLike();
                corrupt(ctx.ScheduleRecommendations.Add(Proposed(season.SeasonID)));
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
                Assert.That(await Refused(e => e.Property(r => r.Fitness).CurrentValue = 1.5), Is.True, "Fitness above 1");
                Assert.That(await Refused(e => e.Property(r => r.Fitness).CurrentValue = -0.5), Is.True, "Fitness below 0");
                Assert.That(await Refused(e => e.Property(r => r.Generations).CurrentValue = -1), Is.True, "Negative generations");
                Assert.That(await Refused(e => e.Property(r => r.AppliedBy).CurrentValue = "commish"), Is.True, "Applied by someone, never applied");
                Assert.That(await Refused(e => e.Property(r => r.AppliedAt).CurrentValue = At), Is.False, "Applied by nobody signed in");
                Assert.That(await Refused(_ => { }), Is.False, "As proposed");
            });
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    internal static async Task<Season> AddSeason(GrifballContext context)
    {
        var season = Season.Seed(seasonName: "S1").WithDates(seasonStart: At, seasonEnd: At.AddDays(28));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }
}
