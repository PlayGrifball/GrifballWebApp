using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The ScheduleRecommendationAggregate migration settles the recommendations its constraints would refuse before adding
/// them: a fitness out of range is clamped to 0 to 1, negative generations become 0, and who applied one that was never
/// applied is cleared; a recommendation as the job and the apply wrote it is left alone. Alike on whichever database the
/// suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScheduleRecommendationAggregateMigrationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ImpossibleRecommendations_AreSettled_BeforeTheConstraints()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_ScheduleRecommendationAggregate"))) - 1]);

        var season = await ScheduleRecommendationDatabaseTestData.AddSeason(_context);
        // As anything but the job and the apply could have written them: past the private setters, as the old model's public ones were
        ScheduleRecommendation Old(double fitness, int generations, DateTime? appliedAt, string? appliedBy)
        {
            var entry = _context.ScheduleRecommendations.Add(ScheduleRecommendation.Propose(season.SeasonID, "commish", 0.5, 10,
                [new ProposedMatchTime(1, At, false, true, true)]));
            entry.Property(r => r.Fitness).CurrentValue = fitness;
            entry.Property(r => r.Generations).CurrentValue = generations;
            entry.Property(r => r.AppliedAt).CurrentValue = appliedAt;
            entry.Property(r => r.AppliedBy).CurrentValue = appliedBy;
            return entry.Entity;
        }
        var fine = Old(0.75, 40, At, "commish");
        var tooFit = Old(1.5, 40, null, null);
        var unfit = Old(-0.25, 40, null, null);
        var negative = Old(0.5, -3, null, null);
        var appliedByNobody = Old(0.5, 40, null, "commish");
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var rows = await _context.ScheduleRecommendations.AsNoTracking()
            .ToDictionaryAsync(r => r.ScheduleRecommendationID, r => (r.Fitness, r.Generations, r.AppliedAt, r.AppliedBy));
        var matches = await _context.Set<ScheduleRecommendationMatch>().CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows[fine.ScheduleRecommendationID], Is.EqualTo((0.75, 40, (DateTime?)At, "commish")), "Left alone");
            Assert.That(rows[tooFit.ScheduleRecommendationID].Fitness, Is.EqualTo(1));
            Assert.That(rows[unfit.ScheduleRecommendationID].Fitness, Is.Zero);
            Assert.That(rows[negative.ScheduleRecommendationID].Generations, Is.Zero);
            Assert.That(rows[appliedByNobody.ScheduleRecommendationID], Is.EqualTo((0.5, 40, (DateTime?)null, (string?)null)), "Not applied: nobody applied it");
            Assert.That(matches, Is.EqualTo(5), "Their matches kept");
        }
    }
}
