using System.Data;
using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Npgsql;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// What this layer sends to SQL Server and PostgreSQL, and what it does with the answer, on a fake connection
/// (FakeDbConnection) that records each command and answers it as a server would: the server column queries, the
/// source snapshot, the target writers' statements around the rows, the named locks, a season's row lock, and SQL
/// Server's .bak made and read back. No server and no network: the rows themselves go in by SqlBulkCopy and binary COPY,
/// which need the real drivers, and stay the integration tests'.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ServerSqlUnitTests
{
    private static readonly string[] ColumnQueryColumns = ["schema", "table", "column", "type", "generated", "identity"];
    private static readonly IsolationLevel[] RepeatableRead = [IsolationLevel.RepeatableRead];
    private static readonly IsolationLevel[] Snapshot = [IsolationLevel.Snapshot];
    private static readonly object?[][] ReadBack = [[1, "Red"], [2, null]];
    private static readonly string[] OnlyTheDelete = ["DELETE FROM [dbo].[Teams]"];
    private static readonly string[] Analyzed = ["Analyzed the target's tables"];
    /// <summary>A SQL Server-like connection: open, and every command must carry a pending local transaction.</summary>
    private readonly FakeDbConnection _connection = new() { RequiresCommandTransaction = true };

    /// <summary>What was sent, the SQL Server catalog's queries (sys.periods, sys.indexes) left out: every write is here,
    /// a SELECT that writes included.</summary>
    private static List<string> Statements(FakeDbConnection connection) =>
        [.. connection.Sent.Select(c => c.Sql).Where(s => !s.Contains("FROM sys.", StringComparison.Ordinal))];

    // ---- SqlDialect: each server's columns ----

    [Test]
    public async Task SqlServerColumns_AreReadFromItsCatalog_TheDefaultSchemaAsNone_AndEfsHistoryLeftOut()
    {
        await _connection.OpenAsync();
        _connection.Answer = _ => FakeDbConnection.Rows(ColumnQueryColumns,
            ["dbo", "Seasons", "SeasonID", "int", false, true],
            ["dbo", "Seasons", "Total", "int", true, false],
            ["Auth", "Users", "Id", "int", false, true],
            ["dbo", "__EFMigrationsHistory", "MigrationId", "nvarchar", false, false]);
        await using var transaction = await _connection.BeginTransactionAsync();

        var columns = await SqlDialect.For(DatabaseProvider.SqlServer).ReadColumnsAsync(_connection, transaction, CancellationToken.None);

        var sent = _connection.Sent.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(columns.Keys, Is.EquivalentTo(new[] { new TableName("", "Seasons"), new TableName("Auth", "Users") }));
            Assert.That(columns[new TableName("", "Seasons")], Is.EqualTo(new[] { new ColumnInfo("SeasonID", "int", false, true), new ColumnInfo("Total", "int", true, false) }));
            Assert.That(sent.Sql, Does.Contain("FROM sys.tables t").And.Contain("c.is_computed, c.is_identity"));
            Assert.That(sent.Transaction, Is.SameAs(transaction));
        }
    }

    [Test]
    public async Task PostgresColumns_AreReadFromInformationSchema_PublicAsNone()
    {
        await _connection.OpenAsync();
        _connection.Answer = _ => FakeDbConnection.Rows(ColumnQueryColumns,
            ["public", "Seasons", "SeasonID", "int4", false, true],
            ["Event", "Teams", "PeriodEnd", "timestamptz", true, false]);

        var columns = await SqlDialect.For(DatabaseProvider.Postgres).ReadColumnsAsync(_connection, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(columns[new TableName("", "Seasons")], Is.EqualTo(new[] { new ColumnInfo("SeasonID", "int4", false, true) }));
            Assert.That(columns[new TableName("Event", "Teams")], Is.EqualTo(new[] { new ColumnInfo("PeriodEnd", "timestamptz", true, false) }));
            Assert.That(_connection.Sent.Single().Sql, Does.Contain("FROM information_schema.columns c").And.Contain("c.is_generated = 'ALWAYS'"));
        }
    }

    [Test]
    public async Task ReadRows_SelectsThePlansColumns_EachAsTheDriverReadsIt_NullsAsNull()
    {
        await _connection.OpenAsync();
        _connection.Answer = _ => FakeDbConnection.Rows(["Id", "Name"], [1, "Red"], [2, null]);
        var plan = new TablePlan(new TableName("", "Teams"), [new ColumnInfo("Id", "int", false, true), new ColumnInfo("Name", "nvarchar", false, false)], [0], [], []);

        var rows = await DatabaseConverter.ReadRowsAsync(SqlDialect.For(DatabaseProvider.SqlServer), _connection, null, plan, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows, Is.EqualTo(ReadBack));
            Assert.That((_connection.Sent.Single().Sql, _connection.Sent.Single().Timeout), Is.EqualTo(("SELECT [Id], [Name] FROM [dbo].[Teams]", 0)));
        }
    }

    // ---- DatabaseConverter: the source's snapshot, and the connections ----

    [Test]
    public async Task SourceSnapshot_OnPostgres_IsARepeatableReadReadOnlyTransaction()
    {
        await _connection.OpenAsync();
        var transaction = await new DatabaseConverter(new FakeLogger<DatabaseConverter>()).BeginSourceTransactionAsync(DatabaseProvider.Postgres, _connection, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_connection.TransactionsBegun, Is.EqualTo(RepeatableRead));
            Assert.That((_connection.Sent.Single().Sql, _connection.Sent.Single().Transaction), Is.EqualTo(("SET TRANSACTION READ ONLY", transaction)), "read only, in that transaction");
        }
    }

    [Test]
    public async Task SourceSnapshot_OnSqlServerAllowingSnapshots_IsASnapshotTransaction()
    {
        await _connection.OpenAsync();
        _connection.Answer = sent => sent.Sql.Contains("snapshot_isolation_state", StringComparison.Ordinal) ? (byte)1 : null;

        var transaction = await new DatabaseConverter(new FakeLogger<DatabaseConverter>()).BeginSourceTransactionAsync(DatabaseProvider.SqlServer, _connection, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transaction?.IsolationLevel, Is.EqualTo(IsolationLevel.Snapshot));
            Assert.That(_connection.TransactionsBegun, Is.EqualTo(Snapshot));
        }
    }

    [Test]
    public async Task SourceSnapshot_OnSqlServerWithoutSnapshots_IsNone_AndSaysNothingShouldWrite()
    {
        await _connection.OpenAsync();
        _connection.Answer = _ => (byte)0;
        var log = new FakeLogger<DatabaseConverter>();

        var transaction = await new DatabaseConverter(log).BeginSourceTransactionAsync(DatabaseProvider.SqlServer, _connection, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transaction, Is.Null);
            Assert.That(_connection.TransactionsBegun, Is.Empty);
            Assert.That(log.At(LogLevel.Warning).Single(), Does.StartWith("The source SQL Server database doesn't allow snapshot isolation"));
        }
    }

    [TestCase(DatabaseProvider.SqlServer, typeof(SqlConnection))]
    [TestCase(DatabaseProvider.Postgres, typeof(NpgsqlConnection))]
    [TestCase(DatabaseProvider.Sqlite, typeof(SqliteConnection))]
    public async Task Connect_IsTheProvidersConnection_Unopened(DatabaseProvider provider, Type type)
    {
        await using var connection = DatabaseConverter.Connect(new DatabaseEndpoint(provider, ConnectionStringOf(provider)));

        Assert.That((connection.GetType(), connection.State), Is.EqualTo((type, ConnectionState.Closed)));
    }

    private static string ConnectionStringOf(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.SqlServer => "Server=db;Database=grif",
        DatabaseProvider.Postgres => "Host=db;Database=grif",
        _ => "Data Source=grif.db",
    };

    [Test]
    public void Connect_ToAnUnknownProvider_IsRefused()
        => Assert.That(() => DatabaseConverter.Connect(new DatabaseEndpoint((DatabaseProvider)99, "x")), Throws.InstanceOf<ArgumentOutOfRangeException>());

    // ---- The target writers ----

    private static readonly string[] TemporalColumns = ["schema", "table", "historySchema", "history", "start", "end", "scale", "startHidden", "endHidden"];
    private static readonly string[] IndexColumns = ["name", "unique", "type", "filter", "column", "descending", "included"];
    private static readonly TablePlan Teams = new(new TableName("", "Teams"), [new ColumnInfo("TeamID", "int", false, true)], [0], [], []);

    /// <summary>SQL Server's catalog: Teams temporal (history TeamsHistory, its start hidden), an index on its period with an
    /// included column, and a filtered clustered one on the history's.</summary>
    private static System.Data.Common.DbDataReader? SqlServerCatalog(SentCommand sent) => sent.Sql switch
    {
        var s when s.Contains("sys.periods", StringComparison.Ordinal) => FakeDbConnection.Rows(TemporalColumns,
            ["dbo", "Teams", "dbo", "TeamsHistory", "PeriodStart", "PeriodEnd", (byte)7, true, false]),
        var s when s.Contains("sys.indexes", StringComparison.Ordinal) && s.Contains("OBJECT_ID(N'[dbo].[Teams]')", StringComparison.Ordinal) => FakeDbConnection.Rows(IndexColumns,
            ["IX_Teams_Period", true, "NONCLUSTERED", null, "PeriodEnd", false, false],
            ["IX_Teams_Period", true, "NONCLUSTERED", null, "PeriodStart", true, false],
            ["IX_Teams_Period", true, "NONCLUSTERED", null, "TeamName", false, true]),
        var s when s.Contains("sys.indexes", StringComparison.Ordinal) => FakeDbConnection.Rows(IndexColumns,
            ["ix_TeamsHistory", false, "CLUSTERED", "([PeriodEnd]>'2000-01-01')", "PeriodEnd", false, false]),
        _ => null,
    };

    private static readonly string[] SqlServerPrepare =
    [
        "ALTER TABLE [dbo].[Teams] SET (SYSTEM_VERSIONING = OFF)",
        "ALTER TABLE [dbo].[Teams] DROP PERIOD FOR SYSTEM_TIME",
        "DELETE FROM [dbo].[Teams]",
        "DROP INDEX [IX_Teams_Period] ON [dbo].[Teams]",
        "ALTER TABLE [dbo].[Teams] DROP COLUMN [PeriodStart], [PeriodEnd]",
        "ALTER TABLE [dbo].[Teams] ADD [PeriodStart] datetime2(7) NOT NULL, [PeriodEnd] datetime2(7) NOT NULL",
        "DROP INDEX [ix_TeamsHistory] ON [dbo].[TeamsHistory]",
        "ALTER TABLE [dbo].[TeamsHistory] DROP COLUMN [PeriodStart], [PeriodEnd]",
        "ALTER TABLE [dbo].[TeamsHistory] ADD [PeriodStart] datetime2(7) NOT NULL, [PeriodEnd] datetime2(7) NOT NULL",
    ];

    private static readonly string[] SqlServerFinish =
    [
        "ALTER TABLE [dbo].[Teams] ADD PERIOD FOR SYSTEM_TIME ([PeriodStart], [PeriodEnd])",
        "ALTER TABLE [dbo].[Teams] ALTER COLUMN [PeriodStart] ADD HIDDEN",
        "CREATE UNIQUE NONCLUSTERED INDEX [IX_Teams_Period] ON [dbo].[Teams] ([PeriodEnd] ASC, [PeriodStart] DESC) INCLUDE ([TeamName])",
        "CREATE CLUSTERED INDEX [ix_TeamsHistory] ON [dbo].[TeamsHistory] ([PeriodEnd] ASC) WHERE ([PeriodEnd]>'2000-01-01')",
        "ALTER TABLE [dbo].[Teams] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[TeamsHistory], DATA_CONSISTENCY_CHECK = ON))",
    ];

    [Test]
    public async Task SqlServerWriter_TakesTheTemporalTablesApart_ForTheLoad_ThenPutsThemBackAsTheyWere()
    {
        _connection.Answer = SqlServerCatalog;
        await _connection.OpenAsync();
        await using var transaction = await _connection.BeginTransactionAsync();
        await using var writer = new SqlServerTargetWriter(_connection, transaction);

        await writer.PrepareAsync([Teams], CancellationToken.None);
        var prepared = Statements(_connection);
        await writer.WriteAsync(Teams, [], CancellationToken.None);
        await writer.FinishAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prepared, Is.EqualTo(SqlServerPrepare));
            Assert.That(Statements(_connection).Skip(prepared.Count), Is.EqualTo(SqlServerFinish), "no rows: nothing written");
            Assert.That(_connection.Sent.Select(c => c.Transaction), Is.All.SameAs(transaction), "all of it in the copy's transaction");
        }
    }

    [Test]
    public async Task SqlServerWriter_WithNoTemporalTables_OnlyClearsTheTables()
    {
        await _connection.OpenAsync();
        await using var transaction = await _connection.BeginTransactionAsync();
        await using var writer = new SqlServerTargetWriter(_connection, transaction);

        await writer.PrepareAsync([Teams], CancellationToken.None);
        await writer.FinishAsync(CancellationToken.None);

        Assert.That(Statements(_connection), Is.EqualTo(OnlyTheDelete));
    }

    private static readonly TablePlan Parent = new(new TableName("", "Parent"), [new ColumnInfo("Id", "integer", false, true)], [0], [], []);
    private static readonly TablePlan Child = new(new TableName("Event", "Child"), [new ColumnInfo("Id", "integer", false, true)], [0], [], []);

    private static readonly string[] PostgresStatements =
    [
        "ALTER TABLE \"public\".\"Parent\" DISABLE TRIGGER USER",
        "ALTER TABLE \"Event\".\"Child\" DISABLE TRIGGER USER",
        "DELETE FROM \"Event\".\"Child\"",
        "DELETE FROM \"public\".\"Parent\"",
        "ALTER TABLE \"public\".\"Parent\" ENABLE TRIGGER USER",
        "ALTER TABLE \"Event\".\"Child\" ENABLE TRIGGER USER",
        "ANALYZE",
    ];

    [Test]
    public async Task PostgresWriter_TurnsTheTriggersOffForTheLoad_ClearsChildrenFirst_AndAnalyzesAfterTheCommit()
    {
        // PostgreSQL's driver doesn't need each command to name the transaction
        await using var postgres = new FakeDbConnection();
        await postgres.OpenAsync();
        var log = new FakeLogger<ServerSqlUnitTests>();
        await using var transaction = await postgres.BeginTransactionAsync();
        await using var writer = new PostgresTargetWriter(postgres, transaction, log);

        await writer.PrepareAsync([Parent, Child], CancellationToken.None);
        await writer.WriteAsync(Parent, [], CancellationToken.None);
        await writer.FinishAsync(CancellationToken.None);
        await transaction.CommitAsync();
        await writer.AfterCommitAsync(CancellationToken.None);

        var analyze = postgres.Sent[^1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Statements(postgres), Is.EqualTo(PostgresStatements));
            Assert.That((analyze.Transaction, analyze.Timeout), Is.EqualTo(((System.Data.Common.DbTransaction?)null, 0)), "after the commit, as long as it takes");
            Assert.That(log.At(LogLevel.Information), Is.EqualTo(Analyzed));
        }
    }

    [TestCase(DatabaseProvider.SqlServer, "A SQL Server target needs a SqlConnection")]
    [TestCase(DatabaseProvider.Postgres, "A PostgreSQL target needs an NpgsqlConnection")]
    public async Task For_AnotherDriversConnection_IsRefused_BeforeAnythingIsSent(DatabaseProvider provider, string message)
    {
        await _connection.OpenAsync();
        await using var transaction = await _connection.BeginTransactionAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => TargetWriter.For(provider, _connection, transaction, new FakeLogger<ServerSqlUnitTests>()),
                Throws.ArgumentException.With.Message.StartsWith(message));
            Assert.That(_connection.Sent, Is.Empty);
        }
    }

    // ---- NativeBackups.SqlServerAsync ----

    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory("grif-serversql-test-").FullName;

    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private static readonly byte[] Bak = [0x54, 0x41, 0x50, 0x45];

    /// <summary>SQL Server's answers: its default backup folder, its edition, the backup made, and the file read back.</summary>
    private static object? SqlServerBackups(SentCommand sent, int engineEdition, string edition) => sent.Sql switch
    {
        var s when s.Contains("InstanceDefaultBackupPath", StringComparison.Ordinal) => "/var/opt/mssql/backups/",
        var s when s.Contains("EngineEdition", StringComparison.Ordinal) => FakeDbConnection.Rows(["e", "n"], [engineEdition, edition]),
        var s when s.Contains("OPENROWSET(BULK", StringComparison.Ordinal) => FakeDbConnection.Rows(["BulkColumn"], [Bak]),
        _ => 0,
    };

    [Test]
    public async Task Bak_InTheDefaultFolder_Compressed_WhereTheEditionCan_IsReadBackIntoTheFile()
    {
        _connection.Answer = sent => SqlServerBackups(sent, 3, "Developer Edition (64-bit)");
        var path = Path.Combine(_directory, "made.bak");

        await NativeBackups.SqlServerAsync(_connection, null, path, CancellationToken.None);

        var backup = _connection.Sent.Single(c => c.Sql.StartsWith("BACKUP DATABASE", StringComparison.Ordinal));
        var read = _connection.Sent.Single(c => c.Sql.Contains("OPENROWSET(BULK", StringComparison.Ordinal));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(Bak));
            Assert.That(backup.Sql, Is.EqualTo(NativeBackups.BackupStatement(compress: true)));
            Assert.That(backup.Parameters, Is.EquivalentTo(new Dictionary<string, object?> { ["@database"] = "grif", ["@file"] = "/var/opt/mssql/backups/download_grif.bak" }));
            Assert.That(read.Parameters, Is.EquivalentTo(new Dictionary<string, object?> { ["@file"] = "/var/opt/mssql/backups/download_grif.bak" }));
            Assert.That((backup.Timeout, read.Timeout), Is.EqualTo((0, 0)), "never timed out");
            Assert.That(_connection.Sent.Where(c => c.Sql.Contains("SERVERPROPERTY", StringComparison.Ordinal)).Select(c => c.Timeout), Is.All.EqualTo(30), "the server's properties: the default");
            Assert.That(_connection.Opened, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Bak_InAGivenFolderAndName_Uncompressed_OnAWebEdition()
    {
        _connection.Answer = sent => SqlServerBackups(sent, 2, "Web Edition (64-bit)");

        await NativeBackups.SqlServerAsync(_connection, "/backups/native/", Path.Combine(_directory, "made.bak"), CancellationToken.None, fileName: "download_scratch.bak");

        var backup = _connection.Sent.Single(c => c.Sql.StartsWith("BACKUP DATABASE", StringComparison.Ordinal));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(backup.Sql, Is.EqualTo(NativeBackups.BackupStatement(compress: false)));
            Assert.That(backup.Parameters["@file"], Is.EqualTo("/backups/native/download_scratch.bak"));
            Assert.That(_connection.Sent.Any(c => c.Sql.Contains("InstanceDefaultBackupPath", StringComparison.Ordinal)), Is.False, "the folder was given");
        }
    }

    [Test]
    public void Bak_ThatSqlServerFailsToMake_FailsTheBackup_WritingNoFile()
    {
        var path = Path.Combine(_directory, "made.bak");
        _connection.Answer = sent => sent.Sql.StartsWith("BACKUP DATABASE", StringComparison.Ordinal)
            ? throw new InvalidOperationException("disk full")
            : SqlServerBackups(sent, 3, "Enterprise Edition");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await NativeBackups.SqlServerAsync(_connection, "/b", path, CancellationToken.None), Throws.InvalidOperationException.With.Message.EqualTo("disk full"));
            Assert.That(File.Exists(path), Is.False);
        }
    }

    [Test]
    public void Bak_WhenSqlServerGivesNoEdition_IsUnavailable_BeforeAnyBackup()
    {
        _connection.Answer = sent => sent.Sql.Contains("EngineEdition", StringComparison.Ordinal)
            ? FakeDbConnection.Rows(["e", "n"])
            : SqlServerBackups(sent, 3, "Enterprise Edition");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await NativeBackups.SqlServerAsync(_connection, "/b", Path.Combine(_directory, "made.bak"), CancellationToken.None),
                Throws.TypeOf<NativeBackupUnavailableException>().With.Message.StartsWith("SQL Server didn't say its edition"));
            Assert.That(_connection.Sent.Any(c => c.Sql.StartsWith("BACKUP DATABASE", StringComparison.Ordinal)), Is.False);
        }
    }

    [Test]
    public void Bak_ReadBackAsNothing_IsUnavailable_LeavingNoFile()
    {
        var path = Path.Combine(_directory, "made.bak");
        _connection.Answer = sent => sent.Sql.Contains("OPENROWSET(BULK", StringComparison.Ordinal)
            ? FakeDbConnection.Rows(["BulkColumn"])
            : SqlServerBackups(sent, 3, "Enterprise Edition");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await NativeBackups.SqlServerAsync(_connection, "/b", path, CancellationToken.None),
                Throws.TypeOf<NativeBackupUnavailableException>().With.Message.EqualTo("SQL Server made the backup (/b/download_grif.bak) but reading it back gave nothing. Download a SQL Server script instead."));
            Assert.That(File.Exists(path), Is.False, "no empty file left behind");
        }
    }
}
