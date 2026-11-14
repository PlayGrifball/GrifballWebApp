using GrifballWebApp.Infrastructure;
using Microsoft.Data.SqlClient;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// TestDatabase.DropDatabase on SQL Server (#436): every test's database is dropped in the background while other tests
/// run, so the drop may end only sessions that are in that database. A KILL by session ID from an earlier list could
/// end another test's session that has since been given the same ID.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TestDatabaseDropTests
{
    [Test]
    public void SqlServerDropSql_KillsNoSession_OnTheFirstAttempt()
    {
        var sql = TestDatabase.SqlServerDropSql("Drop_x", ifExists: false, killSessions: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Does.Not.Contain("KILL"), "SET SINGLE_USER WITH ROLLBACK IMMEDIATE ends the database's sessions by database");
            Assert.That(sql, Does.Contain("ALTER DATABASE [Drop_x] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"));
            Assert.That(sql, Does.Contain("DROP DATABASE [Drop_x]"));
        }
    }

    [Test]
    public void SqlServerDropSql_KillsASessionOnlyWhileItIsStillInTheDatabase_OnARetry()
    {
        var sql = TestDatabase.SqlServerDropSql("Drop_x", ifExists: false, killSessions: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Does.Contain("KILL"));
            Assert.That(sql, Does.Contain("IF EXISTS (SELECT 1 FROM sys.dm_exec_sessions WHERE session_id = "),
                "Each KILL checks the session is still in the database at that moment");
            Assert.That(sql, Does.Contain(" AND database_id = "), "in this database, not merely alive: a reused ID is alive");
            Assert.That(sql, Does.Contain("STRING_AGG"), "not SELECT @v += concatenation, whose row order and completeness SQL Server doesn't promise");
        }
    }

    [Test]
    public async Task DropDatabase_DropsIt_WhenAnotherSessionHoldsItsSingleUserSlot()
    {
        if (TestDatabase.Provider != DatabaseProvider.SqlServer)
            Assert.Ignore("SQL Server's single-user mode.");
        var name = $"DropHeld_{Guid.NewGuid():N}";
        await TestDatabase.CreateDatabase(SetUpFixture.DatabaseContainer, name);
        var connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, name);

        // A session of its own (unpooled) in the database, holding its one single-user slot: the retry's case.
        await using var holder = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await holder.OpenAsync();
        await using (var command = holder.CreateCommand())
        {
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await TestDatabase.DropDatabase(connectionString);

            await using var master = new SqlConnection(TestDatabase.MaintenanceConnectionString(SetUpFixture.DatabaseContainer));
            await master.OpenAsync();
            await using var check = master.CreateCommand();
            check.CommandText = $"SELECT DB_ID('{name}')";
            Assert.That(await check.ExecuteScalarAsync(), Is.EqualTo(DBNull.Value));
        }
        finally
        {
            // Left behind on a failure, it would sit in single-user mode on the shared server for the rest of the run
            await TestDatabase.DropDatabase(connectionString, ifExists: true);
        }
    }

    [Test]
    public async Task TheRetrysKill_EndsTheDatabasesSessions_AndLeavesAnotherDatabasesAlone()
    {
        if (TestDatabase.Provider != DatabaseProvider.SqlServer)
            Assert.Ignore("SQL Server's session kills.");
        var name = $"DropOther_{Guid.NewGuid():N}";
        await TestDatabase.CreateDatabase(SetUpFixture.DatabaseContainer, name);
        var connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, name);
        // One session in the database (the kill's); in another (master), the one that runs the drop and a bystander
        await using var inside = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await inside.OpenAsync();
        await using var other = new SqlConnection(new SqlConnectionStringBuilder(
            TestDatabase.MaintenanceConnectionString(SetUpFixture.DatabaseContainer)) { Pooling = false }.ConnectionString);
        await other.OpenAsync();
        await using var bystander = new SqlConnection(new SqlConnectionStringBuilder(
            TestDatabase.MaintenanceConnectionString(SetUpFixture.DatabaseContainer)) { Pooling = false }.ConnectionString);
        await bystander.OpenAsync();

        try
        {
            // The retry's batch itself, kills and all
            await using (var drop = other.CreateCommand())
            {
                drop.CommandText = TestDatabase.SqlServerDropSql(name, ifExists: false, killSessions: true);
                await drop.ExecuteNonQueryAsync();
            }

            await using var command = bystander.CreateCommand();
            command.CommandText = $"SELECT CASE WHEN DB_ID('{name}') IS NULL THEN 1 ELSE 0 END";
            Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(1), "dropped, and the bystander in master was left alone");
        }
        finally
        {
            await TestDatabase.DropDatabase(connectionString, ifExists: true);
        }
    }
}
