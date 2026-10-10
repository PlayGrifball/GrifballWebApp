using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Test;

/// <summary>
/// The SQL RowHistoryMigrationsSqlGenerator writes, without a database (RowHistoryTests runs it on one).
/// </summary>
[TestFixture]
public class RowHistoryMigrationsSqlGeneratorTests
{
    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Postgres, "Host=example;Database=Grif;Username=postgres;Password=unused").Options);
        return string.Concat(context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.Model).Select(c => c.CommandText));
    }

    [Test]
    public void Postgres_UsesIt_SqlServerNot()
    {
        using var postgres = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Postgres, "Host=example;Database=Grif").Options);
        using var sqlServer = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.SqlServer, "Server=example;Database=Grif").Options);

        Assert.Multiple(() =>
        {
            Assert.That(postgres.GetService<IMigrationsSqlGenerator>(), Is.TypeOf<RowHistoryMigrationsSqlGenerator>());
            Assert.That(sqlServer.GetService<IMigrationsSqlGenerator>(), Is.Not.InstanceOf<RowHistoryMigrationsSqlGenerator>());
        });
    }

    [Test]
    public void AddColumn_SameColumnOnTheHistory_TypeFromTheModel_WithItsDefault()
    {
        var sql = Sql(new AddColumnOperation { Schema = "Event", Table = "Seasons", Name = "Round", ClrType = typeof(int), DefaultValue = 1 });

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("""EXECUTE 'SELECT history_table_name FROM periods.system_versioning WHERE table_name = $1::regclass'"""));
            Assert.That(sql, Does.Contain("""USING '"Event"."Seasons"';"""));
            Assert.That(sql, Does.Contain("""PERFORM periods.drop_system_versioning('"Event"."Seasons"');"""));
            Assert.That(sql, Does.Contain("""EXECUTE $grif_sql$ALTER TABLE "Event"."Seasons" ADD "Round" integer NOT NULL DEFAULT 1;"""));
            Assert.That(sql, Does.Contain("""EXECUTE 'ALTER TABLE ' || history::text || $grif_sql$ ADD COLUMN "Round" integer DEFAULT 1$grif_sql$;"""));
            Assert.That(sql, Does.Contain("""PERFORM periods.add_system_versioning('"Event"."Seasons"');"""));
        });
    }

    [Test]
    public void AlterColumn_NeitherTypeNorNullability_ChangesNothingOnTheHistory()
    {
        var sql = Sql(new AlterColumnOperation
        {
            Schema = "Event", Table = "Seasons", Name = "SeasonName", ClrType = typeof(string), ColumnType = "text", DefaultValue = "x",
            OldColumn = new AddColumnOperation { ClrType = typeof(string), ColumnType = "text" },
        });

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("""ALTER COLUMN "SeasonName" SET DEFAULT 'x'"""));
            Assert.That(sql, Does.Not.Contain("history::text"));
            Assert.That(sql, Does.Contain("""PERFORM periods.add_system_versioning('"Event"."Seasons"');"""));
        });
    }

    [Test]
    public void RenameTable_InItsSchema_RenamesTheHistoryAlike()
    {
        var sql = Sql(new RenameTableOperation { Schema = "Event", Name = "Seasons", NewName = "Leagues" });

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("""EXECUTE format('ALTER TABLE %s RENAME TO %I', history, periods._choose_name(ARRAY['Leagues']::name[], 'history'));"""));
            Assert.That(sql, Does.Not.Contain("SET SCHEMA"));
            Assert.That(sql, Does.Contain("""PERFORM periods.add_system_versioning('"Event"."Leagues"');"""));
        });
    }

    [Test]
    public void RenameTable_SchemaOnly_MovesTheHistoryAlike()
    {
        var sql = Sql(new RenameTableOperation { Schema = "Event", Name = "Seasons", NewSchema = "Other" });

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("RENAME TO %I"));
            Assert.That(sql, Does.Contain("""EXECUTE format('ALTER TABLE %s SET SCHEMA %I', history, 'Other');"""));
            Assert.That(sql, Does.Contain("""PERFORM periods.add_system_versioning('"Other"."Seasons"');"""));
        });
    }

    [Test]
    public void CreateTable_WithThePeriodsColumns_VersionedWithTheExtension_OthersNot()
    {
        AddColumnOperation Column(string table, string name) =>
            new() { Table = table, Name = name, ClrType = typeof(DateTime), ColumnType = "timestamp with time zone" };
        // As EF creates a table temporal on SQL Server: with the period's columns (RowHistory).
        var create = Sql(new CreateTableOperation
        {
            Name = "It's new",
            Columns =
            {
                new AddColumnOperation { Table = "It's new", Name = "Id", ClrType = typeof(int), ColumnType = "integer" },
                Column("It's new", RowHistory.PeriodStart),
                Column("It's new", RowHistory.PeriodEnd),
            },
        });
        // Not temporal on SQL Server, as MatchReschedules.
        var plain = Sql(new CreateTableOperation
        {
            Name = "Plain",
            Columns = { new AddColumnOperation { Table = "Plain", Name = "Id", ClrType = typeof(int), ColumnType = "integer" } },
        });
        var efHistory = Sql(new CreateTableOperation
        {
            Name = HistoryRepository.DefaultTableName,
            Columns = { new AddColumnOperation { Table = HistoryRepository.DefaultTableName, Name = "MigrationId", ClrType = typeof(string), ColumnType = "text" } },
        });

        Assert.Multiple(() =>
        {
            Assert.That(create, Does.Contain("IF EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') THEN"));
            Assert.That(create, Does.Contain("""PERFORM periods.add_system_time_period('"It''s new"', 'PeriodStart', 'PeriodEnd');"""));
            Assert.That(plain, Does.Not.Contain("periods"));
            Assert.That(efHistory, Does.Not.Contain("periods"));
        });
    }

    private static AddColumnOperation PeriodColumn(string name, string? defaultSql, bool nullable = false) => new()
    {
        Schema = "Event", Table = "Seasons", Name = name, ClrType = typeof(DateTime), ColumnType = "timestamp with time zone",
        IsNullable = nullable, DefaultValueSql = defaultSql,
    };

    private const string VersionedOnceItHasBoth = """
            IF EXISTS (SELECT FROM pg_extension WHERE extname = 'periods')
            AND (SELECT count(*) FROM pg_attribute
                 WHERE attrelid = '"Event"."Seasons"'::regclass AND attname IN ('PeriodStart', 'PeriodEnd') AND NOT attisdropped) = 2 THEN
                PERFORM periods.add_system_time_period('"Event"."Seasons"', 'PeriodStart', 'PeriodEnd');
                PERFORM periods.add_system_versioning('"Event"."Seasons"');
        """;

    // A table made temporal: the rows already there since -infinity, as periods adds the column itself;
    // new rows since their transaction.
    [Test]
    public void AddColumn_PeriodStart_RowsThereSinceMinusInfinity_ThenTheModelsDefault_ThenVersioned()
    {
        var sql = Sql(PeriodColumn(RowHistory.PeriodStart, RowHistory.StartDefault));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.StartWith("""
                ALTER TABLE "Event"."Seasons" ADD "PeriodStart" timestamp with time zone NOT NULL DEFAULT ('-infinity');
                ALTER TABLE "Event"."Seasons" ALTER COLUMN "PeriodStart" SET DEFAULT transaction_timestamp();
                DO $grif_history$
                """));
            Assert.That(sql, Does.Contain(VersionedOnceItHasBoth));
            Assert.That(sql, Does.Not.Contain("drop_system_versioning"));
        });
    }

    [Test]
    public void AddColumn_PeriodEnd_AsItIs_ThenVersioned()
    {
        var sql = Sql(PeriodColumn(RowHistory.PeriodEnd, RowHistory.EndDefault));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.StartWith("""ALTER TABLE "Event"."Seasons" ADD "PeriodEnd" timestamp with time zone NOT NULL DEFAULT ('infinity'::timestamp with time zone);"""));
            Assert.That(sql, Does.Contain(VersionedOnceItHasBoth));
        });
    }

    // Without a default (not the model's), nothing to stand in for: the column as it is.
    [Test]
    public void AddColumn_PeriodStart_WithoutDefault_AsItIs()
    {
        var sql = Sql(PeriodColumn(RowHistory.PeriodStart, null, nullable: true));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.StartWith("""ALTER TABLE "Event"."Seasons" ADD "PeriodStart" timestamp with time zone;"""));
            Assert.That(sql, Does.Not.Contain("-infinity"));
            Assert.That(sql, Does.Contain(VersionedOnceItHasBoth));
        });
    }

    // A table no longer temporal (RowHistory's Down): its history and period purged, then the column.
    [Test]
    public void DropColumn_PeriodColumn_PurgesHistoryAndPeriodFirst()
    {
        var sql = Sql(new DropColumnOperation { Schema = "Event", Table = "Seasons", Name = RowHistory.PeriodEnd });

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("""INTO versioned, period USING '"Event"."Seasons"';"""));
            Assert.That(sql, Does.Contain("""PERFORM periods.drop_system_versioning('"Event"."Seasons"', purge => true);"""));
            Assert.That(sql, Does.Contain("""PERFORM periods.drop_system_time_period('"Event"."Seasons"', purge => true);"""));
            Assert.That(sql, Does.Contain("""EXECUTE $grif_sql$ALTER TABLE "Event"."Seasons" DROP COLUMN "PeriodEnd";"""));
            Assert.That(sql, Does.Not.Contain("add_system_versioning"));
            Assert.That(sql.IndexOf("drop_system_time_period"), Is.LessThan(sql.IndexOf("DROP COLUMN")));
        });
    }

    [Test]
    public void OtherOperations_AsNpgsqlWritesThem()
    {
        var sql = Sql(new CreateIndexOperation { Schema = "Event", Table = "Seasons", Name = "IX_Test", Columns = ["SeasonName"] });

        Assert.That(sql, Is.EqualTo("CREATE INDEX \"IX_Test\" ON \"Event\".\"Seasons\" (\"SeasonName\");\n"));
    }
}
