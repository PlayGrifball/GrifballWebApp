using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using static GrifballWebApp.Domain.Test.ScheduleRecommendationTestData;
using static GrifballWebApp.Integration.Test.ScheduleRecommendationDatabaseTestData;

namespace GrifballWebApp.Integration.Test;

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
    [Test]
    public async Task Unapplied_MatchesTheLoadedRule_InTheDatabase()
    {
        var context = await TestDatabase.NewGrifballContext();
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

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unapplied, Is.EqualTo(new[] { open.ScheduleRecommendationID }));
                Assert.That(loaded.Where(r => !r.IsApplied).Select(r => r.ScheduleRecommendationID), Is.EqualTo(unapplied));
                Assert.That(loaded.Select(r => r.Matches.Count), Is.All.EqualTo(3), "Saved with the recommendation");
                Assert.That(children, Is.EqualTo(6));
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task TheDatabase_RefusesImpossibleRecommendations()
    {
        var context = await TestDatabase.NewGrifballContext();
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

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Refused(e => e.Property(r => r.Fitness).CurrentValue = 1.5), Is.True, "Fitness above 1");
                Assert.That(await Refused(e => e.Property(r => r.Fitness).CurrentValue = -0.5), Is.True, "Fitness below 0");
                Assert.That(await Refused(e => e.Property(r => r.Generations).CurrentValue = -1), Is.True, "Negative generations");
                Assert.That(await Refused(e => e.Property(r => r.AppliedBy).CurrentValue = "commish"), Is.True, "Applied by someone, never applied");
                Assert.That(await Refused(e => e.Property(r => r.AppliedAt).CurrentValue = At), Is.False, "Applied by nobody signed in");
                Assert.That(await Refused(_ => { }), Is.False, "As proposed");
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

}
