using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The export script's statements as each server's ScriptDialect writes them around the rows, for hand-made table
/// plans: what runs before the rows (refusing a database with tables, the schema, the seed rows cleared, children
/// first), the rows' INSERTs (in chunks, generated columns left out), the deferred foreign keys, and what ends it.
/// DatabaseScriptExporter.WriteAsync itself reads a migrated source (the Migrations tests'); these need no database.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScriptDialectWritingUnitTests
{
    private const string Schema = "CREATE TABLE [A] ([Id] int);\nGO\nCREATE TABLE [B] ([Id] int);";
    private static readonly TableName Parent = new("", "Parent");
    private static readonly TableName Child = new("", "Child");

    private readonly StringWriter _output = new() { NewLine = "\n" };

    [TearDown]
    public void TearDown() => _output.Dispose();

    private static ColumnInfo Column(string name, string type, bool generated = false) => new(name, type, Generated: generated, Identity: false);

    private static TablePlan Plan(TableName name, params ColumnInfo[] columns) => new(name, [.. columns], key: [0], selfReferences: [], deferred: []);

    /// <summary>A team row whose captain (column 2) is set after every row is in, by its key (column 0).</summary>
    private static TablePlan Teams() => new(new TableName("", "Teams"),
        [Column("TeamID", "int"), Column("TeamName", "nvarchar(max)"), Column("CaptainID", "int")], key: [0], selfReferences: [], deferred: [2]);

    private static readonly TablePlan[] TwoTables = [Plan(Parent, Column("Id", "int")), Plan(Child, Column("Id", "int"))];

    private string Written() => _output.ToString();

    [Test]
    public void For_EachServer_ItsOwnScript()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScriptDialect.For(DatabaseProvider.SqlServer), Is.TypeOf<SqlServerScript>());
            Assert.That(ScriptDialect.For(DatabaseProvider.Postgres), Is.TypeOf<PostgresScript>());
        }
    }

    [Test]
    public async Task SqlServer_Begin_RefusesATakenDatabase_ChecksEachSchemaBatch_AndClearsChildrenFirst()
    {
        await new SqlServerScript().BeginAsync(_output, DatabaseProvider.Postgres, "20261101000000_Last", Schema, TwoTables);

        var script = Written();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(script, Does.StartWith("-- GrifballWebApp database export: a Postgres database, as this server's script."));
            Assert.That(script, Does.Contain("-- Migrations up to 20261101000000_Last."));
            Assert.That(script, Does.Contain("sqlcmd -S <server> -d <new database> -b -i <this file>"));
            Assert.That(script, Does.Contain("RAISERROR(N'This database already has tables: restore into a new, empty database.', 16, 1);"));
            Assert.That(script, Does.Contain("CREATE TABLE [A] ([Id] int);\nGO\nIF @@TRANCOUNT = 0 SET NOEXEC ON;\nCREATE TABLE [B] ([Id] int);"),
                "the schema's own GO starts a batch that checks the transaction too");
            Assert.That(script.IndexOf("DELETE FROM [dbo].[Child];", StringComparison.Ordinal),
                Is.LessThan(script.IndexOf("DELETE FROM [dbo].[Parent];", StringComparison.Ordinal)).And.Not.Negative);
            Assert.That(script, Does.Contain("SET (SYSTEM_VERSIONING = OFF)"));
            Assert.That(script, Does.Contain("DROP PERIOD FOR SYSTEM_TIME"));
        }
    }

    [Test]
    public async Task SqlServer_Insert_NoRows_WritesNothing()
    {
        await new SqlServerScript().InsertAsync(_output, Plan(Parent, Column("Id", "int")), []);

        Assert.That(Written(), Is.Empty);
    }

    [Test]
    public async Task SqlServer_Insert_TakesTheIdentityValues_LeavesGeneratedColumnsOut_AndReseeds()
    {
        var table = Plan(new TableName("", "Users"), Column("Id", "int"), Column("Name", "nvarchar(max)"), Column("Total", "int", generated: true));

        await new SqlServerScript().InsertAsync(_output, table, [[1, "it's", 10], [2, "b", 20]]);

        Assert.That(Written(), Is.EqualTo("""
            IF OBJECTPROPERTY(OBJECT_ID(N'[dbo].[Users]'), 'TableHasIdentity') = 1 SET IDENTITY_INSERT [dbo].[Users] ON;
            INSERT INTO [dbo].[Users] ([Id], [Name]) VALUES
              (1, N'it''s'),
              (2, N'b');
            GO
            IF @@TRANCOUNT = 0 SET NOEXEC ON;
            IF OBJECTPROPERTY(OBJECT_ID(N'[dbo].[Users]'), 'TableHasIdentity') = 1 SET IDENTITY_INSERT [dbo].[Users] OFF;
            IF OBJECTPROPERTY(OBJECT_ID(N'[dbo].[Users]'), 'TableHasIdentity') = 1 DBCC CHECKIDENT (N'[dbo].[Users]', RESEED) WITH NO_INFOMSGS;
            GO
            IF @@TRANCOUNT = 0 SET NOEXEC ON;

            """));
    }

    [Test]
    public async Task Insert_ManyRows_GoInChunksOfFiveHundred()
    {
        var rows = Enumerable.Range(1, 501).Select(i => new object?[] { i }).ToList();

        await new PostgresScript().InsertAsync(_output, Plan(Parent, Column("Id", "integer")), rows);

        var lines = Written().Split('\n');
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines.Count(l => l == "INSERT INTO \"public\".\"Parent\" (\"Id\") VALUES"), Is.EqualTo(2));
            Assert.That(lines, Does.Contain("  (500);").And.Contain("  (501);"), "each chunk's last row ends its statement");
        }
    }

    [Test]
    public async Task SqlServer_Deferred_SetsEachRowsKeysByItsKey_ThenEndsTheBatch()
    {
        await new SqlServerScript().SetDeferredAsync(_output, Teams(), [[1, "Red", 5], [2, "Blue", 6]]);

        Assert.That(Written(), Is.EqualTo("""
            UPDATE [dbo].[Teams] SET [CaptainID] = 5 WHERE [TeamID] = 1;
            UPDATE [dbo].[Teams] SET [CaptainID] = 6 WHERE [TeamID] = 2;
            GO
            IF @@TRANCOUNT = 0 SET NOEXEC ON;

            """));
    }

    [Test]
    public async Task SqlServer_End_PutsSystemVersioningBack_AndCommits()
    {
        await new SqlServerScript().EndAsync(_output, TwoTables);

        var script = Written();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(script, Does.Contain("ADD PERIOD FOR SYSTEM_TIME"));
            Assert.That(script, Does.Contain("SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + HistoryName + N', DATA_CONSISTENCY_CHECK = ON))"));
            Assert.That(script, Does.Contain("COMMIT TRANSACTION;\nGO\nSET NOEXEC OFF;\nGO"));
        }
    }

    [Test]
    public async Task Postgres_Begin_RefusesATakenDatabase_TurnsTheTriggersOff_AndClearsChildrenFirst()
    {
        await new PostgresScript().BeginAsync(_output, DatabaseProvider.SqlServer, "20261101000000_Last", "CREATE TABLE a (id int);", TwoTables);

        var script = Written();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(script, Does.StartWith("-- GrifballWebApp database export: a SqlServer database, as this server's script."));
            Assert.That(script, Does.Contain("psql -v ON_ERROR_STOP=1 -d <new database> -f <this file>"));
            Assert.That(script, Does.Contain("RAISE EXCEPTION 'This database already has tables: restore into a new, empty database.';"));
            Assert.That(script, Does.Contain("CREATE TABLE a (id int);\nALTER TABLE \"public\".\"Parent\" DISABLE TRIGGER USER;\nALTER TABLE \"public\".\"Child\" DISABLE TRIGGER USER;\n"));
            Assert.That(script, Does.EndWith("DELETE FROM \"public\".\"Child\";\nDELETE FROM \"public\".\"Parent\";\n"));
        }
    }

    [Test]
    public async Task Postgres_Insert_AndDeferred_AsPostgresWritesThem()
    {
        var script = new PostgresScript();

        await script.InsertAsync(_output, Teams(), [[1, "Red", null]]);
        await script.InsertAsync(_output, Teams(), []);
        await script.SetDeferredAsync(_output, Teams(), [[1, "Red", 5]]);

        Assert.That(Written(), Is.EqualTo("""
            INSERT INTO "public"."Teams" ("TeamID", "TeamName", "CaptainID") VALUES
              (1, 'Red', NULL);

            UPDATE "public"."Teams" SET "CaptainID" = 5 WHERE "TeamID" = 1;

            """));
    }

    [Test]
    public async Task Postgres_End_TurnsTheTriggersBackOn_CarriesTheSequencesOn_AndCommits()
    {
        await new PostgresScript().EndAsync(_output, TwoTables);

        var script = Written();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(script, Does.StartWith("ALTER TABLE \"public\".\"Parent\" ENABLE TRIGGER USER;\nALTER TABLE \"public\".\"Child\" ENABLE TRIGGER USER;\n"));
            Assert.That(script, Does.Contain("setval(pg_get_serial_sequence"));
            Assert.That(script, Does.EndWith("COMMIT;\nANALYZE;\n"));
        }
    }

    [Test]
    public void Postgres_Literals_ForNullAndAFloat()
    {
        var postgres = new PostgresScript();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(postgres.Literal(null, Column("C", "integer")), Is.EqualTo("NULL"));
            Assert.That(postgres.Literal(1.5f, Column("C", "real")), Is.EqualTo("1.5"));
        }
    }

    [Test]
    public async Task TargetColumns_OnSqlServer_AddsEachTemporalTablesHistory_WithItsColumns_NoneGenerated()
    {
        await using var context = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.SqlServer, new SqlServerScript().ModelConnectionString));
        var model = context.GetService<IDesignTimeModel>().Model;
        var temporal = model.GetEntityTypes().First(e => e.IsTemporal() && e.GetTableName() is not null);

        var columns = DatabaseScriptExporter.TargetColumns(context, DatabaseProvider.SqlServer);

        var current = columns[new TableName($"{temporal.GetSchema()}", temporal.GetTableName()!)];
        var history = columns[SqlServerDialect.Instance.Table($"{temporal.GetHistoryTableSchema()}", temporal.GetHistoryTableName()!)];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(columns, Has.Count.GreaterThan(model.GetRelationalModel().Tables.Count()), "the history tables besides the model's own");
            Assert.That(history.Select(c => (c.Name, c.DataType)), Is.EqualTo(current.Select(c => (c.Name, c.DataType))));
            Assert.That(history.Select(c => c.Generated), Is.All.False);
        }
    }

    [Test]
    public async Task TargetColumns_OnPostgres_AreTheModelsTables_WithTheirStoreTypes()
    {
        await using var context = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.Postgres, new PostgresScript().ModelConnectionString));
        var relational = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        var columns = DatabaseScriptExporter.TargetColumns(context, DatabaseProvider.Postgres);

        var users = relational.Tables.First(t => t.Name == "Users");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(columns, Has.Count.EqualTo(relational.Tables.Count()));
            Assert.That(columns[new TableName(users.Schema ?? "", users.Name)].Select(c => (c.Name, c.DataType)),
                Is.EqualTo(users.Columns.Select(c => (c.Name, c.StoreType))));
        }
    }
}
