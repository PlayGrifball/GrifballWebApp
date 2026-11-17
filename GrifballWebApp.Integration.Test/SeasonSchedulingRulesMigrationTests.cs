using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The SeasonSchedulingRules migration drops the schedule recommendations not applied yet: saved before it, their times are
/// the league's Eastern wall-clock times, which applying them now would take for UTC. Applied ones stay, as history.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonSchedulingRulesMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task RecommendationsNotApplied_AreDropped_TheAppliedKept()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_SeasonSchedulingRules")));
        await migrator.MigrateAsync(migrations[index - 1]);

        // The tables these are in are the same before the migration as after it
        var season = Season.Seed(seasonName: "Before");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        _context.Teams.AddRange(home, away);
        await _context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var eastern = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var pending = ScheduleRecommendation.Propose(season.SeasonID, "staff", 0.5, 10, [new ProposedMatchTime(match.SeasonMatchID, eastern, false, true, true)]);
        var applied = ScheduleRecommendation.Propose(season.SeasonID, "staff", 0.5, 10, [new ProposedMatchTime(match.SeasonMatchID, eastern, false, true, true)]);
        applied.Apply("staff", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        _context.ScheduleRecommendations.AddRange(pending, applied);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var left = await _context.ScheduleRecommendations.AsNoTracking().Include(r => r.Matches).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(left.Select(r => r.ScheduleRecommendationID), Is.EqualTo(new[] { applied.ScheduleRecommendationID }));
            Assert.That(left.Single().Matches, Has.Count.EqualTo(1));
            Assert.That(await _context.Set<ScheduleRecommendationMatch>().CountAsync(), Is.EqualTo(1), "The dropped one's times went with it");
        }
    }
}
