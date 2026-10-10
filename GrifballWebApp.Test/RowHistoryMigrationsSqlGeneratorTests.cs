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
    public void CreateTable_VersionedWithTheExtension_ButNotEFsHistory()
    {
        var create = Sql(new CreateTableOperation
        {
            Name = "It's new",
            Columns = { new AddColumnOperation { Table = "It's new", Name = "Id", ClrType = typeof(int), ColumnType = "integer" } },
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
            Assert.That(efHistory, Does.Not.Contain("periods"));
        });
    }

    [Test]
    public void OtherOperations_AsNpgsqlWritesThem()
    {
        var sql = Sql(new CreateIndexOperation { Schema = "Event", Table = "Seasons", Name = "IX_Test", Columns = ["SeasonName"] });

        Assert.That(sql, Is.EqualTo("CREATE INDEX \"IX_Test\" ON \"Event\".\"Seasons\" (\"SeasonName\");\n"));
    }
}
