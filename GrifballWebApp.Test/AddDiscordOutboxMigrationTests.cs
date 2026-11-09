using GrifballWebApp.Database;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>The AddDiscordOutbox migration, on whichever database the suite runs on (CI runs both).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AddDiscordOutboxMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp()
    {
        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
            Assert.Ignore("A migration of SQL Server's and Postgres's: SQLite's migrations start after it, from one baseline.");
        _context = await SetUpFixture.NewGrifballContext();
    }

    // Not after SetUp ignored the test (SQLite), which made no database
    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    private async Task<bool> OutboxExists()
    {
        try
        {
            await _context.Outbox.AnyAsync();
            return true;
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or Npgsql.PostgresException)
        {
            return false;
        }
    }

    [Test]
    public async Task Down_DropsTheTable_AndUp_MakesItAgain_WithItsIndexes()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var outbox = migrations.IndexOf(migrations.Single(m => m.EndsWith("_AddDiscordOutbox")));

        await migrator.MigrateAsync(migrations[outbox - 1]);
        var afterDown = await OutboxExists();
        await migrator.MigrateAsync();

        _context.Outbox.Add(new OutboxMessage { Kind = "k", Payload = "{}", GroupKey = "match:1", Sequence = 0 });
        _context.Outbox.Add(new OutboxMessage { Kind = "k", Payload = "{}", GroupKey = null, Sequence = 0 });
        _context.Outbox.Add(new OutboxMessage { Kind = "k", Payload = "{}", GroupKey = null, Sequence = 0 });
        await _context.SaveChangesAsync();
        _context.Outbox.Add(new OutboxMessage { Kind = "k", Payload = "{}", GroupKey = "match:1", Sequence = 0 });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());

        Assert.Multiple(() =>
        {
            Assert.That(afterDown, Is.False);
            Assert.That(duplicate, Is.Not.Null, "A group's sequence numbers are unique; ungrouped messages don't clash");
        });
    }
}
