using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The RescheduleAggregate migration drops the match's active-request pointer for the pending request itself (#297),
/// settling first what its unique index and check would refuse: a second pending request of a match is rejected (the
/// pointer's kept, else the newest), and a decision missing its decider or time takes them from its last change.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleAggregateMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private string SeasonMatches => TestDatabase.Provider == DatabaseProvider.Sqlite ? "\"SeasonMatches\"" : "\"Event\".\"SeasonMatches\"";

    [Test]
    public async Task TheActiveRequest_IsTheOnePendingRequest_AndEveryDecisionHasItsDeciderAndTime()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_RescheduleAggregate"))) - 1]);

        var s = await RescheduleTestData.SeedAsync(_context);
        var commissioner = await RescheduleTestData.AddUser(_context, "commish");
        var other = new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Away.TeamID, AwayTeamID = s.Home.TeamID, BestOf = 1 };
        var third = new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Home.TeamID, AwayTeamID = s.Away.TeamID, BestOf = 1 };
        _context.SeasonMatches.AddRange(other, third);
        await _context.SaveChangesAsync();

        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        MatchReschedule Add(SeasonMatch match, string reason)
        {
            var r = MatchReschedule.Request(match.SeasonMatchID, null, null, reason, s.Requester.Id, now);
            _context.MatchReschedules.Add(r);
            return r;
        }
        // The first match points at its older pending request, beside a newer one; the other points at none of its two
        var pointed = Add(s.Match, "pointed");
        var stray = Add(s.Match, "stray");
        var older = Add(other, "older");
        var newer = Add(other, "newer");
        // Approved without its decider or time
        var bare = Add(third, "bare");
        await _context.SaveChangesAsync();
        await _context.Database.ExecuteSqlRawAsync(
            $"UPDATE {SeasonMatches} SET \"ActiveRescheduleRequestId\" = {pointed.MatchRescheduleID} WHERE \"SeasonMatchID\" = {s.Match.SeasonMatchID}");
        await _context.Database.ExecuteSqlRawAsync(
            $"UPDATE \"MatchReschedules\" SET \"Status\" = 1, \"ModifiedByID\" = {commissioner.Id} WHERE \"MatchRescheduleID\" = {bare.MatchRescheduleID}");

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var saved = await _context.MatchReschedules.AsNoTracking().ToDictionaryAsync(r => r.Reason);
        Assert.Multiple(() =>
        {
            Assert.That(saved["pointed"].Status, Is.EqualTo(RescheduleStatus.Pending), "the pointer's request stays active");
            Assert.That(saved["stray"].Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(saved["older"].Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(saved["newer"].Status, Is.EqualTo(RescheduleStatus.Pending), "no pointer: the newest stays active");
            Assert.That(saved["bare"].Status, Is.EqualTo(RescheduleStatus.Approved));
            Assert.That(saved["bare"].ApprovedByUserID, Is.EqualTo(commissioner.Id), "its last change's user");
            Assert.That(saved["bare"].ProcessedAt, Is.EqualTo(saved["bare"].ModifiedAt));
            Assert.That(saved["pointed"].ApprovedByUserID, Is.Null);
            Assert.That(saved["pointed"].ProcessedAt, Is.Null);
            Assert.That(saved.Values.Where(r => r.Status != RescheduleStatus.Pending).Select(r => r.ApprovedByUserID), Is.All.Not.Null);
        });
    }
}
