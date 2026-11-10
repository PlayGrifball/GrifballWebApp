using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

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

    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private string SeasonMatches => TestDatabase.Provider == DatabaseProvider.Sqlite ? "\"SeasonMatches\"" : "\"Event\".\"SeasonMatches\"";

    [Test]
    public async Task TheActiveRequest_IsTheOnePendingRequest_AndEveryDecisionHasItsDeciderAndTime()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_RescheduleAggregate"))) - 1]);

        var s = await RescheduleTestData.SeedAsync(_context);
        var commissioner = await RescheduleTestData.AddUser(_context, "commish");
        var other = SeasonMatch.Seed(s.Season.SeasonID, s.Away.TeamID, s.Home.TeamID, bestOf: 1);
        var third = SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1);
        _context.SeasonMatches.AddRange(other, third);
        await _context.SaveChangesAsync();

        // The requests as the old schema has them (no periods yet, so not through the model): the first match points at
        // its older pending request, beside a newer one; the other points at none of its two; the third's is approved
        // without its decider or time, by the commissioner's last change
        async Task<int> Add(SeasonMatch match, string reason, int status = 0, int? modifiedBy = null)
        {
            await _context.Database.ExecuteSqlAsync($"""
                INSERT INTO "MatchReschedules" ("SeasonMatchID", "Reason", "RequestedByUserID", "RequestedAt", "Status", "ModifiedByID", "CreatedAt", "ModifiedAt")
                VALUES ({match.SeasonMatchID}, {reason}, {s.Requester.Id}, {Now}, {status}, {modifiedBy}, {Now}, {Now})
                """);
            return await _context.Database.SqlQuery<int>($"""SELECT "MatchRescheduleID" AS "Value" FROM "MatchReschedules" WHERE "Reason" = {reason}""").SingleAsync();
        }
        var pointed = await Add(s.Match, "pointed");
        await Add(s.Match, "stray");
        await Add(other, "older");
        await Add(other, "newer");
        await Add(third, "bare", status: 1, modifiedBy: commissioner.Id);
        await _context.Database.ExecuteSqlRawAsync(
            $"UPDATE {SeasonMatches} SET \"ActiveRescheduleRequestId\" = {pointed} WHERE \"SeasonMatchID\" = {s.Match.SeasonMatchID}");

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var saved = await _context.MatchReschedules.AsNoTracking().ToDictionaryAsync(r => r.Reason);
        using (Assert.EnterMultipleScope())
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
        }
    }
}
